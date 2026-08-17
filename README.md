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
  - Cards have finite **charges**; a spent card exhausts and must be recovered with **scrap**
  - New cards come from chests, the shop and quest payouts

- **Risk–Reward Level Design**
  - Optional rewards are usually off the mandatory path, so reaching them costs Shift
  - A branching **run map** lets you plan a route: Skirmish / Fight / Elite, with the harder branches the only way to resupply

- **Contracts**
  - Quests taken at a pinned board in the hub, including **oaths** — streak contracts that ask you to give something up (no cards for three rooms, no Recall, a Shift budget per room)
  - They pay in things the shop doesn't sell: permanent max Shift, max HP, cards, scrap

- **Roguelike Structure**
  - 3 Acts (planned; **Act 1 "Oxidation District" prototype is playable**)
  - A run is: hub → a routed series of levels → an **Act boss** as the finale
  - Shops, enemies, quests, relics, and run upgrades

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

## 👹 Boss Encounters

Each Act ends with a boss. **Act 1's boss — the Moss Knight** — is implemented:

- Stays dormant until the player drops into the arena, then wakes with a cinematic (camera pan, ground pounds, roar).
- A four-move kit (Acid Cleave, Charge, Leap Slam, Slime/Acid Lob) built entirely from existing animations, driven by range + cooldown.
- Two damage avenues: your cards, and a lever-triggered **Crusher Trap** for when the deck runs dry — the crusher also bursts Shift crystals so the boss doesn't starve your core resource.
- A dedicated boss health bar, per-ability sound, and a **death celebration** that drops real, collectible gold and Shift crystals (and handles a mid-air kill by leaving the loot floating).

---

## 🔩 Scrap & the Forge

Killing enemies drops **scrap** — a separate currency from gold, and the only thing that repairs a deck.

- **Gold** comes from piles placed in levels and buys NEW power at the shop
- **Scrap** comes from kills and buys SUSTAIN: charges back onto cards you already own, and cards dragged out of the exhaust pile
- The two never mix. Maintenance always loses to acquisition when they share a wallet

---

## 🎲 Relics

Up to **5 relic slots**. Taking a sixth forces a sell-or-decline decision, so a loadout is curated rather than accumulated — 19 relics so far, offered by chests and the shop.

---

## 🧙 Blompo – Run-Based Upgrades

Blompo provides powerful upgrades that last only for the current run.
Accessing these upgrades always requires card usage, ensuring deckbuilding remains essential.

Example Upgrade:
- Makes a random card in the player’s hand have infinite charges

---

## 🛠️ Development Notes

- Engine: Unity 6 (URP, 2D renderer)
- Language: C#
- Project Status: Active prototype — Act 1 vertical slice (~14 playable cards, 19 relics, 10 combat levels + hub + boss room, one full boss)
- Art is pixel-art (Cainos asset packs) plus procedural, code-built effects (VFX, HUD chips, boss/chest bursts); focus is on gameplay systems and player decision-making

---

## 📌 How to Run
1. Clone the repository
2. Open the project in Unity Hub
3. Open the main scene and press Play
