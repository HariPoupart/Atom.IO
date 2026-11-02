"""Structured contracts between agents (GDD 2.3 "prompt contracts").

Every task returns one of these Pydantic models, so each hand-off between
agents is machine-checked instead of free text.
"""
from __future__ import annotations

from pydantic import BaseModel, Field

# --------------------------------------------------------------------------- #
# Asset Scraper  ->  Lead Architect
# --------------------------------------------------------------------------- #


class AssetEntry(BaseModel):
    name: str = Field(description="Human-readable asset/pack name")
    category: str = Field(description="One of: vfx, ui, input, audio, font, sprite, shader, other")
    source_url: str = Field(description="Where the asset can be downloaded")
    license: str = Field(description="License identifier, e.g. CC0-1.0, Unity-Asset-Store-EULA, Unity-Package")
    price_usd: float = Field(description="Must be 0. Paid assets are excluded.")
    local_path: str = Field(description="Planned path inside the Unity project, under Assets/ThirdParty/ or Packages/")
    unity_compat_note: str = Field(description="Compatibility note for Unity 6.3 LTS / Android")
    used_for: str = Field(description="Which Atom.IO feature this asset serves")


class AssetManifest(BaseModel):
    assets: list[AssetEntry]
    search_notes: str = Field(description="Short note on queries run and anything excluded")


# --------------------------------------------------------------------------- #
# Lead Architect review (asset approval + implementation plan)
# --------------------------------------------------------------------------- #


class RejectedAsset(BaseModel):
    name: str
    reason: str


class ArchitectReview(BaseModel):
    approved_assets: list[str] = Field(description="Names of approved assets, exactly as in the manifest")
    rejected_assets: list[RejectedAsset]
    implementation_plan: list[str] = Field(description="Ordered implementation steps")
    prefab_scene_notes: str = Field(description="Prefab and scene setup notes for the developer")


# --------------------------------------------------------------------------- #
# Level Designer  ->  Lead Architect
# --------------------------------------------------------------------------- #


class SpawnCurve(BaseModel):
    """Tuning knobs. Per-tier numbers are derived from these + gdd_rules."""
    enemy_base_interval_s: float = Field(description="Seconds between enemy spawns at t=0 (2-8)")
    enemy_min_interval_s: float = Field(description="Floor for the enemy spawn interval (0.3-3)")
    time_scale_k: float = Field(description="interval(t) = base / (1 + k * minutes_elapsed); k in 0.05-2")
    enemy_hp_per_tier: float = Field(description="Enemy HP added per player atomic number (0-10)")
    enemy_hp_per_minute: float = Field(description="Enemy HP added per minute survived (0-20)")
    enemy_move_speed: float = Field(description="Units/second (1-6)")
    enemy_sight_range: float = Field(description="Units (3-20)")
    max_alive_enemies_base: int = Field(description="Max simultaneous enemies at tier 1 (3-40)")
    max_alive_enemies_per_tier: float = Field(description="Extra simultaneous enemies per tier (0-1)")
    orb_base_interval_s: float = Field(description="Seconds between natural energy-orb spawns (0.5-6)")
    electron_base_interval_s: float = Field(description="Seconds between wild-electron spawns (1-10)")
    orb_base_value: float = Field(description="Energy per orb at t=0 (1-10)")
    energy_growth_per_minute: float = Field(description="Orb value multiplier per minute, exponential (1.0-1.5)")
    arena_radius_base: float = Field(description="Arena radius at tier 1 (20-80)")
    arena_radius_per_tier: float = Field(description="Arena radius growth per tier (0-0.5)")


class CoreVisual(BaseModel):
    primary_hex: str = Field(description="#RRGGBB nucleus colour")
    secondary_hex: str = Field(description="#RRGGBB electron / rim colour")
    glow_intensity: float = Field(description="0-3 bloom strength")
    pattern: str = Field(description="One of: solid, rings, swirl, crystal, noise, pulse, lattice")
    texture_asset: str = Field(description="Approved asset name (or 'procedural') used to texture the core")
    shader_note: str = Field(description="One sentence for the shader/material setup")


class ArenaTheme(BaseModel):
    background_hex: str
    grid_hex: str
    ambient_particle_asset: str = Field(description="Approved asset name or 'none'")


class Trait(BaseModel):
    name: str
    description: str
    move_speed_mult: float = Field(description="0.7-1.3")
    orbit_speed_mult: float = Field(description="0.7-1.3")
    electron_damage_mult: float = Field(description="0.7-1.3")
    max_hp_mult: float = Field(description="0.7-1.3")


class GroupDesign(BaseModel):
    group: str = Field(description="Element group key, e.g. noble_gas")
    core_visual: CoreVisual
    arena_theme: ArenaTheme
    trait: Trait


class TierOverride(BaseModel):
    atomic_number: int
    reason: str
    core_visual: CoreVisual


class TierDesignSet(BaseModel):
    spawn_curve: SpawnCurve
    group_designs: list[GroupDesign]
    tier_overrides: list[TierOverride] = Field(description="Signature looks for landmark elements")
    fallback: GroupDesign = Field(description="Used for any tier/group that was not designed (group='fallback')")


# --------------------------------------------------------------------------- #
# Lead Architect code bundle + validation
# --------------------------------------------------------------------------- #


class CodeFile(BaseModel):
    path: str = Field(description="Path under Assets/Scripts/AtomIO/, ending in .cs")
    content: str = Field(description="Complete, compilable C# source")


class CodeBundle(BaseModel):
    files: list[CodeFile]
    changes_md: str = Field(description="Markdown for CHANGES.md describing every file and why")


class ValidationReport(BaseModel):
    approved_for_gate: bool = Field(description="True only if the validation suite reported zero errors")
    errors: list[str]
    warnings: list[str]
    notes: str = Field(description="What the developer should check at the manual gate")
