# GAME DESIGN DOCUMENT: ATOM.IO
By Hari Poupart, 10/18/2026. Course: Multi Agent AI for Game Development.
(Gameplay sections transcribed from the GDD PDF. Typos fixed.)
**Rescope (2026-11-12):** the game now ends at Neon (element 10), so there are 10 levels, one arena configuration per element. Evolution no longer has a cost or a failure chance: an energy bar fills as the player collects energy, and when it is full the player evolves to the next element and the bar empties to 0.

## 1.1 Core idea
This game, inspired by the Bohr atomic model, aims to create a game where the player starts off as the first element of the periodic table and aims to grow until they reach Neon, the tenth element and the first to fill two complete Bohr shells (2 + 8 electrons). This core progression concept is fused with the IO game style such as slither.io and archer.io to create a simple yet addictive single player mobile game (Android) with the emphasis on simple yet repeatable gameplay.

## 1.2 Scope
The time constraint due to university studies reduces the scope of the project to something producible in ~40 hours. By choosing an arcade inspired style, the project remains flexible to additional features, yet stays playable in early stages.

## 1.3.1 Core game mechanics
Player attack: Shoots an electron (electrons orbit the player). Only when there is more than 1 orbiting electron.
Player evolution: Moves up elements on the table of elements, which increases protons (+10 HP) and electron slots. Evolution happens automatically when the energy bar is full.
Player health: Directly proportional to the number of protons in the player's core and decreases when the player collides with enemies. Health starts at 10 and increases by 10 each time the player evolves.
Enemy attacks: Collide with the player core to inflict damage (-10 HP).
Enemy health: Collide with the player's orbiting (-10 HP) or released (-20 HP) electrons to take damage. Health starts at 10 and increases linearly by a set amount to be calibrated during testing.

## 1.3.2 Core game loop
The player starts as a hydrogen atom with one proton and electron in the lobby. When they press start they enter the arena for their current element with the goal to survive as long as possible and collect as much energy as possible. Energy is obtained by collecting energy orbs either spawned naturally, or dropped by a killed enemy. Every point of energy collected goes into the energy bar. When the bar is full, the player immediately evolves into the next element, right there in the arena, and the bar empties to 0 (energy beyond the bar's capacity is not carried over). Each element has its own level: its own arena configuration (theme, obstacle layout, spawn pressure) and its own look and trait for the player's core, so evolving mid-run also moves the player into the next level. Energy orbs, unclaimed electrons and enemies spawn randomly on the map and spawn times are inversely proportional to the time elapsed in the arena. Enemies are defeated when their health bar reaches 0. To deal damage to enemies the player can hit them with either their orbiting or released electrons. The player is damaged by enemies when they make contact with the player's core. After reaching 0 health points, the player is returned to the lobby. The energy bar keeps its progress between runs, so no energy is ever lost.
In short: run around the arena killing enemies and collecting energy, fill the bar, evolve, and keep climbing up the periodic table of elements to Neon.

## 1.3.3 Win/Loss condition
In the arena the player loses the run when their HP reaches 0 due to repeated contact with enemies. There is no clear winning condition in the arena, however the longer the player survives the more it has access to exponentially increasing energy. The longer the player survives and the higher the atomic number, the more HP the enemies have.
Overall, the player wins when they reach the final implemented element of the periodic table of elements (Neon, number 10). There are 10 levels in total, one per element from Hydrogen (1) to Neon (10).
Evolution: there is no evolution cost and no chance of failure. The energy bar's capacity is 50 + 3 x current atomic number (53 energy as Hydrogen, 77 as Fluorine). When the bar is full the player evolves to the next element and the bar empties to 0. Neon has no bar to fill: reaching it completes the game, and the player can keep playing Neon's arena for fun.

## 1.4.1 Player actions (Arena)
Player movement: The player controls movement with a virtual joystick on the left.
Player attack: The player releases electrons when the attack button on the right is released after aiming.
Object collection: The player collects energy orbs and electrons automatically by colliding with them. Electrons are collected only if the player hasn't already reached its max number of orbiting electrons, denoted by their atomic number, which is also their number of protons.

## 1.4.2 Player perception (Arena)
User interface (phone held horizontally): Top left: player info (current element, health bar, energy bar toward the next element and time survived). Top right: exit arena button and settings. Middle left: virtual joystick. Middle right: attack joystick.
Arena interactive objects: Enemies are randomly spawned far from the player and follow them when in sight range. Electrons are idle, spawned around the map. Energy orbs are idle, spawned around the map and dropped on enemy death.
What the player sees and does: The player explores the arena map where they find enemies, electrons and energy orbs. Enemies chase the player when they see them and give up when the player escapes. The player collects energy orbs, shoots electrons at enemies, collects wild electrons and repeats until they die.
The player sees their core as a large circle, with their electrons orbiting around it as shown by the Bohr model. When aiming, a drag vector appears on the latest electron obtained to help the player aim.
Balance note: The player always has at least 1 electron orbiting them. They can only release electrons if they have more than one.

## 1.4.3 Player tutorial
When the player enters the Arena the first time as a hydrogen, the first run serves as a tutorial to show that orbiting electrons damage enemies and that energy orbs fill the energy bar. Hydrogen's bar is the smallest (53 energy), so the first evolution into Helium comes quickly. The moment the player first becomes Helium, they are shown how to throw spare electrons and how to pick up spawned ones.

## 1.4.4 Player retention mechanism
The two main retention mechanisms are the evolution mechanism and the player's defense building mechanism. Early on, the player is evolving rapidly and progressing fast, which keeps the game interesting. The energy bar is always visible, so the next evolution is always in sight. As they climb the periodic table of elements, they gain access to more electron slots, filling the first shell (2) and then the second (8), up to 10 orbiting electrons at Neon. Building strong defenses serves as a retention goal for them to keep playing even when evolving is distant. The third retention mechanism is the bonus one, which is changing traits and abilities with evolution. This would largely improve the quality of the game.

## 1.4.5 Bonus features
To keep the player interested in evolutions, elements or groups of elements should get different abilities and appearances. With only 10 elements, every element gets its own designed level (arena configuration) and core look; abilities are given per element group (the 6 groups found from H to Ne).

## 3.1 Technical constraints
Android, landscape orientation, baseline device is a mid-range 2 GB RAM device. Unity 6.3 LTS. No runtime API calls: the shipped app makes zero network calls during gameplay. All agent work happens at editor time and content is pre-generated before the build.
