"""The three pieces of Atom.IO content the pipeline generates.

Each spec bundles:
  * the GDD sections it is grounded in (retrieval),
  * the Writer's task and the JSON shape to return (a Pydantic model),
  * deterministic rule checks the Critic runs in code (numbers, limits,
    coverage, banned lore) on top of its own LLM review,
  * a Markdown preview for humans.
All outputs are JsonUtility-friendly (top-level object, arrays, no dicts) so
Unity can load them from Resources/AtomIO/Content/.
"""
from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Callable

from pydantic import BaseModel, Field

from .. import gdd_rules as R
from . import context as C

# --------------------------------------------------------------------------- shared rule helpers
BANNED_LORE = [  # invented-lore markers the style guide forbids (GDD has no story)
    "king", "queen", "kingdom", "empire", "emperor", "prophecy", "chosen one", "villain", "wizard",
    "magic", "curse", "dragon", "hero", "legendary warrior", "dark lord", "realm",
]
RULE_BREAKERS = ["invincible", "immune", "immunity", "infinite", "unlimited", "never die", "can't die",
                 "throw all", "throw every", "last electron away"]
# Every number the GDD fixes, plus small counting numbers used in tutorials.
ALLOWED_NUMBERS = {"0.5", "1", "2", "3", "8", "10", "18", "20", "32", "50", "118"}
PLACEHOLDERS = {"{element}", "{symbol}", "{cost}", "{energy}", "{next_fail_pct}", "{time}"}


def issue(severity: str, item: str, problem: str, fix: str, gdd_ref: str = "") -> dict:
    return {"severity": severity, "item": item, "problem": problem, "fix": fix, "gdd_ref": gdd_ref,
            "source": "rules"}


def _word_hits(text: str, words: list[str]) -> list[str]:
    low = text.lower()
    return [w for w in words if re.search(rf"\b{re.escape(w)}\b", low)]


def _lore_and_rule_checks(item: str, text: str, out: list[dict]) -> None:
    for w in _word_hits(text, BANNED_LORE):
        out.append(issue("major", item, f"invented-lore word '{w}'",
                         "Atom.IO has no story: use a real fact or game mechanic instead", "style guide rule 1"))
    for w in _word_hits(text, RULE_BREAKERS):
        out.append(issue("blocker", item, f"'{w}' contradicts the core rules",
                         "Keep effects temporary/bounded and never break the 1-electron rule", "1.4.2 balance note"))


# --------------------------------------------------------------------------- 1) element cards
class ElementCard(BaseModel):
    atomic_number: int
    symbol: str
    title: str = Field(description="Card title, max 24 characters")
    flavor: str = Field(description="One-line flavor text, max 110 characters, based on a true fact")


class ElementCards(BaseModel):
    cards: list[ElementCard]


def check_element_cards(d: ElementCards) -> list[dict]:
    out: list[dict] = []
    seen: dict[int, int] = {}
    titles: dict[str, int] = {}
    for c in d.cards:
        item = f"card Z={c.atomic_number}"
        seen[c.atomic_number] = seen.get(c.atomic_number, 0) + 1
        if not 1 <= c.atomic_number <= R.MAX_ATOMIC_NUMBER:
            out.append(issue("blocker", item, "atomic number out of range", "Use 1-118"))
            continue
        el = R.element(c.atomic_number)
        if c.symbol != el["symbol"]:
            out.append(issue("blocker", item, f"symbol '{c.symbol}' should be '{el['symbol']}' ({el['name']})",
                             "Use the symbol from the element table"))
        if len(c.title) > 24:
            out.append(issue("major", item, f"title is {len(c.title)} chars (max 24)", "Shorten the title", "3.1 UI"))
        if len(c.flavor) > 110:
            out.append(issue("major", item, f"flavor is {len(c.flavor)} chars (max 110)", "Cut to one punchy line", "3.1 UI"))
        if len(c.flavor) < 25:
            out.append(issue("minor", item, "flavor is too thin", "Add one real fact"))
        if c.title.lower() in titles:
            out.append(issue("minor", item, f"title duplicates Z={titles[c.title.lower()]}", "Make titles unique"))
        titles.setdefault(c.title.lower(), c.atomic_number)
        _lore_and_rule_checks(item, f"{c.title} {c.flavor}", out)
    missing = [z for z in range(1, R.MAX_ATOMIC_NUMBER + 1) if z not in seen]
    if missing:
        out.append(issue("blocker", "cards", f"missing elements: {missing}",
                         "Write exactly one card for every element 1-118", "1.1 core idea"))
    dupes = [z for z, n in seen.items() if n > 1]
    if dupes:
        out.append(issue("major", "cards", f"duplicate cards for {dupes}", "One card per element"))
    return out


def md_element_cards(d: ElementCards) -> str:
    rows = ["| Z | Symbol | Element | Group | Title | Flavor |", "|---|---|---|---|---|---|"]
    for c in sorted(d.cards, key=lambda c: c.atomic_number):
        el = R.element(c.atomic_number) if 1 <= c.atomic_number <= 118 else {"name": "?", "group": "?"}
        rows.append(f"| {c.atomic_number} | {c.symbol} | {el['name']} | {el['group'].replace('_', ' ')} "
                    f"| {c.title} | {c.flavor} |")
    return "# Element cards (lobby evolve screen)\n\n" + "\n".join(rows) + "\n"


# --------------------------------------------------------------------------- 2) UI + tutorial text
class UiText(BaseModel):
    tutorial_run1: list[str] = Field(description="4 hints for the first run as Hydrogen, max 80 chars each")
    tutorial_evolve_prompt: str = Field(description="Lobby prompt forcing the H->He evolution, max 90 chars")
    tutorial_run2: list[str] = Field(description="4 hints for the second run (throw + pickup), max 80 chars each")
    evolve_success: list[str] = Field(description="3 variants, max 60 chars, must contain {element}")
    evolve_fail: list[str] = Field(description="3 variants, max 80 chars, must contain {next_fail_pct}")
    not_enough_energy: str = Field(description="max 70 chars, must contain {cost}")
    run_over: list[str] = Field(description="3 variants, max 60 chars, must contain {energy}")
    win_oganesson: str = Field(description="Victory text for reaching element 118, max 140 chars")
    loading_tips: list[str] = Field(description="8 tips, max 90 chars, each teaching a real GDD mechanic")


UI_LIMITS = {  # field: (count, max_chars, required placeholder)
    "tutorial_run1": (4, 80, None), "tutorial_evolve_prompt": (None, 90, None),
    "tutorial_run2": (4, 80, None), "evolve_success": (3, 60, "{element}"),
    "evolve_fail": (3, 80, "{next_fail_pct}"), "not_enough_energy": (None, 70, "{cost}"),
    "run_over": (3, 60, "{energy}"), "win_oganesson": (None, 140, None), "loading_tips": (8, 90, None),
}


def check_ui_text(d: UiText) -> list[dict]:
    out: list[dict] = []
    for field, (count, limit, ph) in UI_LIMITS.items():
        val = getattr(d, field)
        lines = val if isinstance(val, list) else [val]
        if count is not None and len(lines) != count:
            out.append(issue("major", field, f"has {len(lines)} lines, needs {count}", f"Return exactly {count}"))
        for i, line in enumerate(lines):
            item = f"{field}[{i}]" if isinstance(val, list) else field
            if len(line) > limit:
                out.append(issue("major", item, f"{len(line)} chars (max {limit})", "Shorten it", "3.1 UI"))
            if ph and ph not in line:
                out.append(issue("blocker", item, f"missing placeholder {ph}", f"Include {ph} exactly"))
            for p in set(re.findall(r"\{[^}]*\}", line)) - PLACEHOLDERS:
                out.append(issue("blocker", item, f"unknown placeholder {p}", f"Only use {sorted(PLACEHOLDERS)}"))
            text_no_ph = re.sub(r"\{[^}]*\}", "", line)
            for num in re.findall(r"(?<![\w.])\d+(?:\.\d+)?", text_no_ph):
                if num not in ALLOWED_NUMBERS:
                    out.append(issue("blocker", item, f"number {num} is not a GDD value",
                                     "Only state numbers the GDD fixes (10 HP/proton, 50 + 3Z, 0.5%, 10/20 dmg, 118)",
                                     "1.3.1 / 1.3.3"))
            _lore_and_rule_checks(item, line, out)
    return out


def md_ui_text(d: UiText) -> str:
    parts = ["# UI, tutorial and lobby text\n"]
    for field in UI_LIMITS:
        val = getattr(d, field)
        parts.append(f"## {field}")
        parts += [f"- {v}" for v in (val if isinstance(val, list) else [val])]
        parts.append("")
    return "\n".join(parts)


# --------------------------------------------------------------------------- 3) group abilities
TRIGGERS = {"passive", "on_release", "on_orbit_hit", "on_pickup", "on_low_hp", "on_kill"}
EFFECT_RANGES = {  # effect: (min, max)
    "damage_mult": (1.05, 1.5), "orbit_speed_mult": (1.05, 1.5), "move_speed_mult": (1.05, 1.3),
    "pickup_radius_mult": (1.05, 1.5), "energy_mult": (1.05, 1.5), "heal_hp": (5, 30),
    "shield_seconds": (0.5, 3), "chain_hit": (1, 3), "slow_enemies": (0.5, 0.9),
}


class GroupAbility(BaseModel):
    group: str
    name: str = Field(description="Ability name, max 24 chars")
    description: str = Field(description="Player-facing description, max 110 chars")
    trigger: str = Field(description=f"One of {sorted(TRIGGERS)}")
    effect: str = Field(description=f"One of {sorted(EFFECT_RANGES)}")
    value: float = Field(description="Effect strength within the allowed range for that effect")
    cooldown_s: float = Field(description="0 for passive, otherwise 0-30 seconds")
    visual_cue: str = Field(description="What the player sees when it fires, max 60 chars")
    chemistry_basis: str = Field(description="The real chemical property it is based on, max 80 chars")


class GroupAbilities(BaseModel):
    abilities: list[GroupAbility]


def check_group_abilities(d: GroupAbilities) -> list[dict]:
    out: list[dict] = []
    seen, names = set(), set()
    for a in d.abilities:
        item = f"ability '{a.group}'"
        if a.group not in R.GROUPS:
            out.append(issue("blocker", item, "unknown group", f"Use one of {R.GROUP_NAMES}"))
        if a.group in seen:
            out.append(issue("major", item, "group has two abilities", "One ability per group"))
        seen.add(a.group)
        if a.name.lower() in names:
            out.append(issue("minor", item, f"duplicate name '{a.name}'", "Make names unique"))
        names.add(a.name.lower())
        if a.trigger not in TRIGGERS:
            out.append(issue("blocker", item, f"trigger '{a.trigger}' not supported", f"Use one of {sorted(TRIGGERS)}"))
        rng = EFFECT_RANGES.get(a.effect)
        if rng is None:
            out.append(issue("blocker", item, f"effect '{a.effect}' not supported", f"Use one of {sorted(EFFECT_RANGES)}"))
        elif not rng[0] <= a.value <= rng[1]:
            out.append(issue("blocker", item, f"{a.effect}={a.value} outside balance range {rng}",
                             "Bring the value inside the range", "1.4.4 retention / balance"))
        if a.trigger == "passive" and a.cooldown_s != 0:
            out.append(issue("major", item, "passive ability has a cooldown", "Set cooldown_s to 0"))
        if not 0 <= a.cooldown_s <= 30:
            out.append(issue("major", item, f"cooldown {a.cooldown_s}s outside 0-30", "Use 0-30 s"))
        for f, lim in (("name", 24), ("description", 110), ("visual_cue", 60), ("chemistry_basis", 80)):
            if len(getattr(a, f)) > lim:
                out.append(issue("major", item, f"{f} is {len(getattr(a, f))} chars (max {lim})", "Shorten it"))
        _lore_and_rule_checks(item, f"{a.name} {a.description} {a.visual_cue}", out)
    missing = [g for g in R.GROUP_NAMES if g not in seen]
    if missing:
        out.append(issue("blocker", "abilities", f"groups with no ability: {missing}",
                         "Write exactly one ability for each of the 11 groups", "1.4.5 bonus features"))
    return out


def md_group_abilities(d: GroupAbilities) -> str:
    rows = ["| Group | Ability | Trigger | Effect | Cooldown | Description | Based on |",
            "|---|---|---|---|---|---|---|"]
    for a in d.abilities:
        rows.append(f"| {a.group.replace('_', ' ')} | **{a.name}** | {a.trigger} | {a.effect} = {a.value:g} "
                    f"| {a.cooldown_s:g}s | {a.description} | {a.chemistry_basis} |")
    return "# Element-group abilities (GDD 1.4.5)\n\n" + "\n".join(rows) + "\n"


# --------------------------------------------------------------------------- registry
@dataclass
class ContentSpec:
    key: str
    title: str
    why: str                    # why the game needs it
    gdd_sections: list[str]
    task: str
    model: type[BaseModel]
    checks: Callable
    to_md: Callable
    extra_context: Callable[[], str]
    max_tokens: int


SPECS: dict[str, ContentSpec] = {
    "element_cards": ContentSpec(
        key="element_cards", title="Element cards",
        why="Shown on the lobby evolve screen for every one of the 118 evolutions.",
        gdd_sections=["1.1", "1.3.1", "1.4.4", "1.4.5", "3.1"],
        task=("Write one evolve-screen card for EVERY element 1-118 (118 cards, in order). "
              "title: max 24 chars, a punchy nickname. flavor: ONE line, max 110 chars, built on a TRUE, "
              "well-known fact about that element (use, discovery, property or name origin). "
              "Copy atomic_number and symbol exactly from the element table. Vary sentence shapes."),
        model=ElementCards, checks=check_element_cards, to_md=md_element_cards,
        extra_context=lambda: "ELEMENT TABLE (Z|symbol|name|group):\n" + C.element_table(),
        max_tokens=16000,
    ),
    "ui_text": ContentSpec(
        key="ui_text", title="Tutorial, HUD and lobby text",
        why="Required by the GDD 1.4.3 tutorial and by every evolve / run-end screen.",
        gdd_sections=["1.3.1", "1.3.2", "1.3.3", "1.4.1", "1.4.2", "1.4.3"],
        task=("Write the game's tutorial, lobby and run-end text. Follow GDD 1.4.3 exactly: run 1 teaches "
              "moving and that ORBITING electrons damage enemies; then the lobby forces the H->He evolution; "
              "run 2 teaches picking up wild electrons and throwing SPARE electrons. Placeholders allowed: "
              "{element} {symbol} {cost} {energy} {next_fail_pct} {time}. Loading tips must each teach a real "
              "mechanic with GDD-correct numbers."),
        model=UiText, checks=check_ui_text, to_md=md_ui_text,
        extra_context=lambda: "",
        max_tokens=4000,
    ),
    "group_abilities": ContentSpec(
        key="group_abilities", title="Element-group abilities",
        why="GDD 1.4.4/1.4.5 bonus retention feature: each element group plays differently.",
        gdd_sections=["1.3.1", "1.4.2", "1.4.4", "1.4.5"],
        task=("Design ONE ability for each of the 11 element groups. Base each on a real chemical property "
              "of that group (e.g. noble gases: full outer shell; alkali metals: violently reactive). "
              f"trigger must be one of {sorted(TRIGGERS)}; effect one of {sorted(EFFECT_RANGES)} with value "
              f"inside its range {EFFECT_RANGES}. Passive abilities have cooldown_s 0. Abilities must not "
              "duplicate the existing stat traits listed below, and must never let the player throw their "
              "last electron or become permanently immune."),
        model=GroupAbilities, checks=check_group_abilities, to_md=md_group_abilities,
        extra_context=lambda: ("ELEMENT GROUPS:\n" + "\n".join(f"{g}: {[R.element(z)['symbol'] for z in zs]}"
                                                               for g, zs in R.GROUPS.items())
                               + "\n\nEXISTING STAT TRAITS (from the Level Designer, do not duplicate):\n"
                               + C.level_designer_traits()),
        max_tokens=5000,
    ),
}
