"""Expand the Level Designer's TierDesignSet into one runtime JSON file per element.

The agent designs *groups* (11 of them), landmark overrides and one spawn curve;
this module multiplies that out to all 118 tiers using the fixed GDD formulas.
Any group the agent did not design is emitted with the fallback block, so no
tier is ever skipped (GDD 2.3).
"""
from __future__ import annotations

from . import gdd_rules as R
from .schemas import GroupDesign, TierDesignSet

SCHEMA_VERSION = 1


def _r(x: float, nd: int = 3) -> float:
    return round(float(x), nd)


def build_tier(z: int, design: TierDesignSet) -> dict:
    el = R.element(z)
    groups = {g.group: g for g in design.group_designs}
    overrides = {o.atomic_number: o for o in design.tier_overrides}
    sc = design.spawn_curve

    gd: GroupDesign = groups.get(el["group"], design.fallback)
    source = "group" if el["group"] in groups else "fallback"
    core = gd.core_visual
    if z in overrides:
        core, source = overrides[z].core_visual, "override"

    is_last = z == R.MAX_ATOMIC_NUMBER
    return {
        "schema_version": SCHEMA_VERSION,
        "atomic_number": z,
        "symbol": el["symbol"],
        "name": el["name"],
        "group": el["group"],
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
            "cost_to_next": 0 if is_last else R.evolution_cost(z),
            "base_fail_chance_pct": 0.0 if is_last else _r(R.base_fail_chance_pct(z)),
            "pity_step_pct": R.PITY_STEP_PCT,
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
            "electron_base_interval_s": _r(sc.electron_base_interval_s),
        },
        "energy": {
            "orb_base_value": _r(sc.orb_base_value, 2),
            "growth_per_minute": _r(sc.energy_growth_per_minute),
        },
        "arena": {
            "radius": _r(sc.arena_radius_base + sc.arena_radius_per_tier * (z - 1), 2),
            "background_hex": gd.arena_theme.background_hex,
            "grid_hex": gd.arena_theme.grid_hex,
            "ambient_particle_asset": gd.arena_theme.ambient_particle_asset,
        },
        "core_visual": core.model_dump(),
        "trait": gd.trait.model_dump(),
    }


def build_all(design: TierDesignSet) -> tuple[dict[str, dict], dict]:
    """Return ({filename: tier_json}, index_json)."""
    files: dict[str, dict] = {}
    for z in range(1, R.MAX_ATOMIC_NUMBER + 1):
        files[f"tier_{z:03d}.json"] = build_tier(z, design)

    # Stand-alone fallback block, loaded by the game if a tier file is ever missing.
    fb = build_tier(1, design.model_copy(update={"group_designs": [], "tier_overrides": []}))
    fb.update({"atomic_number": 0, "symbol": "?", "name": "Fallback", "group": "fallback",
               "design_source": "fallback"})
    files["tier_fallback.json"] = fb

    index = {
        "schema_version": SCHEMA_VERSION,
        "tier_count": R.MAX_ATOMIC_NUMBER,
        "fallback_file": "tier_fallback",
        "design_sources": {s: sum(1 for t in files.values() if t["design_source"] == s and t["atomic_number"])
                           for s in ("group", "override", "fallback")},
        "tiers": [{"atomic_number": t["atomic_number"], "symbol": t["symbol"], "file": f[:-5]}
                  for f, t in files.items() if t["atomic_number"]],
    }
    return files, index
