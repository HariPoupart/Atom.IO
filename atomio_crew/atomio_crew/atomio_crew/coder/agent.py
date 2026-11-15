"""Assignment 5: goal-oriented coding agent for Atom.IO.

    loop:
      scan the Unity scripts           (scanner.py, plain code)
      detect gaps vs GDD requirements  (gaps.py: evidence check + LLM judge for partial ones)
      score the open gaps              (priority.py: transparent formula, blocked if deps missing)
      stop if: nothing missing | everything left is blocked/failed | max iterations | budget
      write code for the top gap       (writer.py, Claude; up to 3 attempts)
      check it                         (static checks + compile against Unity: compiler.py)
      apply it to the project          (backup originals, write files)
      -> re-scan (the new file may finish this gap and unblock others)
"""
from __future__ import annotations

import difflib
import json
import os
import shutil
from datetime import datetime
from pathlib import Path

from .. import budget
from . import writer as W
from .compiler import Compiler
from .gaps import detect
from .priority import score_gaps
from .scanner import scan

DATA = Path(__file__).resolve().parents[2] / "data"
MAX_ATTEMPTS = 3
TOKEN_CAP = int(os.getenv("ATOMIO_CODER_TOKEN_CAP", "800000"))
USD_CAP = float(os.getenv("ATOMIO_CODER_USD_CAP", "8"))


def load_requirements() -> list[dict]:
    return json.loads((DATA / "requirements.json").read_text(encoding="utf-8"))["requirements"]


EXTRACT_SYSTEM = """You turn a Game Design Document into a checklist of implementable features for a Unity \
coding agent. Output ONLY JSON: {"requirements": [...]} using exactly the same fields as the example. Every \
requirement needs: id (R01..), title, gdd_ref (section ids), category (core|ui|retention|polish), description, \
acceptance (list), evidence {types, members ("Type.Member"), loads (Resources paths)} naming the C# types a correct \
implementation would contain, depends_on (ids), data_files (paths under Assets/Resources that it loads), touches \
(existing types it would hook into), effort (1-3), player_visible (bool). Keep the ids and evidence of features \
that already exist in the current list unless the GDD changed them."""


def extract_requirements(mock: bool, out_path: Path, say=print) -> Path:
    """Ask Claude to (re)derive the requirement list from the GDD; writes a PROPOSED file for review."""
    current = (DATA / "requirements.json").read_text(encoding="utf-8")
    if mock:
        proposed = json.loads(current)
    else:
        from crewai import LLM
        from ..models import pick
        llm = LLM(model=pick("sonnet", "ATOMIO_MODEL_CODER"), max_tokens=16000)
        gdd = (DATA / "gdd_full.md").read_text(encoding="utf-8")
        raw = llm.call([{"role": "system", "content": EXTRACT_SYSTEM},
                        {"role": "user", "content": f"GDD:\n{gdd}\n\nCURRENT LIST (example format):\n{current}"}])
        proposed = W.parse_json_object(raw)
    out_path.write_text(json.dumps(proposed, indent=1), encoding="utf-8")
    old_ids = {r["id"]: r["title"] for r in json.loads(current)["requirements"]}
    new_ids = {r["id"]: r["title"] for r in proposed.get("requirements", [])}
    say(f"Proposed {len(new_ids)} requirements -> {out_path}")
    for rid in sorted(set(old_ids) | set(new_ids)):
        if old_ids.get(rid) != new_ids.get(rid):
            say(f"  {rid}: {old_ids.get(rid, '(new)')}  ->  {new_ids.get(rid, '(removed)')}")
    say("Review it, then copy it over data/requirements.json to use it.")
    return out_path


def make_sandbox(unity_root: Path, run_dir: Path) -> Path:
    """Copy just what the agent reads/writes, so a dry demo never touches the real project."""
    sb = run_dir / "sandbox"
    for rel in ("Assets/Scripts", "Assets/Resources", "Assets/AtomIO/Editor"):
        src = unity_root / rel
        if src.exists():
            shutil.copytree(src, sb / rel, ignore=shutil.ignore_patterns("*.meta"))
    for rel in ("Assembly-CSharp.csproj", "ProjectSettings/ProjectVersion.txt"):
        if (unity_root / rel).exists():
            (sb / rel).parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(unity_root / rel, sb / rel)
    return sb


def _tie_break(a, b) -> str:
    if a.score != b.score:
        return ""
    for term, better in (("importance", "higher GDD importance"), ("unblocks", "unblocks more features"),
                         ("effort", "less effort")):
        if a.terms[term] != b.terms[term]:
            return better
    return "earlier in the GDD"


def _gap_table(gaps, scored) -> list[dict]:
    by_id = {s.gap.id: s for s in scored}
    rows = []
    for g in gaps:
        s = by_id.get(g.id)
        rows.append({"id": g.id, "title": g.req["title"], "status": g.status, "reason": g.reason,
                     "score": s.score if s else None, "formula": s.formula() if s else "",
                     "blocked_by": s.blocked_by if s else []})
    return rows


def run(unity_root: Path, mock: bool, dry_run: bool, max_iterations: int, sandbox: bool, out_root: Path,
        report_dir: Path, only: str | None = None, say=print) -> dict:
    unity_root = Path(unity_root).resolve()
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    run_dir = Path(out_root) / "agent_runs" / f"run_{stamp}"
    run_dir.mkdir(parents=True, exist_ok=True)
    work = make_sandbox(unity_root, run_dir) if sandbox else unity_root
    say(f"Project: {unity_root}" + (f"\nSandbox: {work}  (the real project is not modified)" if sandbox else ""))

    reqs = load_requirements()
    reqs_by_id = {r["id"]: r for r in reqs}
    compiler = Compiler(work, refs_root=unity_root)
    say(f"Compile check: {compiler.backend}" + (f"  ({compiler.note})" if compiler.backend == "none" else ""))

    if mock:
        from .mock import CoderMockLLM
        llm, judge = CoderMockLLM(model="mock/coder"), None
        llm.plant_bug = compiler.backend != "none"  # only plant the demo bug if something can catch it
    else:
        from crewai import LLM
        from ..models import pick
        llm = LLM(model=pick("sonnet", "ATOMIO_MODEL_CODER"), max_tokens=32000)
        judge = LLM(model=pick("sonnet", "ATOMIO_MODEL_JUDGE"), max_tokens=1000)

    log = {"mode": "mock" if mock else "live", "project": str(unity_root), "sandbox": sandbox,
           "compiler": compiler.backend, "iterations": [], "stop_reason": ""}
    skip: set[str] = set()
    expected_done: str | None = None

    for it in range(1, max_iterations + 2):
        ix = scan(work)
        gaps = detect(reqs, ix, judge)
        scored = score_gaps(gaps, ix, skip)
        snap = {"iteration": it, "gaps": _gap_table(gaps, scored)}
        log["iterations"].append(snap)
        open_gaps = [g for g in gaps if g.status != "done"]
        say(f"\n[scan {it}] {len(gaps) - len(open_gaps)}/{len(gaps)} requirements done; open: "
            + (", ".join(f"{g.id}({g.status})" for g in open_gaps) or "none"))

        if expected_done and any(g.id == expected_done for g in open_gaps):
            say(f"  {expected_done} was written but its evidence is still missing: marking it failed")
            skip.add(expected_done)
            snap["note"] = f"{expected_done} written but evidence still missing"
            scored = score_gaps(gaps, ix, skip)
        expected_done = None

        for s in scored:
            say(f"    {s.gap.id} {s.gap.req['title']:<32} score {s.formula()}"
                + (f"  BLOCKED by {s.blocked_by}" if s.blocked_by else ""))

        if not open_gaps:
            log["stop_reason"] = "all requirements met"
            break
        eligible = [s for s in scored if s.eligible and (only is None or s.gap.id == only)]
        if not eligible:
            log["stop_reason"] = "remaining gaps are blocked or failed" if not only else f"{only} not eligible"
            break
        if it > max_iterations:
            log["stop_reason"] = f"max iterations ({max_iterations}) reached"
            break

        pick_ = eligible[0]
        req = pick_.gap.req
        snap["picked"] = {"id": req["id"], "title": req["title"], "score": pick_.score,
                          "formula": pick_.formula(), "terms": pick_.terms,
                          "runner_up": ({"id": eligible[1].gap.id, "score": eligible[1].score,
                                         "tie_break": _tie_break(pick_, eligible[1])}
                                        if len(eligible) > 1 else None)}
        say(f"  -> building {req['id']} {req['title']} (score {pick_.score})")
        if dry_run:
            log["stop_reason"] = "dry run: plan only"
            break

        attempts, feedback, previous, result = [], None, None, None
        for attempt in range(1, MAX_ATTEMPTS + 1):
            raw = None
            try:
                raw = llm.call(W.build_messages(req, ix, reqs_by_id, feedback, previous))
                reply = W.parse_reply(raw)
                errors = W.static_checks(reply["files"], req, ix)
                comp = {"backend": compiler.backend, "ok": True, "errors": [], "warning": ""}
                if not errors:
                    comp = compiler.check({f["path"]: f["content"] for f in reply["files"]})
                    errors = comp["errors"]
            except Exception as e:  # malformed reply, API error: count as a failed attempt
                reply, comp, errors = None, {}, [f"{type(e).__name__}: {str(e)[:300]}"]
            attempts.append({"attempt": attempt, "errors": errors, "compile_backend": comp.get("backend"),
                             "compile_warning": comp.get("warning", "")})
            say(f"     attempt {attempt}: " + ("checks passed" if not errors else f"{len(errors)} problem(s): {errors[0][:120]}"))
            if not errors and reply:
                result = reply
                break
            feedback = errors
            previous = json.dumps(reply) if reply else (raw if isinstance(raw, str) else None)
            try:
                usage_total = budget.usage_of(llm)
                if usage_total["prompt_tokens"] + usage_total["completion_tokens"] > TOKEN_CAP:
                    break
            except Exception:
                pass

        snap["attempts"] = attempts
        if result is None:
            say(f"  {req['id']} failed after {len(attempts)} attempt(s): skipping it")
            skip.add(req["id"])
            snap["outcome"] = "failed"
            continue

        written = apply(result["files"], work, run_dir, it)
        snap.update({"outcome": "written", "files": written, "summary": result.get("summary", ""),
                     "manual_steps": result.get("manual_steps", [])})
        expected_done = req["id"]
        say(f"  wrote {len(written)} file(s): " + ", ".join(f"{w['path']} ({w['action']})" for w in written))

        u = budget.usage_of(llm)
        if u["prompt_tokens"] + u["completion_tokens"] > TOKEN_CAP or budget.cost_usd(u) > USD_CAP:
            log["stop_reason"] = "budget cap reached"
            break

    u = budget.usage_of(llm)
    log["usage"] = {**u, "usd": round(budget.cost_usd(u), 3)}
    write_report(log, run_dir, Path(report_dir))
    return {"log": log, "run_dir": run_dir}


def apply(files: list[dict], work: Path, run_dir: Path, it: int) -> list[dict]:
    written = []
    diff_lines = []
    for f in files:
        dest = work / f["path"]
        old = dest.read_text(encoding="utf-8") if dest.exists() else ""
        if dest.exists():
            backup = run_dir / "backup" / f"iter{it}" / f["path"]
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(dest, backup)
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text(f["content"], encoding="utf-8")
        added = sum(1 for _ in f["content"].splitlines())
        written.append({"path": f["path"], "action": "modified" if old else "created", "lines": added})
        diff_lines += difflib.unified_diff(old.splitlines(), f["content"].splitlines(),
                                           f"a/{f['path']}", f"b/{f['path']}", lineterm="")
    (run_dir / f"iter{it}.diff").write_text("\n".join(diff_lines), encoding="utf-8")
    return written


def write_report(log: dict, run_dir: Path, report_dir: Path) -> None:
    L = [f"# Coding agent report ({log['mode']} run{', sandbox' if log['sandbox'] else ''})", "",
         f"- Project: `{log['project']}`", f"- Compile check: **{log['compiler']}**",
         f"- Stop reason: **{log['stop_reason']}**",
         f"- Tokens: {log['usage']['prompt_tokens'] + log['usage']['completion_tokens']:,} (~${log['usage']['usd']})", ""]
    for snap in log["iterations"]:
        L.append(f"## Scan {snap['iteration']}")
        if snap.get("note"):
            L.append(f"> {snap['note']}")
        L += ["| Req | Feature | Status | Priority score | Notes |", "|---|---|---|---|---|"]
        for r in snap["gaps"]:
            if r["status"] == "done":
                continue
            note = f"BLOCKED by {', '.join(r['blocked_by'])}" if r["blocked_by"] else r["reason"]
            L.append(f"| {r['id']} | {r['title']} | {r['status']} | {r['formula'] or '-'} | {note} |")
        done = [r["id"] for r in snap["gaps"] if r["status"] == "done"]
        L.append(f"\nDone: {', '.join(done) or 'none'}\n")
        if "picked" in snap:
            p = snap["picked"]
            ru = p.get("runner_up")
            L.append(f"**Picked {p['id']} {p['title']}**: score {p['formula']}"
                     + (f" (runner-up {ru['id']} at {ru['score']}"
                        + (f"; tie broken because it {ru['tie_break']}" if ru.get("tie_break") else "") + ")"
                        if ru else ""))
        for a in snap.get("attempts", []):
            L.append(f"- attempt {a['attempt']}: " + ("passed checks" if not a["errors"] else
                     "rejected: " + "; ".join(e[:160] for e in a["errors"][:3]))
                     + (f" _(warning: {a['compile_warning'][:160]})_" if a.get("compile_warning") else ""))
        if snap.get("outcome") == "written":
            L.append(f"- wrote: " + ", ".join(f"`{w['path']}` ({w['action']})" for w in snap["files"]))
            if snap.get("summary"):
                L.append(f"- summary: {snap['summary']}")
        elif snap.get("outcome") == "failed":
            L.append("- **failed**: skipped for the rest of the run")
        L.append("")
    text = "\n".join(L) + "\n"
    (run_dir / "AGENT_REPORT.md").write_text(text, encoding="utf-8")
    (run_dir / "agent_log.json").write_text(json.dumps(log, indent=1), encoding="utf-8")
    report_dir.mkdir(parents=True, exist_ok=True)
    (report_dir / "AGENT_REPORT.md").write_text(text, encoding="utf-8")
    (report_dir / "agent_log.json").write_text(json.dumps(log, indent=1), encoding="utf-8")
