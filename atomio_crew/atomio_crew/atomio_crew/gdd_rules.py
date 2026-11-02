"""Atom.IO game rules, taken straight from the GDD.

This module is the single source of truth for every number the GDD fixes
(HP per proton, evolution cost, failure curve, damage values, ...).
The agents never do this math themselves: the Level Designer chooses *style*
and *tuning knobs*, and the expander in ``postprocess.py`` combines those with
the rules below to produce the 118 tier files.
"""
from __future__ import annotations

# --- GDD 1.3.1 Core mechanics -------------------------------------------------
HP_PER_PROTON = 10                 # "Health starts at 10 and increases by 10 each evolution"
ENEMY_CONTACT_DAMAGE = 10          # "Collide with player core to inflict damage (-10 hp)"
ORBITING_ELECTRON_DAMAGE = 10      # "orbiting (-10 hp)"
RELEASED_ELECTRON_DAMAGE = 20      # "released (-20 hp)"
ENEMY_BASE_HP = 10                 # "Enemy health starts at 10 and increases linearly"
MIN_ORBITING_ELECTRONS = 1         # "The player always has at least 1 electron orbiting them"
MAX_ATOMIC_NUMBER = 118            # Oganesson = win condition

# --- GDD 1.3.3 Win / loss -------------------------------------------------------
PITY_STEP_PCT = 0.5                # "Each failure subtracts 0.5% to the chance of failure"


def max_hp(z: int) -> int:
    return HP_PER_PROTON * z


def electron_slots(z: int) -> int:
    """Max orbiting electrons = atomic number = number of protons (GDD 1.4.1)."""
    return z


def evolution_cost(z: int) -> int:
    """Energy spent to evolve from element z to z+1: 50 + 3 * z."""
    return 50 + 3 * z


def base_fail_chance_pct(z: int) -> float:
    """(-1 + 1.04015^(z-1)) %, i.e. 0 % for hydrogen, ~99 % at element 118."""
    return max(0.0, -1.0 + 1.04015 ** (z - 1))


def fail_chance_pct(z: int, failures_at_tier: int) -> float:
    return min(100.0, max(0.0, base_fail_chance_pct(z) - PITY_STEP_PCT * failures_at_tier))


STARTER_ENERGY = evolution_cost(1)  # GDD 1.4.3: "starts with enough energy to evolve once"

# --- Periodic table -------------------------------------------------------------
_SYMBOLS = (
    "H He Li Be B C N O F Ne Na Mg Al Si P S Cl Ar K Ca Sc Ti V Cr Mn Fe Co Ni Cu Zn "
    "Ga Ge As Se Br Kr Rb Sr Y Zr Nb Mo Tc Ru Rh Pd Ag Cd In Sn Sb Te I Xe Cs Ba La Ce "
    "Pr Nd Pm Sm Eu Gd Tb Dy Ho Er Tm Yb Lu Hf Ta W Re Os Ir Pt Au Hg Tl Pb Bi Po At Rn "
    "Fr Ra Ac Th Pa U Np Pu Am Cm Bk Cf Es Fm Md No Lr Rf Db Sg Bh Hs Mt Ds Rg Cn Nh Fl "
    "Mc Lv Ts Og"
).split()

_NAMES = (
    "Hydrogen Helium Lithium Beryllium Boron Carbon Nitrogen Oxygen Fluorine Neon Sodium "
    "Magnesium Aluminium Silicon Phosphorus Sulfur Chlorine Argon Potassium Calcium Scandium "
    "Titanium Vanadium Chromium Manganese Iron Cobalt Nickel Copper Zinc Gallium Germanium "
    "Arsenic Selenium Bromine Krypton Rubidium Strontium Yttrium Zirconium Niobium Molybdenum "
    "Technetium Ruthenium Rhodium Palladium Silver Cadmium Indium Tin Antimony Tellurium Iodine "
    "Xenon Caesium Barium Lanthanum Cerium Praseodymium Neodymium Promethium Samarium Europium "
    "Gadolinium Terbium Dysprosium Holmium Erbium Thulium Ytterbium Lutetium Hafnium Tantalum "
    "Tungsten Rhenium Osmium Iridium Platinum Gold Mercury Thallium Lead Bismuth Polonium "
    "Astatine Radon Francium Radium Actinium Thorium Protactinium Uranium Neptunium Plutonium "
    "Americium Curium Berkelium Californium Einsteinium Fermium Mendelevium Nobelium Lawrencium "
    "Rutherfordium Dubnium Seaborgium Bohrium Hassium Meitnerium Darmstadtium Roentgenium "
    "Copernicium Nihonium Flerovium Moscovium Livermorium Tennessine Oganesson"
).split()

# Element groups drive the bonus "traits & appearance per group" feature (GDD 1.4.5).
GROUPS: dict[str, list[int]] = {
    "reactive_nonmetal": [1, 6, 7, 8, 15, 16, 34],
    "noble_gas": [2, 10, 18, 36, 54, 86],
    "alkali_metal": [3, 11, 19, 37, 55, 87],
    "alkaline_earth_metal": [4, 12, 20, 38, 56, 88],
    "metalloid": [5, 14, 32, 33, 51, 52],
    "halogen": [9, 17, 35, 53, 85],
    "post_transition_metal": [13, 31, 49, 50, 81, 82, 83, 84],
    "transition_metal": list(range(21, 31)) + list(range(39, 49)) + list(range(72, 81))
    + list(range(104, 109)),
    "lanthanide": list(range(57, 72)),
    "actinide": list(range(89, 104)),
    # 109-118: chemical properties not experimentally known -> its own group.
    "unknown_properties": list(range(109, 119)),
}
GROUP_NAMES = list(GROUPS)

_GROUP_OF: dict[int, str] = {z: g for g, zs in GROUPS.items() for z in zs}

assert len(_SYMBOLS) == len(_NAMES) == MAX_ATOMIC_NUMBER, "periodic table must have 118 entries"
assert sorted(_GROUP_OF) == list(range(1, MAX_ATOMIC_NUMBER + 1)), "every element needs exactly one group"


def element(z: int) -> dict:
    if not 1 <= z <= MAX_ATOMIC_NUMBER:
        raise ValueError(f"atomic number out of range: {z}")
    return {"atomic_number": z, "symbol": _SYMBOLS[z - 1], "name": _NAMES[z - 1], "group": _GROUP_OF[z]}


def all_elements() -> list[dict]:
    return [element(z) for z in range(1, MAX_ATOMIC_NUMBER + 1)]
