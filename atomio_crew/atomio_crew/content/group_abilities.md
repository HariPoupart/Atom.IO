_Status: **approved** after 2 round(s). GDD 1.4.4/1.4.5 bonus retention feature: each element group plays differently._

# Element-group abilities (GDD 1.4.5)

| Group | Ability | Trigger | Effect | Cooldown | Description | Based on |
|---|---|---|---|---|---|---|
| reactive nonmetal | **Covalent Link** | on_orbit_hit | chain_hit = 1 | 4s | Every 4 s, an orbit hit arcs to 1 more nearby enemy, like a shared bond. | Nonmetals share electrons in covalent bonds |
| noble gas | **Full Shell** | on_low_hp | shield_seconds = 2 | 20s | Drop below 30% HP and your full shell blocks contact damage for 2 s. | A full outer shell makes noble gases unreactive |
| alkali metal | **Violent Reaction** | on_release | damage_mult = 1.3 | 6s | Every 6 s, your next thrown electron hits 30% harder. | One loose outer electron makes alkali metals react violently |
| alkaline earth metal | **Mineral Mend** | on_pickup | heal_hp = 5 | 8s | Grabbing an energy orb restores 5 HP, at most once every 8 s. | Calcium and magnesium are building blocks of bone and minerals |
| metalloid | **Semiconductor** | passive | pickup_radius_mult = 1.2 | 0s | Pull in orbs and loose electrons from 20% farther away. | Metalloids conduct partway, like silicon in chips |
| halogen | **Electron Hunger** | on_kill | energy_mult = 1.25 | 0s | Enemies you destroy drop 25% more energy. | Halogens are one electron short and grab them eagerly |
| post transition metal | **Malleable Guard** | on_low_hp | heal_hp = 15 | 25s | Once every 25 s, dropping below 30% HP restores 15 HP. | Soft metals like tin and lead bend instead of breaking |
| transition metal | **Catalyst** | passive | orbit_speed_mult = 1.15 | 0s | Your electrons orbit 15% faster, sweeping enemies more often. | Transition metals speed up reactions as catalysts |
| lanthanide | **Magnetic Field** | on_orbit_hit | slow_enemies = 0.7 | 5s | Every 5 s, an orbit hit slows that enemy to 70% speed for a moment. | Rare earths like neodymium make the strongest magnets |
| actinide | **Half-Life** | on_kill | chain_hit = 2 | 10s | Every 10 s, a destroyed enemy releases radiation that hits 2 nearby enemies. | Actinides are radioactive and release energy as they decay |
| unknown properties | **Island of Stability** | passive | move_speed_mult = 1.1 | 0s | Properties unknown, momentum isn't: you move 10% faster. | Superheavy atoms may sit near a predicted island of stability |
