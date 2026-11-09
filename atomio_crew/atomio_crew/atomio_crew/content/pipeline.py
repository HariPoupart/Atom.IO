"""Writer -> Critic -> revise loop that generates Atom.IO content from the GDD.

For each content spec:
  1. Retrieval: pull the GDD sections the spec depends on + the style guide.
  2. Writer agent drafts the content as JSON.
  3. Critic = rule checks in code (numbers, limits, coverage, banned lore)
            + an LLM Consistency Critic that reviews the draft against the GDD
              (contradictions, false facts, invented lore, off-voice text).
  4. If any blocker/major issue remains, the Writer revises with the critic's
     feedback. Repeat up to ``max_rounds``.
  5. Every round is logged; the final content and a critic report are saved.
"""
from __future__ import annotations

import json
import os
import re
import shutil
from datetime import datetime
from pathlib import Path

from pydantic import ValidationError

from .. import budget
from . import context as C
from .specs import SPECS, ContentSpec

DEFAULT_WRITER = "anthropic/claude-sonnet-4-5"
DEFAULT_CRITIC = "anthropic/claude-sonnet-4-5"
CONTENT_TOKEN_CAP = 700_000
CONTENT_USD_CAP = float(os.getenv("ATOMIO_CONTENT_USD_CAP", "5"))
BLOCKING = {"blocker", "major"}


# --------------------------------------------------------------------------- prompts
WRITER_SYSTEM = """You are the Atom.IO Writer agent. You write game-ready content for Atom.IO, an Android \
arcade .io game where the player is an atom climbing the periodic table (Bohr model). You ALWAYS ground \
your writing in the GDD excerpt you are given and follow the style guide exactly. Output ONLY one JSON \
object matching the schema. No markdown, no commentary.

STYLE GUIDE:
{style}"""

CRITIC_SYSTEM = """You are the Atom.IO Consistency Critic agent. You did not write the draft; your job is \
to catch problems before they reach the game. Review the draft against the GDD excerpt and the style guide:
1. Contradictions with GDD mechanics or numbers (blocker).
2. False or doubtful real-world facts (blocker).
3. Invented lore, characters or story; Atom.IO has none (major).
4. Off-voice or confusing text; it must sound like Atom.IO: punchy arcade, Bohr-model atom, real chemistry (major).
5. Polish (minor).
A rule checker already ran in code; its findings are listed. Do NOT repeat them; find what code cannot.
Only flag real problems, quote the item, and give a concrete fix. verdict is "pass" only if you found no \
blocker or major issue. Output ONLY JSON:
{{"verdict": "pass"|"revise", "voice_score": 1-5, "summary": "...", "issues": [{{"severity": \
"blocker"|"major"|"minor", "item": "...", "problem": "...", "fix": "...", "gdd_ref": "section id or style rule"}}]}}

STYLE GUIDE:
{style}"""


def _context_block(spec: ContentSpec) -> str:
    extra = spec.extra_context()
    return (f"CONTENT_KEY: {spec.key}\n\nGDD EXCERPT (source of truth):\n{C.gdd_excerpt(spec.gdd_sections)}"
            + (f"\n\n{extra}" if extra else ""))


def writer_messages(spec: ContentSpec, previous: str | None, issues: list[dict]) -> list[dict]:
    schema = json.dumps(spec.model.model_json_schema())
    user = (f"{_context_block(spec)}\n\nTASK:\n{spec.task}\n\nJSON SCHEMA:\n{schema}\n")
    if previous is not None:
        fb = "\n".join(f"- [{i['severity']}] {i['item']}: {i['problem']} -> FIX: {i['fix']}" for i in issues)
        user += (f"\nPREVIOUS DRAFT:\n{previous}\n\nCRITIC FEEDBACK (fix every blocker and major issue, keep "
                 f"everything that was not flagged):\n{fb}\n\nReturn the full revised JSON.")
    else:
        user += "\nReturn the JSON now."
    return [{"role": "system", "content": WRITER_SYSTEM.format(style=C.style_guide())},
            {"role": "user", "content": user}]


def critic_messages(spec: ContentSpec, draft: str, rule_issues: list[dict]) -> list[dict]:
    found = "\n".join(f"- {i['item']}: {i['problem']}" for i in rule_issues) or "(none)"
    user = (f"{_context_block(spec)}\n\nTHE WRITER WAS ASKED TO:\n{spec.task}\n\nRULE CHECKER FINDINGS "
            f"(already reported, do not repeat):\n{found}\n\nDRAFT TO REVIEW:\n{draft}")
    return [{"role": "system", "content": CRITIC_SYSTEM.format(style=C.style_guide())},
            {"role": "user", "content": user}]


# --------------------------------------------------------------------------- helpers
def extract_json(text: str) -> dict:
    """Pull the JSON object out of an LLM reply (tolerates code fences / chatter)."""
    if not isinstance(text, str):
        text = json.dumps(text) if isinstance(text, dict) else str(text)
    m = re.search(r"```(?:json)?\s*(\{.*\})\s*```", text, re.S)
    if m:
        text = m.group(1)
    start, end = text.find("{"), text.rfind("}")
    if start == -1 or end <= start:
        raise ValueError("no JSON object in reply")
    return json.loads(text[start:end + 1])


def make_llms(mock: bool):
    if mock:
        from .mock import ContentMockLLM
        return (lambda spec: ContentMockLLM(model="mock/writer", role="writer")), \
            ContentMockLLM(model="mock/critic", role="critic")
    from crewai import LLM
    writer_model = os.getenv("ATOMIO_MODEL_WRITER") or os.getenv("ATOMIO_MODEL") or DEFAULT_WRITER
    critic_model = os.getenv("ATOMIO_MODEL_CRITIC") or os.getenv("ATOMIO_MODEL") or DEFAULT_CRITIC
    return (lambda spec: LLM(model=writer_model, temperature=0.7, max_tokens=spec.max_tokens)), \
        LLM(model=critic_model, temperature=0.1, max_tokens=8000)


class Usage:
    def __init__(self) -> None:
        self.llms: list = []

    def add(self, llm) -> None:
        if llm not in self.llms:
            self.llms.append(llm)

    def total(self) -> dict:
        t = {"prompt_tokens": 0, "completion_tokens": 0, "calls": 0}
        for llm in self.llms:
            u = budget.usage_of(llm)
            for k in t:
                t[k] += u[k]
        t["total_tokens"] = t["prompt_tokens"] + t["completion_tokens"]
        t["usd"] = round(budget.cost_usd(t), 4)
        return t

    def check(self) -> None:
        t = self.total()
        if t["total_tokens"] > CONTENT_TOKEN_CAP or t["usd"] > CONTENT_USD_CAP:
            raise budget.BudgetExceeded(f"content pipeline used {t['total_tokens']:,} tokens / ${t['usd']:.2f}")


# --------------------------------------------------------------------------- the loop
def run_spec(spec: ContentSpec, writer, critic, usage: Usage, max_rounds: int, run_dir: Path,
             say=print) -> dict:
    usage.add(writer)
    usage.add(critic)
    history: list[dict] = []
    previous_raw: str | None = None
    issues: list[dict] = []
    best = None  # (n_blocking, round, obj)

    for rnd in range(1, max_rounds + 1):
        say(f"  round {rnd}: Writer {'drafting' if rnd == 1 else 'revising'}...")
        raw = writer.call(writer_messages(spec, previous_raw, issues))
        usage.check()

        obj, rule_issues = None, []
        try:
            obj = spec.model.model_validate(extract_json(raw))
            rule_issues = spec.checks(obj)
            draft_json = obj.model_dump_json(indent=1)
        except (ValueError, ValidationError) as e:
            rule_issues = [{"severity": "blocker", "item": "draft", "problem": f"not valid JSON for the schema: "
                            f"{str(e)[:300]}", "fix": "Return only a JSON object that matches the schema",
                            "gdd_ref": "", "source": "rules"}]
            draft_json = raw if isinstance(raw, str) else str(raw)

        critic_review = {"verdict": "revise", "voice_score": None, "summary": "skipped: draft unreadable",
                         "issues": []}
        if obj is not None:
            say(f"  round {rnd}: Critic reviewing ({len(rule_issues)} rule findings so far)...")
            for attempt in range(2):
                try:
                    critic_review = extract_json(critic.call(critic_messages(spec, draft_json, rule_issues)))
                    break
                except (ValueError, json.JSONDecodeError):
                    critic_review = {"verdict": "revise", "voice_score": None, "issues": [],
                                     "summary": "critic reply was not valid JSON"}
            usage.check()
        llm_issues = [{**i, "source": "critic"} for i in critic_review.get("issues", []) if isinstance(i, dict)]
        for i in llm_issues:
            i.setdefault("severity", "major")
            i["severity"] = str(i["severity"]).lower()

        issues = rule_issues + llm_issues
        n_block = sum(1 for i in issues if i["severity"] in BLOCKING)
        passed = obj is not None and n_block == 0
        history.append({"round": rnd, "passed": passed, "blocking": n_block,
                        "voice_score": critic_review.get("voice_score"),
                        "critic_summary": critic_review.get("summary", ""), "issues": issues})
        rd = run_dir / spec.key
        rd.mkdir(parents=True, exist_ok=True)
        (rd / f"round_{rnd}_draft.json").write_text(draft_json, encoding="utf-8")
        (rd / f"round_{rnd}_review.json").write_text(json.dumps(history[-1], indent=1), encoding="utf-8")

        say(f"  round {rnd}: {'PASS' if passed else 'REVISE'}  ({n_block} blocking, "
            f"{len(issues) - n_block} minor, voice {critic_review.get('voice_score')}/5)")
        if obj is not None and (best is None or n_block < best[0]):
            best = (n_block, rnd, obj)
        if passed:
            break
        previous_raw = draft_json

    status = "approved" if history and history[-1]["passed"] else "needs_review"
    return {"key": spec.key, "status": status, "rounds": len(history), "history": history,
            "final": best[2] if best else None, "final_round": best[1] if best else None}


def critic_report(results: list[dict], mode: str, usage: dict) -> str:
    L = [f"# Critic report ({mode} run, {datetime.now():%Y-%m-%d %H:%M})", "",
         "Auto-generated by `python main.py generate`. Every issue below was raised by the critic "
         "(`rules` = code checks, `critic` = LLM Consistency Critic) and sent back to the Writer.", "",
         "| Content | Status | Rounds | Issues caught (blocking / total) | Final voice score |",
         "|---|---|---|---|---|"]
    for r in results:
        caught = sum(len(h["issues"]) for h in r["history"])
        blocking = sum(h["blocking"] for h in r["history"])
        L.append(f"| {SPECS[r['key']].title} | **{r['status']}** | {r['rounds']} | {blocking} / {caught} "
                 f"| {r['history'][-1]['voice_score'] if r['history'] else '-'}/5 |")
    L += ["", f"Tokens: {usage['total_tokens']:,} (~${usage['usd']:.2f})", ""]
    for r in results:
        L.append(f"## {SPECS[r['key']].title}")
        for h in r["history"]:
            L.append(f"### Round {h['round']}: {'PASS' if h['passed'] else 'sent back for revision'}")
            if h["critic_summary"]:
                L.append(f"> {h['critic_summary']}")
            if not h["issues"]:
                L.append("- No issues.")
            for i in h["issues"]:
                ref = f" ({i['gdd_ref']})" if i.get("gdd_ref") else ""
                L.append(f"- **{i['severity']}** [{i['source']}] `{i.get('item', '')}`: {i.get('problem', '')}"
                         f"{ref}. *Fix:* {i.get('fix', '')}")
            L.append("")
    return "\n".join(L) + "\n"


def run(keys: list[str], mock: bool, max_rounds: int, out_root: Path, content_dir: Path, say=print) -> dict:
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    run_dir = out_root / "content_runs" / f"run_{stamp}"
    run_dir.mkdir(parents=True, exist_ok=True)
    writer_for, critic = make_llms(mock)
    usage = Usage()
    results, status = [], "completed"

    for key in keys:
        spec = SPECS[key]
        say(f"\n[{spec.title}] grounded in GDD sections {', '.join(spec.gdd_sections)}")
        try:
            results.append(run_spec(spec, writer_for(spec), critic, usage, max_rounds, run_dir, say))
        except budget.BudgetExceeded as e:
            status = f"stopped: {e}"
            break
        except Exception as e:  # keep whatever finished instead of crashing
            status = f"stopped on {key}: {type(e).__name__}: {e}"
            say(f"  ERROR: {status}")
            break

    content_dir.mkdir(parents=True, exist_ok=True)
    unity_dir = content_dir / "unity" / "Assets" / "Resources" / "AtomIO" / "Content"
    unity_dir.mkdir(parents=True, exist_ok=True)
    for r in results:
        if r["final"] is None:
            continue
        spec = SPECS[r["key"]]
        data = r["final"].model_dump()
        payload = {"status": r["status"], "rounds": r["rounds"], **data}
        (content_dir / f"{spec.key}.json").write_text(json.dumps(payload, indent=2, ensure_ascii=False),
                                                       encoding="utf-8")
        (content_dir / f"{spec.key}.md").write_text(
            f"_Status: **{r['status']}** after {r['rounds']} round(s). {spec.why}_\n\n" + spec.to_md(r["final"]),
            encoding="utf-8")
        (unity_dir / f"{spec.key}.json").write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")

    u = usage.total()
    (content_dir / "CRITIC_REPORT.md").write_text(critic_report(results, "mock" if mock else "live", u),
                                                  encoding="utf-8")
    log = {"mode": "mock" if mock else "live", "status": status, "usage": u,
           "results": [{k: v for k, v in r.items() if k != "final"} for r in results]}
    (content_dir / "critic_log.json").write_text(json.dumps(log, indent=1, ensure_ascii=False), encoding="utf-8")
    shutil.copy2(content_dir / "CRITIC_REPORT.md", run_dir / "CRITIC_REPORT.md")
    return {"status": status, "results": results, "usage": u, "run_dir": run_dir}
