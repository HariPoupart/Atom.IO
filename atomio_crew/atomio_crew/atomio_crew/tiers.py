"""Expand the Level Designer's TierDesignSet into one runtime JSON file per level.

Rescope: 10 levels (H..Ne), each designed individually by the Level Designer.
The fixed GDD numbers (HP, slots, energy bar size, damage) always come from
``gdd_rules``; the design only supplies style and tuning. A level the agent did
not design is emitted with the fallback block, so no level is ever skipped
(GDD 2.3).
"""
from __future__ import annotations

from . import gdd_rules as R
from .schemas import LevelDesign, TierDesignSet

SCHEMA_VERSION = 2


def _r(x: float, nd: int = 3) -> float:
    return round(float(x), nd)


def build_tier(z: int, design: TierDesignSet) -> dict:
    el = R.element(z)
    levels = {lv.atomic_number: lv for lv in design.level_designs}
    sc = design.spawn_curve

    lv: LevelDesign = levels.get(z, design.fallback)
    source = "level" if z in levels else "fallback"
    is_last = z == R.MAX_ATOMIC_NUMBER
    return {
        "schema_version": SCHEMA_VERSION,
        "atomic_number": z,
        "symbol": el["symbol"],
        "name": el["name"],
        "group": el["group"],
        "level_name": lv.level_name if source == "level" else f"{el['name']} Arena",
        "design_source": source,
        "player": {
            "max_hp": R.max_hp(z),
            "electron_slots": R.electron_slots(z),
            "min_orbiting_electrons": R.MIN_ORBITING_ELECTRONS,
            "orbiting_electron_damage": R.ORBITING_ELECTRON_DAMAGE,
            "released_electron_damage": R.RELEASED_ELECTRON_DAMAGE,
        },
        "evolution": {
            "next_atomic_number": 0 if is_last else z + 1,
            "energy_to_evolve": R.energy_bar_capacity(z),
            "bar_reset_value": R.BAR_RESET_VALUE,
            "is_final_element": is_last,
        },
        "enemy": {
            "base_hp": _r(R.ENEMY_BASE_HP + sc.enemy_hp_per_tier * (z - 1), 1),
            "hp_per_minute": _r(sc.enemy_hp_per_minute, 2),
            "contact_damage": R.ENEMY_CONTACT_DAMAGE,
            "move_speed": _r(sc.enemy_move_speed, 2),
            "sight_range": _r(sc.enemy_sight_range, 2),
        },
        "spawn": {
            "enemy_base_interval_s": _r(sc.enemy_base_interval_s),
            "enemy_min_interval_s": _r(sc.enemy_min_interval_s),
            "time_scale_k": _r(sc.time_scale_k),
            "max_alive_enemies": int(sc.max_alive_enemies_base + sc.max_alive_enemies_per_tier * (z - 1)),
            "orb_base_interval_s": _r(sc.orb_base_interval_s),
        },
        "electrons": {
            # Hydrogen's single slot is always full and pickup is taught at Helium, so H spawns none.
            "base_interval_s": _r(sc.electron_base_interval_s + sc.electron_interval_per_tier * (z - 1)),
            "max_wild": 0 if z == 1 else int(sc.max_wild_electrons_base + sc.max_wild_electrons_per_tier * (z - 1)),
            "lifetime_s": _r(sc.electron_lifetime_s, 2),
            "min_spawn_distance": _r(sc.electron_min_spawn_distance, 2),
        },
        "energy": {
            "orb_base_value": _r(sc.orb_base_value, 2),
            "growth_per_minute": _r(sc.energy_growth_per_minute),
        },
        "arena": {
            "radius": _r(sc.arena_radius_base + sc.arena_radius_per_tier * (z - 1), 2),
            "background_hex": lv.arena_theme.background_hex,
            "grid_hex": lv.arena_theme.grid_hex,
            "ambient_particle_asset": lv.arena_theme.ambient_particle_asset,
            "obstacle_pattern": lv.arena_layout.obstacle_pattern,
            "obstacle_count": int(lv.arena_layout.obstacle_count),
            "obstacle_size": _r(lv.arena_layout.obstacle_size, 2),
        },
        "core_visual": lv.core_visual.model_dump(),
        "trait": lv.trait.model_dump(),
    }


def build_all(design: TierDesignSet) -> tuple[dict[str, dict], dict]:
    """Return ({filename: tier_json}, index_json)."""
    files: dict[str, dict] = {}
    for z in range(1, R.MAX_ATOMIC_NUMBER + 1):
        files[f"tier_{z:03d}.json"] = build_tier(z, design)

    # Stand-alone fallback block, loaded by the game if a tier file is ever missing.
    fb = build_tier(1, design.model_copy(update={"level_designs": []}))
    fb.update({"atomic_number": 0, "symbol": "?", "name": "Fallback", "group": "fallback",
               "level_name": "Unknown Arena", "design_source": "fallback"})
    files["tier_fallback.json"] = fb

    index = {
        "schema_version": SCHEMA_VERSION,
        "tier_count": R.MAX_ATOMIC_NUMBER,
        "final_element": R.element(R.MAX_ATOMIC_NUMBER)["symbol"],
        "fallback_file": "tier_fallback",
        "design_sources": {s: sum(1 for t in files.values() if t["design_source"] == s and t["atomic_number"])
                           for s in ("level", "fallback")},
        "tiers": [{"atomic_number": t["atomic_number"], "symbol": t["symbol"], "level_name": t["level_name"],
                   "file": f[:-5]}
                  for f, t in files.items() if t["atomic_number"]],
    }
    return files, index
