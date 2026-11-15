"""Gap detection: compare the GDD requirements against the scanned codebase.

Pass 1 (code, always): check each requirement's evidence (types, Type.Member,
Resources paths). All present -> done; some -> partial; none -> missing.
Pass 2 (LLM judge, live mode only, partial requirements only): read the
relevant files against the acceptance criteria and confirm the status.
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass, field

from .scanner import CodeIndex


@dataclass
class Gap:
    req: dict
    status: str                    # done | partial | missing
    found: list[str] = field(default_factory=list)
    missing: list[str] = field(default_factory=list)
    reason: str = ""

    @property
    def id(self) -> str:
        return self.req["id"]


def evidence_check(req: dict, ix: CodeIndex) -> Gap:
    ev = req.get("evidence", {})
    checks = ([(t, ix.has_type(t)) for t in ev.get("types", [])]
              + [(m, ix.has_member(m)) for m in ev.get("members", [])]
              + [(f"loads {p}", ix.loads_path(p)) for p in ev.get("loads", [])])
    found = [name for name, ok in checks if ok]
    missing = [name for name, ok in checks if not ok]
    if not checks:
        status = "done"
    elif not missing:
        status = "done"
    elif found:
        status = "partial"
    else:
        status = "missing"
    reason = "all evidence found" if status == "done" else f"missing: {', '.join(missing)}"
    return Gap(req, status, found, missing, reason)


JUDGE_SYSTEM = ("You are the Atom.IO Gap Judge. Decide whether a GDD requirement is already implemented in "
                "the given Unity C# files. Output ONLY JSON: {\"status\": \"done\"|\"partial\"|\"missing\", "
                "\"reason\": \"one sentence\"}.")


def llm_judge(gap: Gap, ix: CodeIndex, llm) -> Gap:
    """Second opinion for 'partial' gaps: is it really unfinished?"""
    names = set(gap.req["evidence"].get("types", [])) | {m.split(".")[0] for m in gap.req["evidence"].get("members", [])}
    files = [ix.type_file(n) for n in names if ix.type_file(n)]
    snippet = "\n\n".join(f"// {f.path}\n{f.text[:6000]}" for f in files[:3])
    user = (f"REQUIREMENT {gap.id}: {gap.req['title']} (GDD {gap.req['gdd_ref']})\n{gap.req['description']}\n"
            f"ACCEPTANCE: {gap.req['acceptance']}\nEVIDENCE FOUND: {gap.found}\nEVIDENCE MISSING: {gap.missing}\n\n"
            f"FILES:\n{snippet}")
    try:
        text = llm.call([{"role": "system", "content": JUDGE_SYSTEM}, {"role": "user", "content": user}])
        m = re.search(r"\{.*\}", text, re.S)
        verdict = json.loads(m.group(0)) if m else {}
        if verdict.get("status") in ("done", "partial", "missing"):
            gap.status = verdict["status"]
            gap.reason = f"judge: {verdict.get('reason', '')}"
    except Exception as e:  # the judge is advisory; keep the evidence verdict
        gap.reason += f" (judge unavailable: {type(e).__name__})"
    return gap


def detect(requirements: list[dict], ix: CodeIndex, judge_llm=None) -> list[Gap]:
    gaps = [evidence_check(r, ix) for r in requirements]
    if judge_llm is not None:
        gaps = [llm_judge(g, ix, judge_llm) if g.status == "partial" else g for g in gaps]
    return gaps
