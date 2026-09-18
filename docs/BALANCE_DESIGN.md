# Food & Drink Rebalance — Balance Design

This document records the accepted gameplay target for Graveyard Keeper 1.407. It is not a release changelog.

## Core principles

- Give changed foods a clear gameplay role rather than normalizing everything to identical efficiency.
- Count direct Keeper preparation energy, fuel, preprocessing, station time, and interaction cost separately.
- Preserve useful vanilla niches and specialist buffs.
- Prefer existing effects/icons and native UI/timer behavior.
- Complex recipes should visibly reward their extra ingredients and processing.
- Avoid broad hunger/fullness systems, mandatory grind, or unrelated economy changes.

## Accepted food changes

- **Grated carrot:** 2 carrots -> 1 serving, 30 Energy. Fast no-fuel convenience food.
- **Carrot cutlets:** 3 carrots -> 4 cutlets, 15 Energy each. More crop-efficient oven route.
- **Grated beetroot:** 2 beets -> 1 serving, 30 Energy; refugee-camp duplicate uses the same requirement.
- **Beet slices:** 2 beets -> 2 servings, 20 Energy +10 HP each.
- **Fried Egg:** 15 Energy + Sobering; one serving removes exactly one Inebriated duration dose.
- **Boiled Egg:** plain 20 Energy, no custom buff.
- **Omelette:** 30 Energy + Well Fed; sole egg-family Well Fed source.
- **Green Jelly:** 30 Energy + normal vanilla-strength Speed for a short nominal duration.
- **Red Jelly:** 45 Energy x3.
- **Sandwich:** 40 Energy x3.
- **Croissant:** 35 Energy x4.
- **Berry-juice pie:** 60 Energy x2.
- **Creamy vegetable soup:** 20/25/30 Energy x4; existing Slow Metabolism retained.
- **Toasts:** 24/32/40 Energy x3; Beer Thirst retained.
- **Burger:** 50/60/70 Energy x4; Steady Hand retained.
- **Honey cake:** 55 Energy each; output increased from 1 to 2; Circumspect retained.

## Well Fed

- x2.00 faster eligible Keeper manual workstation progression.
- Total Keeper energy required to finish the same craft remains vanilla; the accepted implementation accelerates progression only.
- Uses one visible `gkfr_wellfed` row with `b_hammer`.
- Repeated Omelettes extend duration rather than stacking strength.
- Does not accelerate passive/zombie production, gardens/growth, ordinary world-resource tool actions, or unsafe repair paths.
- Follows normal game buff timing; Longer Days therefore stretches its timer naturally.

## Green Jelly Speed

- Uses vanilla-strength `speed_buff = +1.5`, icon `b_run`, with a shorter source duration than the potion.
- Generic localized effect name (`Speed`, `Скорость`, etc.) avoids describing the food as a potion.
- Repeated Green Jellies extend one effect row.

## Alcohol and egg roles

### Wine

- Base Energy: 30/40/50 by quality.
- +30 HP remains.
- Wine Master additions: +6/+8/+10, totals 36/48/60.
- Applies one visible Inebriated dose using `b_drunk` and `speed_buff = -0.33`.
- Repeated Wine extends one Inebriated timer; severity does not stack.
- Role: compact Energy + HP alcohol with a movement tradeoff.

### Mead

- Base Energy: 50/60/70 by quality.
- No HP recovery.
- Wine Master additions: +10/+12/+14, totals 60/72/84.
- Applies the same one-dose Inebriated effect as Wine.
- Role: later concentrated pure-energy alcohol, distinct from Wine's healing versatility.

### Fried Egg Sobering

- Each Fried Egg removes exactly one alcohol-duration dose from active Inebriated.
- If one dose or less remains, the remaining effect is removed natively.
- If sober, the egg is simply a 15-Energy food.

### Unchanged adjacent systems

Beer, Apple/Berry Ferment, Booze, tavern economy/quests, and alcohol production recipes remain outside this narrow player-consumption rebalance.

## Dominance checks

- Grated vegetables trade crop efficiency for no-fuel convenience.
- Carrot cutlets remain meaningfully more crop-efficient than grated carrot.
- Beet slices remain distinct through cooking plus HP recovery.
- Fried Egg is utility-focused; Boiled Egg is simple energy; Omelette is work-session utility.
- Green Jelly stays short-duration so Speed Potion retains long-travel value.
- Well Fed must not reduce total craft energy or steal unrelated field-tool/passive-production niches.
- Wine remains more versatile through HP; Mead offers more Energy per use without healing.
- Sobering costs one serving per alcohol dose, keeping Inebriated a meaningful tradeoff.

## Stable baseline

Public **1.2.1** is the accepted stable gameplay/runtime line for Graveyard Keeper 1.407. It preserves the accepted 1.1.6/1.2.0 balance values and Well Fed x2.00 design. The 1.2.1 change is an engineering correctness fix to Well Fed actor-context resolution; it does not change the intended balance, duration, eligibility policy, or total craft-energy semantics. See `docs/POST_AUDIT_1.2.1.md` for the closed audit record and runtime acceptance evidence.
