"""Tools the agents can call, plus the shared run state they read from."""
from __future__ import annotations

import json
import os
from dataclasses import dataclass, field
from typing import Any

from crewai.tools import tool

from . import gdd_rules as R
from . import validators

# --------------------------------------------------------------------------- shared state
@dataclass
class RunState:
    """Task outputs are stored here by task callbacks so tools can read them
    without the LLM having to re-paste large JSON into a tool call."""
    manifest: Any = None
    review: Any = None
    design: Any = None
    code: Any = None
    validation_report: Any = None
    tier_files: dict | None = None
    tier_index: dict | None = None
    extras: dict = field(default_factory=dict)


STATE = RunState()


# --------------------------------------------------------------------------- Asset Scraper tools
# Curated, offline seed catalogue of free Unity-compatible sources. Every entry
# is a real, free pack. The scraper agent filters it against the GDD; with a
# SERPER_API_KEY set it can also search the live web (see crew.py).
FREE_ASSET_CATALOG = [
    {"name": "Kenney Particle Pack", "category": "vfx", "source_url": "https://kenney.nl/assets/particle-pack",
     "license": "CC0-1.0", "price_usd": 0, "tags": ["particles", "glow", "sparks", "energy", "orb", "trail"]},
    {"name": "Wenrexa Magic Sphere Sprites", "category": "sprite", "source_url": "https://wenrexa.itch.io/item-magics",
     "license": "CC0-1.0", "price_usd": 0,
     "tags": ["sphere", "orb", "magic", "glow", "electron", "pickup", "shaded", "icons", "colored"],
     "notes": "Pre-shaded magic sphere sprites, PNG 16-512 px, pay what you want (0 allowed), "
              "author states commercial use is fine and the devlog says CC0."},
    {"name": "Basic Glowy Shapes (Sunset Arena)", "category": "sprite",
     "source_url": "https://unprecedented.itch.io/basic-glowy-shapes-assets-from-sunset-arena",
     "license": "CC0-1.0", "price_usd": 0,
     "tags": ["glow", "shapes", "neon", "circle", "electron", "arena", "minimal"],
     "notes": "Tiny (27 kB) set of glowy shapes from an arena game; author: CC0, 'use them however you like'."},
    {"name": "Basic 2D Shapes (Reactorcore)", "category": "sprite",
     "source_url": "https://reactorcore.itch.io/basic-2d-shapes-primitive-128px",
     "license": "CC0-1.0", "price_usd": 0,
     "tags": ["circle", "shapes", "primitive", "soft edge", "tintable", "sphere", "electron"],
     "notes": "92 flat 128 px shapes (circles, bursts, splats), dark and bright, soft-edge versions; CC0."},
    {"name": "Kenney Mobile Controls", "category": "input", "source_url": "https://kenney.nl/assets/mobile-controls",
     "license": "CC0-1.0", "price_usd": 0, "tags": ["joystick", "touch", "buttons", "mobile", "controls"]},
    {"name": "Kenney UI Pack", "category": "ui", "source_url": "https://kenney.nl/assets/ui-pack",
     "license": "CC0-1.0", "price_usd": 0, "tags": ["ui", "buttons", "health bar", "panels", "hud"]},
    {"name": "Kenney Space Shooter Redux", "category": "sprite", "source_url": "https://kenney.nl/assets/space-shooter-redux",
     "license": "CC0-1.0", "price_usd": 0, "tags": ["space", "enemies", "sprites", "lasers", "background"]},
    {"name": "Kenney Fonts", "category": "font", "source_url": "https://kenney.nl/assets/kenney-fonts",
     "license": "CC0-1.0", "price_usd": 0, "tags": ["font", "ui", "text"]},
    {"name": "Kenney Interface Sounds", "category": "audio", "source_url": "https://kenney.nl/assets/interface-sounds",
     "license": "CC0-1.0", "price_usd": 0, "tags": ["ui", "click", "sfx", "audio"]},
    {"name": "Kenney Sci-fi Sounds", "category": "audio", "source_url": "https://kenney.nl/assets/sci-fi-sounds",
     "license": "CC0-1.0", "price_usd": 0, "tags": ["laser", "shoot", "sfx", "energy", "audio"]},
    {"name": "Joystick Pack (Fenerax Studios)", "category": "input",
     "source_url": "https://assetstore.unity.com/packages/tools/input-management/joystick-pack-107631",
     "license": "Unity-Asset-Store-EULA", "price_usd": 0, "tags": ["joystick", "touch", "mobile", "virtual stick"]},
    {"name": "Unity Universal Render Pipeline", "category": "shader",
     "source_url": "https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/index.html",
     "license": "Unity-Package", "price_usd": 0, "tags": ["shader", "bloom", "glow", "2d lights", "post processing"]},
    {"name": "TextMesh Pro (in uGUI)", "category": "ui",
     "source_url": "https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html",
     "license": "Unity-Package", "price_usd": 0, "tags": ["text", "font", "ui", "hud"]},
]


@tool("free_asset_catalog")
def free_asset_catalog(query: str) -> str:
    """Search the curated catalogue of FREE Unity-compatible game assets.
    Input: space-separated keywords (e.g. 'particles glow joystick ui sfx').
    Returns a JSON list of matching assets with name, url, license and price."""
    words = [w.lower() for w in query.replace(",", " ").split() if w.strip()]
    hits = [a for a in FREE_ASSET_CATALOG
            if not words or any(w in " ".join(a["tags"] + [a["name"].lower(), a["category"]]) for w in words)]
    return json.dumps(hits or FREE_ASSET_CATALOG, indent=1)


@tool("license_check")
def license_check(license_id: str) -> str:
    """Classify a license string. Returns 'ALLOWED' (free for commercial Android release,
    no attribution needed or attribution only) or 'REJECT' with a reason."""
    lic = license_id.strip().upper()
    if lic.startswith(("CC0", "UNITY-PACKAGE", "UNITY-ASSET-STORE-EULA", "MIT", "OFL")):
        return "ALLOWED"
    if lic.startswith("CC-BY") and not {"NC", "SA", "ND"} & set(lic.split("-")):
        return "ALLOWED (attribution required: add to credits)"
    return f"REJECT: '{license_id}' is non-commercial, share-alike, paid or unknown"


# --------------------------------------------------------------------------- shared GDD tool
@tool("gdd_formula_table")
def gdd_formula_table(atomic_numbers: str) -> str:
    """Return the fixed GDD numbers (HP, electron slots, energy bar size to evolve)
    and element group for a comma-separated list of atomic numbers 1-10, e.g. '1,6,10'.
    Evolution has no cost and no failure chance: a full bar evolves the player and empties to 0."""
    out = []
    for tok in atomic_numbers.split(","):
        tok = tok.strip()
        if not tok.isdigit():
            continue
        z = int(tok)
        if 1 <= z <= R.MAX_ATOMIC_NUMBER:
            out.append({**R.element(z), "max_hp": R.max_hp(z), "electron_slots": z,
                        "energy_to_evolve": R.energy_bar_capacity(z),
                        "is_final_element": z == R.MAX_ATOMIC_NUMBER})
    return json.dumps(out)


@tool("element_groups")
def element_groups(_: str = "") -> str:
    """List the 10 playable elements (H..Ne, one level each) and the 6 element groups they belong to."""
    return json.dumps({
        "levels": [{**R.element(z), "energy_to_evolve": R.energy_bar_capacity(z)}
                   for z in range(1, R.MAX_ATOMIC_NUMBER + 1)],
        "groups": {g: [R.element(z)["symbol"] for z in zs] for g, zs in R.GROUPS.items()},
    })


# --------------------------------------------------------------------------- Lead Architect validation tool
@tool("run_validation_suite")
def run_validation_suite(scope: str = "all") -> str:
    """Run the deterministic Atom.IO validation suite on everything produced so far
    in this run (asset manifest, architect review, level designs, the 10 expanded tier
    JSON files, and C# code bundle). Input is ignored; pass 'all'.
    Returns JSON {suite, passed, errors, warnings}."""
    from .tiers import build_all

    s = STATE
    if s.design is not None and s.tier_files is None:
        s.tier_files, s.tier_index = build_all(s.design)
    result = validators.run_all(manifest=s.manifest, review=s.review, design=s.design,
                                tier_files=s.tier_files, code=s.code)
    s.extras["validation"] = result
    return json.dumps(result)


@tool("tier_json_contract")
def tier_json_contract(atomic_number: str = "5") -> str:
    """Return one tier JSON file exactly as the game will load it, built from the
    Level Designer's actual TierDesignSet in this run. Your C# serializable classes
    must declare a public field for every key (same names, same nesting).
    Input: an atomic number 1-10 (default 5)."""
    from .tiers import build_tier

    if STATE.design is None:
        return ("ERROR: no TierDesignSet from the Level Designer in this run, so there is no tier "
                "JSON contract to implement against. The pipeline cannot continue correctly.")
    z = int(atomic_number) if str(atomic_number).strip().isdigit() else 5
    return json.dumps(build_tier(min(max(z, 1), R.MAX_ATOMIC_NUMBER), STATE.design))


def optional_web_search_tools() -> list:
    """Live web search for the scraper when SERPER_API_KEY is configured."""
    if not os.getenv("SERPER_API_KEY"):
        return []
    try:
        from crewai_tools import SerperDevTool  # type: ignore
    except ImportError:
        return []
    return [SerperDevTool(n_results=8)]
