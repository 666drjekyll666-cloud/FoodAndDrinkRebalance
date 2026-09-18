# Changelog

Only accepted user-facing releases are listed as stable.

## 1.2.1 — accepted stable release

- Fixed a Well Fed actor-context sequencing bug in the manual-crafting Harmony prefix: the hook now uses the current `CraftComponent.DoAction(WorldGameObject, float, bool)` `other_obj` argument instead of reading a field that can still contain null/stale state before the original method runs.
- Narrowed the patch resolver to the exact Graveyard Keeper 1.407 `DoAction(WorldGameObject, float, bool)` overload.
- Preserved the accepted x2.00 Well Fed multiplier, `delta_time` acceleration mechanism, total craft-energy semantics, gameplay scope/exclusions, food/alcohol balance, custom buffs, timers, and localization.
- Runtime diagnostics confirmed unbuffed manual craft remains x1, Well Fed manual craft receives x2, the same craft performed by a zombie is not accelerated, and tested berry/garden world-resource gathering runs outside the player craft-speed hook.

## 1.2.0 — accepted public release

- Renamed the public project and DLL to **Food & Drink Rebalance** / `FoodAndDrinkRebalance.dll`.
- Preserved the existing BepInEx GUID for plugin identity and compatibility.
- Preserved the accepted 1.1.6 gameplay behavior; no balance or runtime mechanic changes were introduced by the migration.
- Established the clean public repository as the stable project home.

## 1.1.6 — accepted legacy stable baseline

- Raised Omelette's Well Fed manual-workstation progression multiplier from x1.50 to x2.00.
- Kept Well Fed duration, eligibility, and vanilla total craft-energy accounting unchanged.
- Retained the accepted 1.1.5 alcohol/egg redesign and all prior food/recipe balance behavior.

## 1.1.5 — accepted legacy stable

- Fried Egg: 15 Energy + one-dose Sobering.
- Boiled Egg: plain 20 Energy.
- Omelette: 30 Energy + Well Fed.
- Wine: 30/40/50 Energy +30 HP, Inebriated, Wine Master totals 36/48/60.
- Mead: 50/60/70 pure Energy, no HP recovery, Inebriated, Wine Master totals 60/72/84.

## 1.0.0 — accepted legacy public milestone

- Promoted the accepted food/recipe rebalance, Green Jelly Speed, localized visible buffs, and Well Fed production-speed behavior.
