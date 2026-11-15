"""Atom.IO game rules, taken straight from the GDD (rescoped 2026-11-12).

This module is the single source of truth for every number the GDD fixes
(HP per proton, energy bar size, damage values, ...).
The agents never do this math themselves: the Level Designer chooses *style*
and *tuning knobs* for each of the 10 levels, and ``tiers.py`` combines those
with the rules below to produce the 10 tier files.

Rescope: the game ends at Neon (Z = 10), so there are 10 levels. Evolution has
no cost and no failure chance: an energy bar fills with every bit of energy
collected; when it is full the player evolves and the bar empties to 0.
"""
from __future__ import annotations

# --- GDD 1.3.1 Core mechanics -------------------------------------------------
HP_PER_PROTON = 10                 # "Health starts at 10 and increases by 10 each evolution"
ENEMY_CONTACT_DAMAGE = 10          # "Collide with player core to inflict damage (-10 hp)"
ORBITING_ELECTRON_DAMAGE = 10      # "orbiting (-10 hp)"
RELEASED_ELECTRON_DAMAGE = 20      # "released (-20 hp)"
ENEMY_BASE_HP = 10                 # "Enemy health starts at 10 and increases linearly"
MIN_ORBITING_ELECTRONS = 0         # design change 2026-11-15: no minimum, the last electron can be thrown
MAX_ATOMIC_NUMBER = 10             # Neon = final element and win condition (10 levels)

# --- GDD 1.3.3 Evolution: energy bar ----------------------------------------------
BAR_BASE = 50                      # bar capacity = 50 + 3 x Z
BAR_PER_PROTON = 3
BAR_RESET_VALUE = 0                # a full bar evolves the player and empties to 0 (no carry-over)


def max_hp(z: int) -> int:
    return HP_PER_PROTON * z


def electron_slots(z: int) -> int:
    """Max orbiting electrons = atomic number = number of protons (GDD 1.4.1)."""
    return z


def energy_bar_capacity(z: int) -> int:
    """Energy needed to fill the bar and evolve from element z to z+1: 50 + 3 * z.
    The final element (Neon) has no bar: returns 0."""
    return 0 if z >= MAX_ATOMIC_NUMBER else BAR_BASE + BAR_PER_PROTON * z


def total_energy_to_win() -> int:
    return sum(energy_bar_capacity(z) for z in range(1, MAX_ATOMIC_NUMBER))


# --- Periodic table (the 10 playable elements) ------------------------------------
_SYMBOLS = "H He Li Be B C N O F Ne".split()
_NAMES = "Hydrogen Helium Lithium Beryllium Boron Carbon Nitrogen Oxygen Fluorine Neon".split()

# Element groups drive the bonus "abilities per group" feature (GDD 1.4.5).
# Only the 6 groups that exist between H and Ne are in the game.
GROUPS: dict[str, list[int]] = {
    "reactive_nonmetal": [1, 6, 7, 8],
    "noble_gas": [2, 10],
    "alkali_metal": [3],
    "alkaline_earth_metal": [4],
    "metalloid": [5],
    "halogen": [9],
}
GROUP_NAMES = list(GROUPS)

_GROUP_OF: dict[int, str] = {z: g for g, zs in GROUPS.items() for z in zs}

assert len(_SYMBOLS) == len(_NAMES) == MAX_ATOMIC_NUMBER, "the game has exactly 10 elements"
assert sorted(_GROUP_OF) == list(range(1, MAX_ATOMIC_NUMBER + 1)), "every element needs exactly one group"


def element(z: int) -> dict:
    if not 1 <= z <= MAX_ATOMIC_NUMBER:
        raise ValueError(f"atomic number out of range 1-{MAX_ATOMIC_NUMBER}: {z}")
    return {"atomic_number": z, "symbol": _SYMBOLS[z - 1], "name": _NAMES[z - 1], "group": _GROUP_OF[z]}


def all_elements() -> list[dict]:
    return [element(z) for z in range(1, MAX_ATOMIC_NUMBER + 1)]


def min_orbiting(z: int) -> int:
    """Electrons that must stay in orbit: none for any element (design change 2026-11-15)."""
    return MIN_ORBITING_ELECTRONS
