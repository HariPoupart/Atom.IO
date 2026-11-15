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
