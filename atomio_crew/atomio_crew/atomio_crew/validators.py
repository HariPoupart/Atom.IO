"""Deterministic validation suite run before the manual gate (GDD 2.4 / 2.5).

The Lead Architect calls this through the ``run_validation_suite`` tool and
the CLI runs it again before anything can be promoted into the Unity project.
"""
from __future__ import annotations

import re
import shutil
import subprocess
import tempfile
from pathlib import Path, PurePosixPath

from . import gdd_rules as R
from .schemas import ArchitectReview, AssetManifest, CodeBundle, TierDesignSet

HEX = re.compile(r"^#[0-9A-Fa-f]{6}$")
PATTERNS = {"solid", "rings", "swirl", "crystal", "noise", "pulse", "lattice"}
FREE_LICENSES = ("CC0", "CC-BY", "MIT", "UNITY-ASSET-STORE-EULA", "UNITY-PACKAGE", "OFL")
# Things the shipped game must never use: runtime network calls (GDD 3.1.2)
# and external dependencies not listed in the GDD (GDD 2.3).
FORBIDDEN_USINGS = ("System.Net", "UnityEngine.Networking", "Newtonsoft", "Anthropic", "OpenAI")
SCRIPTS_ROOT = "Assets/Scripts/AtomIO/"

RANGES = {  # SpawnCurve sanity bounds (mirrors the field descriptions)
    "enemy_base_interval_s": (2, 8), "enemy_min_interval_s": (0.3, 3), "time_scale_k": (0.05, 2),
    "enemy_hp_per_tier": (0, 10), "enemy_hp_per_minute": (0, 20), "enemy_move_speed": (1, 6),
    "enemy_sight_range": (3, 20), "max_alive_enemies_base": (3, 40), "max_alive_enemies_per_tier": (0, 1),
    "orb_base_interval_s": (0.5, 6), "electron_base_interval_s": (1, 10), "orb_base_value": (1, 10),
    "energy_growth_per_minute": (1.0, 1.5), "arena_radius_base": (20, 80), "arena_radius_per_tier": (0, 0.5),
}


class Report:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []

    def err(self, m: str) -> None:
        self.errors.append(m)

    def warn(self, m: str) -> None:
        self.warnings.append(m)

    def as_dict(self) -> dict:
        return {"suite": "atomio-validation", "passed": not self.errors,
                "errors": self.errors, "warnings": self.warnings}


# --------------------------------------------------------------------------- assets
def check_manifest(m: AssetManifest, rep: Report) -> None:
    if not m.assets:
        rep.err("asset manifest is empty")
    for a in m.assets:
        if a.price_usd != 0:
            rep.err(f"asset '{a.name}' is not free (price_usd={a.price_usd})")
        lic = a.license.upper()
        if not lic.startswith(FREE_LICENSES):
            rep.err(f"asset '{a.name}' has unrecognised license '{a.license}'")
        elif any(t in lic.split("-") for t in ("NC", "SA", "ND")):
            rep.err(f"asset '{a.name}' license '{a.license}' is non-commercial, share-alike or no-derivatives")
        if not a.source_url.startswith("https://"):
            rep.err(f"asset '{a.name}' source_url must be https")
        if not (a.local_path.startswith("Assets/") or a.local_path.startswith("Packages/")):
            rep.warn(f"asset '{a.name}' local_path should live under Assets/ or Packages/")


def check_review(review: ArchitectReview, m: AssetManifest, rep: Report) -> None:
    names = {a.name for a in m.assets}
    for n in review.approved_assets:
        if n not in names:
            rep.err(f"architect approved unknown asset '{n}'")


# --------------------------------------------------------------------------- tiers
def _check_visual(cv, where: str, approved: set[str], rep: Report) -> None:
    for f in ("primary_hex", "secondary_hex"):
        if not HEX.match(getattr(cv, f)):
            rep.err(f"{where}: {f} '{getattr(cv, f)}' is not #RRGGBB")
    if cv.pattern not in PATTERNS:
        rep.err(f"{where}: pattern '{cv.pattern}' not in {sorted(PATTERNS)}")
    if not 0 <= cv.glow_intensity <= 3:
        rep.err(f"{where}: glow_intensity {cv.glow_intensity} outside 0-3")
    if cv.texture_asset != "procedural" and cv.texture_asset not in approved:
        rep.err(f"{where}: texture_asset '{cv.texture_asset}' is not an approved asset")


def check_design(d: TierDesignSet, approved: set[str], rep: Report) -> None:
    for k, (lo, hi) in RANGES.items():
        v = getattr(d.spawn_curve, k)
        if not lo <= v <= hi:
            rep.err(f"spawn_curve.{k}={v} outside [{lo}, {hi}]")
    if d.spawn_curve.enemy_min_interval_s > d.spawn_curve.enemy_base_interval_s:
        rep.err("spawn_curve: enemy_min_interval_s must be <= enemy_base_interval_s")

    seen = set()
    for g in [*d.group_designs, d.fallback]:
        where = f"group '{g.group}'"
        if g is not d.fallback:
            if g.group not in R.GROUPS:
                rep.err(f"{where}: unknown group (valid: {R.GROUP_NAMES})")
            if g.group in seen:
                rep.err(f"{where}: designed twice")
            seen.add(g.group)
        _check_visual(g.core_visual, where, approved, rep)
        for f in ("background_hex", "grid_hex"):
            if not HEX.match(getattr(g.arena_theme, f)):
                rep.err(f"{where}: arena_theme.{f} is not #RRGGBB")
        amb = g.arena_theme.ambient_particle_asset
        if amb != "none" and amb not in approved:
            rep.err(f"{where}: ambient_particle_asset '{amb}' is not an approved asset")
        for f in ("move_speed_mult", "orbit_speed_mult", "electron_damage_mult", "max_hp_mult"):
            if not 0.7 <= getattr(g.trait, f) <= 1.3:
                rep.err(f"{where}: trait.{f} outside 0.7-1.3")
    used = {g.core_visual.texture_asset for g in [*d.group_designs, d.fallback]}
    used |= {g.arena_theme.ambient_particle_asset for g in [*d.group_designs, d.fallback]}
    used |= {o.core_visual.texture_asset for o in d.tier_overrides}
    if not used & approved:
        rep.err("Level Designer used none of the approved assets: sourced art never reaches the game")
    missing = set(R.GROUP_NAMES) - seen
    if missing:
        rep.warn(f"groups not designed, will use fallback: {sorted(missing)}")
    for o in d.tier_overrides:
        if not 1 <= o.atomic_number <= R.MAX_ATOMIC_NUMBER:
            rep.err(f"override for invalid atomic number {o.atomic_number}")
        else:
            _check_visual(o.core_visual, f"override Z={o.atomic_number}", approved, rep)


def check_tier_files(files: dict[str, dict], rep: Report) -> None:
    for z in range(1, R.MAX_ATOMIC_NUMBER + 1):
        t = files.get(f"tier_{z:03d}.json")
        if t is None:
            rep.err(f"tier {z} missing")
            continue
        # Re-verify the GDD formulas on the final artefact.
        if t["player"]["max_hp"] != R.max_hp(z):
            rep.err(f"tier {z}: max_hp mismatch")
        if t["player"]["electron_slots"] != z:
            rep.err(f"tier {z}: electron_slots mismatch")
        if z < R.MAX_ATOMIC_NUMBER and t["evolution"]["cost_to_next"] != R.evolution_cost(z):
            rep.err(f"tier {z}: evolution cost mismatch")
    if "tier_fallback.json" not in files:
        rep.err("tier_fallback.json missing")


# --------------------------------------------------------------------------- C#
def _strip_cs(src: str) -> str:
    """Remove comments and string/char literals so brace counting is reliable."""
    src = re.sub(r"//[^\n]*", "", src)
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    src = re.sub(r'@"(?:[^"]|"")*"', '""', src)
    src = re.sub(r'\$?"(?:\\.|[^"\\])*"', '""', src)
    src = re.sub(r"'(?:\\.|[^'\\])'", "' '", src)
    return src


def _json_keys(obj, out: set[str]) -> set[str]:
    if isinstance(obj, dict):
        for k, v in obj.items():
            out.add(k)
            _json_keys(v, out)
    elif isinstance(obj, list):
        for v in obj:
            _json_keys(v, out)
    return out


def check_code(bundle: CodeBundle, sample_tier: dict | None, rep: Report) -> None:
    if not bundle.files:
        rep.err("code bundle is empty")
    paths = set()
    all_fields: set[str] = set()
    for f in bundle.files:
        p = PurePosixPath(f.path)
        if not f.path.startswith(SCRIPTS_ROOT) or ".." in p.parts or p.suffix != ".cs":
            rep.err(f"{f.path}: must be a .cs file under {SCRIPTS_ROOT}")
            continue
        if f.path in paths:
            rep.err(f"{f.path}: duplicate path")
        paths.add(f.path)
        code = _strip_cs(f.content)
        for a, b in ("{}", "()", "[]"):
            if code.count(a) != code.count(b):
                rep.err(f"{f.path}: unbalanced '{a}{b}' ({code.count(a)} vs {code.count(b)})")
        for u in FORBIDDEN_USINGS:
            if re.search(rf"\busing\s+{re.escape(u)}", code) or f"{u}." in code:
                rep.err(f"{f.path}: forbidden dependency '{u}' (no runtime network / no external deps)")
        if not re.search(r"\bnamespace\s+AtomIO\b", code):
            rep.warn(f"{f.path}: not inside the AtomIO namespace")
        stem = p.stem
        if not re.search(rf"\b(class|struct|interface|enum)\s+{re.escape(stem)}\b", code):
            rep.err(f"{f.path}: no type named '{stem}' (Unity requires MonoBehaviour name == file name)")
        all_fields |= set(re.findall(r"\bpublic\s+[\w<>\[\],]+\s+(\w+)\s*(?:=[^;]*)?;", code))

    # Cross-agent contract: every key in the tier JSON must exist as a public
    # field somewhere, otherwise JsonUtility silently drops it.
    if sample_tier is not None:
        missing = sorted(_json_keys(sample_tier, set()) - all_fields)
        if missing:
            rep.err(f"tier JSON keys with no matching public C# field (JsonUtility would drop them): {missing}")


STUBS = Path(__file__).parent / "compile_check" / "UnityStubs.cs"


def compile_check(bundle: CodeBundle, rep: Report) -> None:
    """Compile the bundle against Unity API stubs with Mono's `mcs`, if installed.

    This catches real C# errors (typos, wrong types, missing members) before the
    developer opens Unity. It is not a full Unity compile: an API missing from the
    stubs is reported as a warning so a human can confirm it.
    """
    mcs = shutil.which("mcs")
    if not mcs:
        rep.warn("compile check skipped: install Mono `mcs` to compile C# against Unity stubs")
        return
    with tempfile.TemporaryDirectory() as tmp:
        srcs = []
        for i, f in enumerate(bundle.files):
            p = Path(tmp) / f"{i:03d}_{PurePosixPath(f.path).name}"
            p.write_text(f.content)
            srcs.append((p, f.path))
        proc = subprocess.run([mcs, "-target:library", "-nologo", "-nowarn:0618,0649",
                               f"-out:{Path(tmp) / 'check.dll'}", str(STUBS), *[str(p) for p, _ in srcs]],
                              capture_output=True, text=True, timeout=120)
    names = {p.name: orig for p, orig in srcs}
    for line in (proc.stdout + proc.stderr).splitlines():
        m = re.match(r".*?([^/\\]+\.cs)\((\d+),\d+\): (error|warning) (CS\d+): (.*)", line)
        if not m:
            continue
        fname, ln, kind, code, msg = m.groups()
        where = f"{names.get(fname, fname)}:{ln}"
        # Missing *Unity* APIs may just be absent from the stubs -> warning; everything else is an error.
        unknown_api = code in ("CS0246", "CS0234") or (
            code in ("CS0117", "CS1061") and ("UnityEngine" in msg or "TMPro" in msg))
        if kind == "error" and not unknown_api:
            rep.err(f"compile {code} at {where}: {msg}")
        elif kind == "error":
            rep.warn(f"compile {code} at {where}: {msg} (API not in stubs: confirm it exists in Unity 6.3)")
        else:
            rep.warn(f"compile {code} at {where}: {msg}")


# Which agent produces each stage. Missing stage = that agent was removed or failed.
STAGES = {
    "manifest": "Asset Scraper (AssetManifest)",
    "review": "Lead Architect (ArchitectReview)",
    "design": "Level Designer (TierDesignSet)",
    "code": "Lead Architect (CodeBundle)",
}


def run_all(manifest=None, review=None, design=None, tier_files=None, code=None) -> dict:
    rep = Report()
    given = {"manifest": manifest, "review": review, "design": design, "code": code}
    for key, who in STAGES.items():
        if given[key] is None:
            rep.err(f"missing output from {who}: pipeline incomplete")

    approved: set[str] = set(review.approved_assets) if review else set()
    if manifest is not None:
        check_manifest(manifest, rep)
    if review is not None and manifest is not None:
        check_review(review, manifest, rep)
    elif review is not None:
        for n in review.approved_assets:
            rep.err(f"Lead Architect approved '{n}' but there is no Asset Scraper manifest it was sourced from")
        approved = set()  # unsourced approvals are not trusted downstream
    if review is not None and not approved:
        rep.err("Lead Architect approved no assets: nothing for the Level Designer to use")
    if design is not None:
        check_design(design, approved, rep)
    if design is not None and tier_files is None:
        from .tiers import build_all
        tier_files, _ = build_all(design)
    if tier_files is not None:
        check_tier_files(tier_files, rep)
    if code is not None:
        sample = tier_files.get("tier_026.json") if tier_files else None
        if sample is None:
            rep.err("no tier JSON to check the C# against (Level Designer output missing)")
        check_code(code, sample, rep)
        compile_check(code, rep)
    return rep.as_dict()
