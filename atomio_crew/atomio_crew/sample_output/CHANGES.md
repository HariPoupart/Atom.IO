# CHANGES (20261113-044205, mock run)

Gate status: **READY FOR REVIEW** (0 errors, 1 warnings)

## Lead Architect: core gameplay scripts

Generated against Unity 6.3 LTS. No external packages and no networking.

| File | Purpose |
|---|---|
| `Assets/Scripts/AtomIO/Arena/ArenaCamera.cs` | Smooth camera follow for the player atom. |
| `Assets/Scripts/AtomIO/Arena/ArenaLayoutBuilder.cs` | Builds each level's boundary ring and static obstacles from the tier's arena layout. |
| `Assets/Scripts/AtomIO/Arena/ArenaSession.cs` | Run lifecycle, energy into the bar, mid-run evolution into the next level, return to lobby. |
| `Assets/Scripts/AtomIO/Arena/ArenaSpawner.cs` | Inverse-time spawning of enemies and orbs; pooled wild electrons per the level's electrons block (cap, min distance, none at H). |
| `Assets/Scripts/AtomIO/Arena/ElectronPool.cs` | Re-uses electron objects (no GC spikes) and counts wild electrons for the per-level cap. |
| `Assets/Scripts/AtomIO/Arena/EnergyOrb.cs` | Energy pickup. |
| `Assets/Scripts/AtomIO/Combat/Electron.cs` | Glowing yellow sphere; wild (bob, pulse, lifetime fade) / orbiting / released states, 10 and 20 damage, pickup flash, returns to the pool. |
| `Assets/Scripts/AtomIO/Combat/Enemy.cs` | Chase in sight range, give up at 1.5x range, contact damage, orb drop. |
| `Assets/Scripts/AtomIO/Controls/PlayerInputBridge.cs` | Connects sticks to the player and orbit. |
| `Assets/Scripts/AtomIO/Controls/TouchStick.cs` | uGUI virtual joystick (move / aim). |
| `Assets/Scripts/AtomIO/Core/AtomFormulas.cs` | GDD constants and formulas (HP, slots, energy bar size, final element Neon). |
| `Assets/Scripts/AtomIO/Data/TierConfig.cs` | Serializable mirror of tier_NNN.json and the spawn/energy/HP curves. |
| `Assets/Scripts/AtomIO/Data/TierConfigLoader.cs` | Loads tier JSON from Resources with fallback; zero network calls. |
| `Assets/Scripts/AtomIO/Meta/LobbyController.cs` | Lobby UI: element, energy bar, win message at Neon, start run. |
| `Assets/Scripts/AtomIO/Meta/MetaProgress.cs` | PlayerPrefs save: element, energy bar progress, tutorial stage; bar full = evolve, bar to 0. |
| `Assets/Scripts/AtomIO/Player/ElectronOrbit.cs` | Bohr shells (2 then 8: Neon fills both), slot cap = Z, keep >= 1, aim line, release. |
| `Assets/Scripts/AtomIO/Player/PlayerAtom.cs` | Player nucleus: movement, HP = 10 x Z, trait multipliers, core colours. |
| `Assets/Scripts/AtomIO/UI/ArenaHud.cs` | Top-left HUD (HP bar, energy bar), evolve banner and tutorial hints. |

## Level Designer: level configs

- 10 level files (H..Ne) + `tier_fallback.json` + `index.json` in `Assets/Resources/AtomIO/Tiers/`
- Design sources: 10 designed levels, 0 fallback
  - H: Proton Nursery
  - He: Balloon Lift
  - Li: Battery Pit
  - Be: Featherframe
  - B: Green Flame
  - C: Diamond Mine
  - N: Triple Bond
  - O: Flame Feeder
  - F: Electron Thief
  - Ne: Neon Sign

## Lead Architect: asset decisions

- Approved: Kenney Particle Pack
- Approved: Kenney Mobile Controls
- Approved: Kenney UI Pack
- Approved: Kenney Fonts
- Approved: Kenney Sci-fi Sounds
- Approved: Kenney Interface Sounds
- Approved: Unity Universal Render Pipeline
- Approved: TextMesh Pro (in uGUI)
- Rejected: Joystick Pack (Fenerax Studios) (Duplicates our own 60-line uGUI TouchStick; Kenney Mobile Controls already gives the sprites.)
- Rejected: Kenney Space Shooter Redux (Spaceship art clashes with the abstract Bohr-model style; enemies will be procedural circles.)
