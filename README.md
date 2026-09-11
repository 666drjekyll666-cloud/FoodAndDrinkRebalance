# Food & Drink Rebalance

A focused, vanilla-friendly balance mod for **Graveyard Keeper 1.407**. It corrects selected food and recipe outliers and gives several foods and drinks clearer gameplay roles without replacing the cooking system.

## Main changes

- selected weak or overly efficient food recipes are rebalanced;
- **Fried Egg**: 15 Energy + **Sobering**, removing one Inebriated duration dose;
- **Boiled Egg**: plain 20 Energy;
- **Omelette**: 30 Energy + **Well Fed**, the egg family's production-speed food;
- **Wine**: 30/40/50 Energy +30 HP, with **Inebriated**;
- **Mead**: 50/60/70 pure Energy, no HP recovery, also with Inebriated;
- **Well Fed** doubles eligible Keeper manual-workstation progression without reducing total craft energy;
- **Green Jelly** gains a short vanilla-strength Speed effect;
- built-in localization for the game's supported interface languages.

Detailed values and design rationale are recorded in `docs/BALANCE_DESIGN.md`.

## Requirements

- Graveyard Keeper 1.407
- BepInEx 5.x (tested with 5.4.23.5)

## Installation

Copy `FoodAndDrinkRebalance.dll` into:

`Graveyard Keeper/BepInEx/plugins/`

Restart the game.

## Compatibility

Mods that change the same food/recipe records or the same manual crafting-time path may overwrite or compound these changes. Longer Days is compatible; timed food and alcohol effects follow the game's normal timer scaling.

## Status

Current stable version: **1.2.0**.

## Development

- Canonical project: `FoodAndDrinkRebalance.csproj`
- Runtime source: `src/GKFoodRebalancePlugin.cs`
- Verified runtime facts: `docs/VERIFIED_RUNTIME_DATA.md`
- Build/test history: `docs/TEST_BUILD_LOG.md`
- Project rules: `AGENTS.md`
