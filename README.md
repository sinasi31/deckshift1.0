# DeckShift

DeckShift is a 2D roguelike deckbuilder platformer built in Unity, centered around a constrained movement system called **Shift**.

Each jump consumes one Shift, turning movement into a limited resource and forcing players to carefully plan traversal, combat, and reward collection.

---

## 🎮 Core Features

- **Shift System**
  - Every jump consumes 1 Shift
  - Shift carries over between levels
  - Ending a level with low Shift increases future difficulty

- **Deckbuilding + Platforming**
  - Cards are used for both combat and traversal
  - Players add one card to their deck at the end of each level

- **Risk–Reward Level Design**
  - Optional rewards such as slot machines and Shift Crystals
  - Limited Shift prevents collecting everything in a single run

- **Roguelike Structure**
  - 3 Acts (planned)
  - 10 levels + 1 boss per Act
  - Shops, enemies, and permanent run upgrades

---

## 🃏 Highlighted Mechanics

### Portal Card
- Cost: 2
- Limited range
- Allows players to:
  - Interact with distant rewards
  - Fire projectiles through the portal
  - Teleport to the portal location later

Designed to provide strong utility without fully bypassing core constraints.

### Create Platform Card
- Spawns a temporary platform at the cursor position
- Helps players recover from risky jumps without trivializing level design

---

## 🎰 Slot Machines

- Each slot machine has 3 reels
- Reels contain numbers from 1 to 7 and one Skull symbol
- If any Skull appears, no item is awarded
- Otherwise, reel values are summed to determine item rarity
  - Low total → Common
  - Medium total → Rare
  - High total → Epic
  - 7-7-7 → Legendary (Jackpot)

---

## 🧙 Blompo – Run-Based Upgrades

Blompo provides powerful upgrades that last only for the current run.
Accessing these upgrades always requires card usage, ensuring deckbuilding remains essential.

Example Upgrade:
- Makes a random card in the player’s hand have infinite charges

---

## 🛠️ Development Notes

- Engine: Unity
- Language: C#
- Project Status: Active prototype
- Visuals are placeholder; focus is on gameplay systems and player decision-making

---

## 📌 How to Run
1. Clone the repository
2. Open the project in Unity Hub
3. Open the main scene and press Play
