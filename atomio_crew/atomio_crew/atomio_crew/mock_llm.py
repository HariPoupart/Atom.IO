"""Offline mock LLM so the crew can run end-to-end without an API key.

It plugs into CrewAI as a real ``BaseLLM``: the agents, tasks, tool calls,
context passing, Pydantic parsing and callbacks all run for real; only the
text that would come from Claude is canned. Tool-using agents (Asset Scraper
and the validation step) really call their tools in mock mode, and the final
validation report is built from the real tool output.
"""
from __future__ import annotations

import json
import re
from collections import defaultdict
from pathlib import Path
from typing import Any

from crewai.llms.base_llm import BaseLLM

from .schemas import TierDesignSet

MOCK_DIR = Path(__file__).parent / "mock_data"
SCRIPTS_ROOT = "Assets/Scripts/AtomIO/"


def canned_tier_design() -> TierDesignSet:
    return TierDesignSet.model_validate_json((MOCK_DIR / "tier_design_set.json").read_text())


def _canned_manifest() -> dict:
    from .tools import FREE_ASSET_CATALOG

    used_for = {
        "vfx": "Core glow, electron trails, energy orbs, enemy death bursts",
        "input": "Left move stick and right aim stick sprites",
        "ui": "HUD health bar, lobby buttons and panels",
        "sprite": "Possible enemy sprites",
        "font": "HUD and lobby text",
        "audio": "Shoot, pickup, evolve and UI sounds",
        "shader": "Bloom/glow on the atom core (2D renderer)",
    }
    assets = []
    for a in FREE_ASSET_CATALOG:
        vendor = "Kenney" if a["name"].startswith("Kenney") else "AssetStore"
        pack = re.sub(r"[^A-Za-z0-9]", "", a["name"].replace("Kenney ", ""))
        local = (f"Packages/{pack}" if a["license"] == "Unity-Package"
                 else f"Assets/ThirdParty/{vendor}/{pack}/")
        assets.append({
            "name": a["name"], "category": a["category"], "source_url": a["source_url"],
            "license": a["license"], "price_usd": 0, "local_path": local,
            "unity_compat_note": ("Plain PNG/OGG files, import as Sprite (2D) / AudioClip. Works in any Unity version."
                                  if vendor == "Kenney" else "First-party / Asset Store package, supports Unity 6."),
            "used_for": used_for.get(a["category"], "General"),
        })
    return {"assets": assets,
            "search_notes": "Queried free_asset_catalog for particles/glow, joystick/touch, ui/hud, font, sfx and "
                            "shader/bloom. All licenses passed license_check; no paid, NC or SA assets included."}


def _canned_review() -> dict:
    return {
        "approved_assets": ["Kenney Particle Pack", "Kenney Mobile Controls", "Kenney UI Pack", "Kenney Fonts",
                            "Kenney Sci-fi Sounds", "Kenney Interface Sounds", "Unity Universal Render Pipeline",
                            "TextMesh Pro (in uGUI)"],
        "rejected_assets": [
            {"name": "Joystick Pack (Fenerax Studios)",
             "reason": "Duplicates our own 60-line uGUI TouchStick; Kenney Mobile Controls already gives the sprites."},
            {"name": "Kenney Space Shooter Redux",
             "reason": "Spaceship art clashes with the abstract Bohr-model style; enemies will be procedural circles."},
        ],
        "implementation_plan": [
            "Create Lobby and Arena scenes; set landscape orientation, URP 2D renderer with Bloom.",
            "Core/AtomFormulas: GDD numbers (HP, slots, evolution cost, failure curve).",
            "Data/TierConfig + TierConfigLoader: read Resources/AtomIO/Tiers/tier_NNN.json with JsonUtility.",
            "Player/PlayerAtom + Player/ElectronOrbit: movement, HP, Bohr shells, aim and release.",
            "Combat/Electron + Combat/Enemy: wild/orbit/released states, damage, chase-and-give-up AI.",
            "Arena/ArenaSpawner + EnergyOrb + ArenaSession: inverse-time spawns, energy, run end.",
            "Controls/TouchStick + PlayerInputBridge: two on-screen sticks, no extra packages.",
            "Meta/MetaProgress + LobbyController: banked energy, evolution with pity, tutorial gating.",
            "UI/ArenaHud: element, HP bar, energy, time, electrons, tutorial hints.",
        ],
        "prefab_scene_notes": (
            "Physics2D layers: Player, Enemy, Electron, Pickup. Disable Electron-Electron, Pickup-Enemy and "
            "Electron-Pickup collisions. Prefabs: Player (Rigidbody2D dynamic, gravity 0, CircleCollider2D, "
            "PlayerAtom, ElectronOrbit, child core and rim SpriteRenderers, LineRenderer for aim), Electron "
            "(kinematic Rigidbody2D, trigger CircleCollider2D, TrailRenderer, Kenney particle sprite), Enemy "
            "(dynamic Rigidbody2D, CircleCollider2D, Enemy, orb prefab reference), EnergyOrb (trigger collider). "
            "Arena scene: ArenaSession, ArenaSpawner, Canvas (Screen Space Overlay, scale with screen size "
            "1920x1080) with left/right TouchStick, ArenaHud, exit button wired to ArenaSession.ExitArena, and an "
            "EventSystem (use InputSystemUIInputModule if the new Input System is active). Lobby scene: "
            "LobbyController with evolve/start buttons. Add both scenes to Build Settings (Lobby first)."
        ),
    }


def _canned_code() -> dict:
    files = []
    for p in sorted((MOCK_DIR / "csharp").rglob("*.cs")):
        rel = p.relative_to(MOCK_DIR / "csharp").as_posix()
        files.append({"path": SCRIPTS_ROOT + rel, "content": p.read_text()})
    lines = ["## Lead Architect: core gameplay scripts", "",
             "Generated against Unity 6.3 LTS. No external packages and no networking.", "",
             "| File | Purpose |", "|---|---|"]
    purpose = {
        "AtomFormulas": "GDD constants and formulas (HP, slots, evolution cost, failure chance, pity).",
        "TierConfig": "Serializable mirror of tier_NNN.json and the spawn/energy/HP curves.",
        "TierConfigLoader": "Loads tier JSON from Resources with fallback; zero network calls.",
        "MetaProgress": "PlayerPrefs save: element, banked energy, pity counter, tutorial stage.",
        "LobbyController": "Lobby UI: evolve with failure chance, forced first evolution, start run.",
        "PlayerAtom": "Player nucleus: movement, HP = 10 x Z, trait multipliers, core colours.",
        "ElectronOrbit": "Bohr shells (2/8/18/32), slot cap = Z, keep >= 1, aim line, release.",
        "Electron": "Wild / orbiting / released states, 10 and 20 damage, pickup.",
        "Enemy": "Chase in sight range, give up at 1.5x range, contact damage, orb drop.",
        "EnergyOrb": "Energy pickup.",
        "ArenaSession": "Run lifecycle, time and energy tracking, return to lobby.",
        "ArenaSpawner": "Inverse-time spawning of enemies, orbs and wild electrons.",
        "TouchStick": "uGUI virtual joystick (move / aim).",
        "PlayerInputBridge": "Connects sticks to the player and orbit.",
        "ArenaHud": "Top-left HUD and tutorial hints.",
    }
    for f in files:
        stem = Path(f["path"]).stem
        lines.append(f"| `{f['path']}` | {purpose.get(stem, '')} |")
    return {"files": files, "changes_md": "\n".join(lines) + "\n"}


def _final(obj: dict | str) -> str:
    body = obj if isinstance(obj, str) else json.dumps(obj)
    return f"Thought: I now have everything I need.\nFinal Answer: {body}"


class MockLLM(BaseLLM):
    """Deterministic stand-in for Claude, keyed on the CrewAI task name."""

    llm_type: str = "mock"

    def __init__(self, **data: Any) -> None:
        super().__init__(**data)
        object.__setattr__(self, "_calls", defaultdict(int))
        object.__setattr__(self, "_usage", {"prompt_tokens": 0, "completion_tokens": 0, "calls": 0})

    # CrewAI hooks ---------------------------------------------------------
    def supports_function_calling(self) -> bool:  # use the text ReAct loop
        return False

    def get_context_window_size(self) -> int:
        return 200_000

    def call(self, messages, tools=None, callbacks=None, available_functions=None,
             from_task=None, from_agent=None, response_model=None, **kwargs) -> str:
        task = getattr(from_task, "name", None) or "unknown"
        self._calls[task] += 1
        n = self._calls[task]
        text = self._respond(task, n, messages)
        prompt = messages if isinstance(messages, str) else "".join(str(m.get("content", "")) for m in messages)
        self._usage["prompt_tokens"] += len(prompt) // 4
        self._usage["completion_tokens"] += len(text) // 4
        self._usage["calls"] += 1
        return text

    def usage(self) -> dict:
        return dict(self._usage)

    # Canned behaviour -----------------------------------------------------
    def _respond(self, task: str, n: int, messages) -> str:
        if task == "scout_assets":
            if n == 1:
                return ("Thought: I should search the free catalogue for every need in the GDD.\n"
                        "Action: free_asset_catalog\n"
                        'Action Input: {"query": "particles glow joystick touch ui hud font sfx shader bloom"}')
            if n == 2:
                return ("Thought: Now verify a license before building the manifest.\n"
                        "Action: license_check\n"
                        'Action Input: {"license_id": "Unity-Asset-Store-EULA"}')
            return _final(_canned_manifest())
        if task == "review_assets_and_plan":
            return _final(_canned_review())
        if task == "design_tiers":
            if n == 1:
                return ("Thought: Let me look at the element groups first.\n"
                        "Action: element_groups\n"
                        'Action Input: {"_": ""}')
            return _final(canned_tier_design().model_dump())
        if task == "implement_core_scripts":
            if n == 1:
                return ("Thought: I need the exact tier JSON the Level Designer's design produces.\n"
                        "Action: tier_json_contract\n"
                        'Action Input: {"atomic_number": "26"}')
            return _final(_canned_code())
        if task == "validate_outputs":
            if n == 1:
                return ("Thought: I must run the validation suite.\n"
                        "Action: run_validation_suite\n"
                        'Action Input: {"scope": "all"}')
            return _final(self._report_from_observation(messages))
        return _final("{}")

    @staticmethod
    def _report_from_observation(messages) -> dict:
        blob = messages if isinstance(messages, str) else "\n".join(str(m.get("content", "")) for m in messages)
        found = None
        for m in re.finditer(r'\{"suite": "atomio-validation".*?\]\}', blob, flags=re.S):
            found = m.group(0)
        result = json.loads(found) if found else {"passed": False, "errors": ["validation suite output not found"],
                                                  "warnings": []}
        return {
            "approved_for_gate": bool(result.get("passed")),
            "errors": result.get("errors", []),
            "warnings": result.get("warnings", []),
            "notes": ("Open the Unity project, let scripts compile, then check: (1) tutorial run as H, "
                      "(2) forced evolve to He, (3) throw/pickup lesson, (4) tier 26/79/118 core looks, "
                      "(5) profiler on a 2 GB Android device with 100+ orbiting electrons."),
        }
