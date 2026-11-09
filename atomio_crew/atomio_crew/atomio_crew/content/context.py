"""Load the Atom.IO GDD and pick the sections each content type needs.

The GDD is split on its section headings (``## 1.3.1 ...``). Each content spec
lists the section ids it depends on, and only those sections are put in front
of the Writer and the Critic. That keeps prompts small and makes it explicit
which part of the design every piece of content is grounded in.
"""
from __future__ import annotations

import json
import re
from functools import lru_cache
from pathlib import Path

from .. import gdd_rules as R

DATA = Path(__file__).resolve().parents[2] / "data"


@lru_cache(maxsize=1)
def gdd_sections() -> dict[str, str]:
    text = (DATA / "gdd_full.md").read_text(encoding="utf-8")
    sections: dict[str, str] = {}
    for block in re.split(r"\n(?=## )", text):
        m = re.match(r"## ([\d.]+) ", block)
        if m:
            sections[m.group(1)] = block.strip()
    return sections


def gdd_excerpt(section_ids: list[str]) -> str:
    secs = gdd_sections()
    missing = [s for s in section_ids if s not in secs]
    if missing:
        raise KeyError(f"GDD sections not found: {missing}")
    return "\n\n".join(secs[s] for s in section_ids)


@lru_cache(maxsize=1)
def style_guide() -> str:
    return (DATA / "style_guide.md").read_text(encoding="utf-8").strip()


def element_table() -> str:
    """Compact fact table so the Writer never has to guess a symbol or group."""
    return "\n".join(f"{e['atomic_number']}|{e['symbol']}|{e['name']}|{e['group']}" for e in R.all_elements())


def level_designer_traits() -> str:
    """Traits from the Assignment 3 crew, so new abilities complement rather than duplicate them."""
    root = DATA.parent
    for p in (root / "sample_output" / "03_tier_design_set.json",
              root / "atomio_crew" / "mock_data" / "tier_design_set.json"):
        if p.exists():
            d = json.loads(p.read_text(encoding="utf-8"))
            return "\n".join(f"{g['group']}: {g['trait']['name']} ({g['trait']['description']})"
                             for g in d.get("group_designs", []))
    return "(none)"
