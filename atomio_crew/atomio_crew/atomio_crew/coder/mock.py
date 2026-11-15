"""Offline stand-in for Claude so the coding agent runs end to end without an API key.

The mock Writer returns reference implementations stored in mock_data/coder/<REQ>/ and,
where a feature needs a hook in an existing script, patches the CURRENT text of that
script (taken from the prompt, exactly as the real model would see it). To show the
fix loop, the first attempt at R17 uses a constant that doesn't exist
(AtomFormulas.FinalAtomicNumber); the compile check rejects it and the retry fixes it.
The judge is never needed in mock mode. Scanning, scoring, checks, compiling, writing
and re-scanning all run for real.
"""
from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

from crewai.llms.base_llm import BaseLLM

DATA = Path(__file__).resolve().parents[1] / "mock_data" / "coder"
ROOT = "Assets/Scripts/AtomIO/"

# (file, anchor, replacement) patches applied to existing scripts for R16 (all additive).
R16_PATCHES = {
    "Assets/Scripts/AtomIO/Player/PlayerAtom.cs": [
        ("        private float lastHitTime = -999f;\n",
         "        private float lastHitTime = -999f;\n        private float shieldUntil = -1f;\n"),
        ("if (IsDead || Time.time - lastHitTime < contactDamageCooldown)",
         "if (IsDead || IsShielded || Time.time - lastHitTime < contactDamageCooldown)"),
        ("        private void ApplyVisuals(",
         "        /// <summary>True while a group ability (noble gas Full Shell) blocks contact damage.</summary>\n"
         "        public bool IsShielded => Time.time < shieldUntil;\n\n"
         "        /// <summary>Restores HP from a group ability, never above the maximum.</summary>\n"
         "        public void Heal(int amount)\n        {\n            if (IsDead || amount <= 0)\n            {\n"
         "                return;\n            }\n\n            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);\n"
         "            HealthChanged?.Invoke(CurrentHp, MaxHp);\n        }\n\n"
         "        /// <summary>Blocks contact damage for a few seconds (temporary, never permanent).</summary>\n"
         "        public void Shield(float seconds)\n        {\n"
         "            shieldUntil = Mathf.Max(shieldUntil, Time.time + Mathf.Max(0f, seconds));\n        }\n\n"
         "        private void ApplyVisuals("),
    ],
    "Assets/Scripts/AtomIO/Combat/Enemy.cs": [
        ("using AtomIO.Arena;\n", "using AtomIO.Arena;\nusing AtomIO.Core;\n"),
        ("                Killed?.Invoke(this);\n",
         "                GameplayHooks.RaiseEnemyKilled(this);\n                Killed?.Invoke(this);\n"),
    ],
    "Assets/Scripts/AtomIO/Combat/Electron.cs": [
        ("using AtomIO.Arena;\n", "using AtomIO.Arena;\nusing AtomIO.Core;\n"),
        ("enemy.TakeDamage(Mathf.RoundToInt(releaseDamage * damageMult));",
         "enemy.TakeDamage(Mathf.RoundToInt(releaseDamage * damageMult * GameplayHooks.DamageMultFor(this)));"),
        ("enemy.TryTakeOrbitHit(Mathf.RoundToInt(orbitDamage * damageMult));",
         "enemy.TryTakeOrbitHit(Mathf.RoundToInt(orbitDamage * damageMult));\n"
         "                GameplayHooks.RaiseOrbitHit(enemy);"),
        ("            rb.linearVelocity = direction.normalized * releaseSpeed;\n            SetTrail(true);\n",
         "            rb.linearVelocity = direction.normalized * releaseSpeed;\n            SetTrail(true);\n"
         "            GameplayHooks.RaiseReleased(this);\n"),
    ],
}

SUMMARIES = {
    "R16": ("Loads group_abilities.json and runs the current group's ability in the arena: a controller that "
            "attaches itself to ArenaSession, switches ability on evolution, and implements all six triggers "
            "and effects. Adds small additive hooks: GameplayHooks events raised by Electron and Enemy, and "
            "PlayerAtom.Heal / Shield."),
    "R17": "Lobby card at the bottom of the lobby: the current element's title and flavor, and the next element.",
    "R18": "HUD line under the electron count showing the active ability and its cooldown; flashes when it fires.",
    "R15": "MENU button left of EXIT; pauses the arena with Resume, Sound on/off (saved) and Exit to lobby.",
}


def _current_files(prompt: str) -> dict[str, str]:
    """Existing scripts as the Writer saw them in the prompt (RELEVANT FILES section)."""
    files = {}
    for m in re.finditer(r"// ===== (\S+) =====\n(.*?)(?=\n// ===== |\nYOUR PREVIOUS ATTEMPT|\Z)", prompt, re.S):
        files[m.group(1)] = m.group(2)
    return files


def reference_files(req_id: str, prompt: str, attempt: int) -> list[dict]:
    out = []
    base = DATA / req_id
    for p in sorted(base.rglob("*.cs")):
        text = p.read_text(encoding="utf-8")
        if req_id == "R17" and attempt >= 2:
            text = text.replace("AtomFormulas.FinalAtomicNumber", "AtomFormulas.MaxAtomicNumber")
        out.append({"path": ROOT + p.relative_to(base).as_posix(), "content": text})
    current = _current_files(prompt)
    for path, patches in (R16_PATCHES.items() if req_id == "R16" else []):
        text = current.get(path)
        if text is None:
            continue
        for anchor, repl in patches:
            text = text.replace(anchor, repl, 1)
        out.append({"path": path, "content": text})
    return out


class CoderMockLLM(BaseLLM):
    llm_type: str = "mock"

    def __init__(self, **data: Any) -> None:
        super().__init__(**data)
        object.__setattr__(self, "_usage", {"prompt_tokens": 0, "completion_tokens": 0, "calls": 0})
        object.__setattr__(self, "plant_bug", True)

    def supports_function_calling(self) -> bool:
        return False

    def get_context_window_size(self) -> int:
        return 200_000

    def usage(self) -> dict:
        return dict(self._usage)

    def call(self, messages, tools=None, callbacks=None, available_functions=None,
             from_task=None, from_agent=None, response_model=None, **kwargs) -> str:
        prompt = "".join(str(m.get("content", "")) for m in messages)
        m = re.search(r"FEATURE (R\d+):", prompt)
        req_id = m.group(1) if m else ""
        attempt = 2 if ("YOUR PREVIOUS ATTEMPT FAILED" in prompt or not self.plant_bug) else 1
        files = reference_files(req_id, prompt, attempt)
        text = json.dumps({"files": files, "summary": SUMMARIES.get(req_id, ""),
                           "manual_steps": ["None: the feature sets itself up at runtime."]})
        self._usage["prompt_tokens"] += len(prompt) // 4
        self._usage["completion_tokens"] += len(text) // 4
        self._usage["calls"] += 1
        return text
