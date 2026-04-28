# Bonus Only Businesses Mod (High Seas, High Profits)

A total economic overhaul mod for **High Seas, High Profits** that introduces the **Regional Mandate**. This mod transforms the game into a deep logistical challenge by forcing cities to only build and manage industries for which they have specific specialized bonuses.

## Features

### 1. The Regional Mandate
Cities can no longer build any business they want. The build menu is dynamically filtered to only show goods that match the city's unique bonuses. If a city doesn't have a license for a good, it cannot produce it.

### 2. Expanded Specialization
The mod increases the number of specialized bonuses per city from the vanilla 3 to **5** (configurable). This provides more strategic depth while maintaining strict regional production limits.

### 3. Smart Mayor AI
The Mayor's AI has been completely rewritten to respect the Mandate:
* **Goal Filtering:** Mayors will only generate "Build Business" goals for goods the city specializes in.

### 4. World Sanitizer & Jumpstarter
* **New Game Support:** Injects extra bonuses during world generation and scrubs any non-bonus buildings placed by the game's default generator.

## Installation

1. Ensure you have **MelonLoader** installed for *High Seas, High Profits*.
2. Download the `Bonus_Only_Businesses.dll`.
3. Place the DLL into your game's `Mods` folder.
4. Launch the game.

## Configuration

You can adjust the level of specialization by changing the `MaxBonusCount` variable in the source code, but it
will only change after a new game is made:
```csharp
public static int MaxBonusCount = 5; // Change this to increase/decrease bonus goods
```
