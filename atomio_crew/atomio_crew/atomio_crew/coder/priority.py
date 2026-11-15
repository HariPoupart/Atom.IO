"""Priority scoring: which gap should the agent build next?

    score = 3 x GDD importance   (core 3, ui 2, retention 2, polish 1)
          + 2 x unblocks         (other open gaps that depend on this one)
          + 2 x player impact    (visible in a normal run)
          + 1 x assets ready     (its data files already exist in Resources)
          - 1 x effort           (1 small .. 3 large)
    A gap whose dependencies are not done is BLOCKED and not eligible.
    Ties: higher importance, then more unblocks, then lower effort, then GDD order.
Every term is kept so the report can show exactly why a feature was picked.
"""
from __future__ import annotations

from dataclasses import dataclass

from .gaps import Gap
from .scanner import CodeIndex

IMPORTANCE = {"core": 3, "ui": 2, "retention": 2, "polish": 1}
WEIGHTS = {"importance": 3, "unblocks": 2, "impact": 2, "assets": 1, "effort": -1}


@dataclass
class Scored:
    gap: Gap
    score: int
    terms: dict
    blocked_by: list[str]

    @property
    def eligible(self) -> bool:
        return not self.blocked_by

    def formula(self) -> str:
        t = self.terms
        return (f"3x{t['importance']} + 2x{t['unblocks']} + 2x{t['impact']} + 1x{t['assets']} "
                f"- 1x{t['effort']} = {self.score}")


def score_gaps(gaps: list[Gap], ix: CodeIndex, skip: set[str] = frozenset()) -> list[Scored]:
    status = {g.id: g.status for g in gaps}
    open_gaps = [g for g in gaps if g.status != "done" and g.id not in skip]
    open_ids = {g.id for g in open_gaps}
    scored = []
    for g in open_gaps:
        r = g.req
        unblocks = sum(1 for o in open_gaps if g.id in o.req.get("depends_on", []))
        data = r.get("data_files", [])
        terms = {
            "importance": IMPORTANCE.get(r.get("category", "polish"), 1),
            "unblocks": unblocks,
            "impact": 1 if r.get("player_visible") else 0,
            "assets": 1 if data and all(ix.has_resource(d) for d in data) else 0,
            "effort": int(r.get("effort", 2)),
        }
        score = sum(WEIGHTS[k] * v for k, v in terms.items())
        blocked_by = [d for d in r.get("depends_on", []) if status.get(d) != "done" or d in open_ids]
        scored.append(Scored(g, score, terms, blocked_by))
    order = {g.id: i for i, g in enumerate(gaps)}
    scored.sort(key=lambda s: (not s.eligible, -s.score, -s.terms["importance"], -s.terms["unblocks"],
                               s.terms["effort"], order[s.gap.id]))
    return scored
