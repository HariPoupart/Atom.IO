"""Offline stand-in for Claude so the content pipeline runs without an API key.

Writer: the first draft is the reference content with realistic mistakes
planted in it (wrong facts, a wrong GDD number, invented lore, a broken
placeholder, an over-powered ability...). When it receives critic feedback it
returns the corrected content. Critic: flags the planted problems that code
cannot detect (facts, contradictions, lore) by actually reading the draft.
The rule checks, revision loop, logging and outputs all run for real.
"""
from __future__ import annotations

import copy
import json
import re
from pathlib import Path
from typing import Any

from crewai.llms.base_llm import BaseLLM

DATA = Path(__file__).resolve().parents[1] / "mock_data" / "content"


def reference(key: str) -> dict:
    return json.loads((DATA / f"{key}.json").read_text(encoding="utf-8"))


def flawed(key: str) -> dict:
    """The first draft: reference content + mistakes a real LLM plausibly makes."""
    d = copy.deepcopy(reference(key))
    if key == "element_cards":
        cards = {c["atomic_number"]: c for c in d["cards"]}
        cards[2]["flavor"] = "The lightest element in the universe. Its full shell shrugs off every fight."
        cards[14]["flavor"] = "Chosen by the Proton King to guard the realm of microchips."
        cards[50]["flavor"] = ("Bend a tin bar and it cries with a crackle that metalworkers have heard for "
                               "centuries, and it is still used in solder today.")
        cards[79]["symbol"] = "Ag"
        d["cards"] = [c for c in d["cards"] if c["atomic_number"] != 113]
    elif key == "ui_text":
        d["loading_tips"][1] = "Evolving always costs 50 energy, so save up."
        d["loading_tips"][0] = "Each evolution adds 25 HP per proton."
        d["tutorial_run2"][3] = "Out of spares? Throw your last one to finish off an enemy!"
        d["evolve_fail"][1] = "Unstable! Evolution failed. Try again soon."
    elif key == "group_abilities":
        ab = {a["group"]: a for a in d["abilities"]}
        ab["alkali_metal"]["value"] = 2.0
        ab["noble_gas"]["description"] = "Your full shell makes you invincible while below 30% HP."
        ab["halogen"]["chemistry_basis"] = "Halogens have a full outer shell"
        d["abilities"] = [a for a in d["abilities"] if a["group"] != "unknown_properties"]
    return d


# Problems only a reader (not code) can catch: (text in draft, issue)
CRITIC_FINDINGS = {
    "element_cards": [
        ("The lightest element in the universe", {
            "severity": "blocker", "item": "card Z=2 (He)", "gdd_ref": "style guide rule 4",
            "problem": "False fact: hydrogen, not helium, is the lightest element (and it is the player's Z=1).",
            "fix": "Use a true helium fact, e.g. first full shell / lifts balloons."}),
    ],
    "ui_text": [
        ("always costs 50 energy", {
            "severity": "blocker", "item": "loading_tips[1]", "gdd_ref": "1.3.3",
            "problem": "Contradicts the GDD: evolving costs 50 + 3 x current atomic number, not a flat 50.",
            "fix": "State the real formula: 50 + 3 per proton you already have."}),
        ("Throw your last one", {
            "severity": "blocker", "item": "tutorial_run2[3]", "gdd_ref": "1.4.2 balance note",
            "problem": "Contradicts the electron rule: the last electron always stays in orbit.",
            "fix": "Teach that the last electron can't be thrown and protects the core."}),
    ],
    "group_abilities": [
        ("Halogens have a full outer shell", {
            "severity": "blocker", "item": "ability 'halogen'", "gdd_ref": "style guide rule 4",
            "problem": "Wrong chemistry: halogens are ONE electron short of a full shell (that's noble gases).",
            "fix": "Base it on halogens grabbing electrons eagerly."}),
    ],
}


class ContentMockLLM(BaseLLM):
    llm_type: str = "mock"

    def __init__(self, role: str = "writer", **data: Any) -> None:
        super().__init__(**data)
        object.__setattr__(self, "_role", role)
        object.__setattr__(self, "_usage", {"prompt_tokens": 0, "completion_tokens": 0, "calls": 0})

    def supports_function_calling(self) -> bool:
        return False

    def get_context_window_size(self) -> int:
        return 200_000

    def usage(self) -> dict:
        return dict(self._usage)

    def call(self, messages, tools=None, callbacks=None, available_functions=None,
             from_task=None, from_agent=None, response_model=None, **kwargs) -> str:
        prompt = "".join(str(m.get("content", "")) for m in messages)
        key = re.search(r"CONTENT_KEY: (\w+)", prompt).group(1)
        if self._role == "writer":
            out = reference(key) if "PREVIOUS DRAFT" in prompt else flawed(key)
            text = json.dumps(out, ensure_ascii=False)
        else:
            draft = prompt.split("DRAFT TO REVIEW:", 1)[1]
            issues = [i for needle, i in CRITIC_FINDINGS.get(key, []) if needle in draft]
            text = json.dumps({
                "verdict": "revise" if issues else "pass",
                "voice_score": 3 if issues else 5,
                "summary": (f"Found {len(issues)} problem(s) the rule checker can't see." if issues else
                            "Consistent with the GDD, factually sound, and reads like Atom.IO."),
                "issues": issues,
            })
        self._usage["prompt_tokens"] += len(prompt) // 4
        self._usage["completion_tokens"] += len(text) // 4
        self._usage["calls"] += 1
        return text
