# CHANGES (20261101-232949, mock run)

Gate status: **READY FOR REVIEW** (0 errors, 1 warnings)

## Lead Architect: core gameplay scripts

Generated against Unity 6.3 LTS. No external packages and no networking.

| File | Purpose |
|---|---|
| `Assets/Scripts/AtomIO/Arena/ArenaSession.cs` | Run lifecycle, time and energy tracking, return to lobby. |
| `Assets/Scripts/AtomIO/Arena/ArenaSpawner.cs` | Inverse-time spawning of enemies, orbs and wild electrons. |
| `Assets/Scripts/AtomIO/Arena/EnergyOrb.cs` | Energy pickup. |
| `Assets/Scripts/AtomIO/Combat/Electron.cs` | Wild / orbiting / released states, 10 and 20 damage, pickup. |
| `Assets/Scripts/AtomIO/Combat/Enemy.cs` | Chase in sight range, give up at 1.5x range, contact damage, orb drop. |
| `Assets/Scripts/AtomIO/Controls/PlayerInputBridge.cs` | Connects sticks to the player and orbit. |
| `Assets/Scripts/AtomIO/Controls/TouchStick.cs` | uGUI virtual joystick (move / aim). |
| `Assets/Scripts/AtomIO/Core/AtomFormulas.cs` | GDD constants and formulas (HP, slots, evolution cost, failure chance, pity). |
| `Assets/Scripts/AtomIO/Data/TierConfig.cs` | Serializable mirror of tier_NNN.json and the spawn/energy/HP curves. |
| `Assets/Scripts/AtomIO/Data/TierConfigLoader.cs` | Loads tier JSON from Resources with fallback; zero network calls. |
| `Assets/Scripts/AtomIO/Meta/LobbyController.cs` | Lobby UI: evolve with failure chance, forced first evolution, start run. |
| `Assets/Scripts/AtomIO/Meta/MetaProgress.cs` | PlayerPrefs save: element, banked energy, pity counter, tutorial stage. |
| `Assets/Scripts/AtomIO/Player/ElectronOrbit.cs` | Bohr shells (2/8/18/32), slot cap = Z, keep >= 1, aim line, release. |
| `Assets/Scripts/AtomIO/Player/PlayerAtom.cs` | Player nucleus: movement, HP = 10 x Z, trait multipliers, core colours. |
| `Assets/Scripts/AtomIO/UI/ArenaHud.cs` | Top-left HUD and tutorial hints. |

## Level Designer: tier configs

- 118 tier files + `tier_fallback.json` + `index.json` in `Assets/Resources/AtomIO/Tiers/`
- Design sources: 102 by group, 7 landmark overrides, 9 fallback

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
