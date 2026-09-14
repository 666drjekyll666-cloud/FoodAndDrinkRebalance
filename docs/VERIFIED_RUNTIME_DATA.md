# Food & Drink Rebalance — Verified Runtime Data

This file records Graveyard Keeper 1.407 internals relied on by production. Do not infer IDs/schema from display names; establish new facts before using them in runtime code.

## Baseline data

The original read-only diagnostic exposed 2,102 `CraftDefinition` rows, 1,157 `ItemDefinition` rows, 34 `BuffDefinition` rows, and 187 `TechDefinition` rows.

Recipe ingredients are in `CraftDefinition.needs`; outputs are in `CraftDefinition.output`. Food energy may be represented both in `params_on_use` and `on_use_expressions`; production changes keep both synchronized where applicable. Accepted beet-slice HP uses `params_on_use._hp`.

## Localization coverage contract

Production localization is event-bound and uses the game's active language state rather than polling:

- `GameSettings._cur_lng` is read as the current language code; the production helper lowercases it and normalizes `-` to `_`.
- `GJL.LoadLanguageResource` is patched with a postfix so custom strings are re-injected after the game loads or changes a language.
- The custom strings are written into the active `GJL.cur_lng.dict` dictionary.
- Graveyard Keeper exposes 11 supported interface languages. Current production source covers all 11: English (`en`, default fallback), French (`fr`), German (`de`), Simplified Chinese (`zh_cn`, including normalized `zh-cn`), Spanish (`es`), Brazilian Portuguese (`pt_br`, including normalized `pt-br`), Korean (`ko`), Japanese (`ja`), Russian (`ru`), Italian (`it`), and Polish (`pl`).
- `Well Fed`, `Inebriated`, `Sobering`, and the generic `Speed` name have per-language strings for the ten non-English languages plus English fallback.
- Green Jelly's Speed description preferentially reuses the current-language vanilla `pot_speed_d` string, then `buff_pot_speed_d`, with English only as a final missing-key fallback.

Historical accepted player evidence verified the localization injection path and visible custom-buff presentation in Russian. The all-11 statement is a static source-coverage guarantee, not a claim that every translation has been separately native-speaker or in-game QA tested.

## Alcohol IDs and vanilla contract

### Red wine

| Quality | Item ID | Vanilla base | Wine Master expression |
| --- | --- | --- | --- |
| q1 | `bottle_red_vine:1` | 60 Energy, 30 HP | `AddPpar("energy", 25*Ppar("p_wine_master"))` |
| q2 | `bottle_red_vine:2` | 72 Energy, 30 HP | `AddPpar("energy", 30*Ppar("p_wine_master"))` |
| q3 | `bottle_red_vine:3` | 84 Energy, 30 HP | `AddPpar("energy", 30*Ppar("p_wine_master"))` |

The historical guesses `wine_1`, `wine_2`, `wine_3` are invalid and must not be reused.

### Mead

| Quality | Item ID | Vanilla base | Wine Master expression |
| --- | --- | --- | --- |
| q1 | `cup_mead:1` | 12 Energy, 5 HP | `AddPpar("energy", 5*Ppar("p_wine_master"))` |
| q2 | `cup_mead:2` | 18 Energy, 5 HP | `AddPpar("energy", 8*Ppar("p_wine_master"))` |
| q3 | `cup_mead:3` | 24 Energy, 5 HP | `AddPpar("energy", 10*Ppar("p_wine_master"))` |

Production stable behavior validates all six Wine/Mead targets and their expected old expressions before applying the alcohol module.

## Buff and movement contract

- Vanilla speed-potion buff: `buff_pot_speed`.
- Verified resource: additive `speed_buff = 1.5`.
- Green Jelly custom effect: `gkfr_speed_food`, same +1.5 resource, shorter duration, `b_run` icon.
- Well Fed production effect: `gkfr_wellfed`, `b_hammer` icon.
- Accepted Well Fed multiplier: x2.00 for eligible Keeper manual workstation progression. Exact prior internal-energy testing proved that changing only `CraftComponent.DoAction` `delta_time` preserves total craft-energy cost.
- Inebriated: `gkfr_inebriated`, `b_drunk` icon, additive `speed_buff = -0.33`.
- Verified normal walking speed is 3.3; native movement adds `speed_buff` directly, so -0.33 produces 2.97 (90% of normal speed).
- Positive Speed and Inebriated combine additively through the native `speed_buff` path; no recurring movement hook is required.

## Active buff/timer contract

Verified Graveyard Keeper 1.407 runtime primitives:

- `PlayerBuff.end_time` is authoritative active-duration state.
- `BuffsLogics.AddBuff(string, float?)` adds duration to an existing additive-overlay buff rather than multiplying its resource strength.
- `BuffsLogics.FindBuffByID(string)` resolves the active buff.
- `BuffsLogics.RemoveBuff(string)` removes the active buff, subtracts its resources, and refreshes UI safely.
- `MainGame.game_time` is the current time basis used with `end_time`.

`AddBuff` converts a source length by `length / 450 * 60`. Inebriated source length is 1, so one alcohol dose is exactly `60 / 450` in `end_time` units. Fried Egg Sobering subtracts one such dose; if one dose or less remains, it uses native `RemoveBuff`.

## Accepted production balance facts

- Fried Egg: 15 Energy + one-dose Sobering.
- Boiled Egg: plain 20 Energy.
- Omelette: 30 Energy + sole egg-family Well Fed source.
- Wine: 30/40/50 Energy, +30 HP; Wine Master totals 36/48/60; one Inebriated dose.
- Mead: 50/60/70 Energy, no HP; Wine Master totals 60/72/84; one Inebriated dose.
- Beer remains unchanged.

## Timing compatibility

Longer Days has been verified to stretch vanilla and custom buff timers through normal game timing. Food & Drink Rebalance follows vanilla timing semantics and does not independently compensate durations.
