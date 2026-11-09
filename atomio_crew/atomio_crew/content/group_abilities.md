_Status: **approved** after 3 round(s). GDD 1.4.4/1.4.5 bonus retention feature: each element group plays differently._

# Element-group abilities (GDD 1.4.5)

| Group | Ability | Trigger | Effect | Cooldown | Description | Based on |
|---|---|---|---|---|---|---|
| reactive nonmetal | **Shared Bond** | on_pickup | heal_hp = 5 | 8s | Grab a wild electron and share the bond: heal 5 HP. Ready again after 8 seconds. | Nonmetals like carbon share electrons in covalent bonds |
| noble gas | **Discharge Glow** | passive | pickup_radius_mult = 1.2 | 0s | Your glow draws loose electrons and energy orbs in from 20% farther away. | Noble gases glow brightly when excited in discharge tubes |
| alkali metal | **Chain Reaction** | on_kill | chain_hit = 2 | 6s | Kill an enemy and the blast leaps to 2 nearby foes. Ready again after 6 seconds. | Alkali metals react violently with water, releasing sudden energy |
| alkaline earth metal | **Flare Burst** | on_release | damage_mult = 1.15 | 8s | Your next thrown electron flares for 15% extra damage. Ready again after 8 seconds. | Magnesium burns with a dazzling white flame |
| metalloid | **Doped Gains** | passive | energy_mult = 1.15 | 0s | Doped for flow: you collect 15% more energy from every orb. | Doping silicon with impurities boosts its electrical conductivity |
| halogen | **Sanitize** | on_kill | heal_hp = 10 | 12s | A kill sprays disinfectant and heals 10 HP. Ready again after 12 seconds. | Chlorine and iodine are widely used as disinfectants |
| post transition metal | **Oxide Skin** | on_low_hp | shield_seconds = 1.5 | 30s | At low HP, a thin shield blocks damage for 1.5 seconds. Ready again after 30 seconds. | Aluminium forms a thin protective oxide layer on its surface |
| transition metal | **Catalyst Rush** | on_pickup | move_speed_mult = 1.2 | 10s | Grab an electron and speed up 20% for a moment. Ready again after 10 seconds. | Transition metals such as platinum and iron act as catalysts |
| lanthanide | **Phosphor Flash** | on_kill | energy_mult = 1.3 | 5s | A kill drops 30% more energy. Ready again after 5 seconds. | Europium and terbium are used in glowing screen and lamp phosphors |
| actinide | **Decay Chain** | on_orbit_hit | chain_hit = 1 | 10s | An orbiting electron hit sets off a decay chain to 1 more enemy. Ready again after 10 seconds. | Uranium and thorium decay through a series of radioactive steps |
| unknown properties | **Fleeting Flux** | on_release | slow_enemies = 0.8 | 10s | Thrown electrons slow enemies to 80% speed for a moment. Ready again after 10 seconds. | Superheavy atoms are made a few at a time and decay in moments |
