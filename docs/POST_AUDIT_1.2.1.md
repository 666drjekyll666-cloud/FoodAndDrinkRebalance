# Food & Drink Rebalance 1.2.1 — audit closure

Date: 2026-09-19  
Status: **accepted stable engineering result**

This document closes the post-1.2.0 engineering audit that led to 1.2.1. It is intended to prevent future reviews from reopening already-investigated questions without new evidence. Future audits should start here, then consult `docs/VERIFIED_RUNTIME_DATA.md`, `docs/BALANCE_DESIGN.md`, and `docs/TEST_BUILD_LOG.md`.

## Executive outcome

The audit found **one proven runtime correctness defect** in the Well Fed crafting hook. It was fixed narrowly in 1.2.1 and verified in Graveyard Keeper 1.407.

No balance redesign was required. The accepted x2.00 Well Fed multiplier, the `delta_time` acceleration seam, total craft-energy semantics, food/alcohol balance, custom buffs, timer arithmetic, and localization architecture were retained.

The audit also identified several non-blocking hardening opportunities. They are recorded below as future work, not as known player-facing defects.

## Proven defect: stale actor context in the Well Fed prefix

### 1.2.0 behavior

The Well Fed Harmony prefix patched `CraftComponent.DoAction`, but its classifier obtained the actor by reflectively reading `CraftComponent.other_obj`.

Graveyard Keeper 1.407 IL proves the exact method is:

`CraftComponent.DoAction(WorldGameObject other_obj, float delta_time, bool for_gratitude_points)`

The original method checks the current `other_obj` argument and only later assigns:

`this.other_obj = other_obj`

Because a Harmony prefix runs before the original method body, the field can still be null or contain the previous call's actor when the prefix executes. The 1.2.0 hook therefore had a real call-context sequencing bug.

Possible consequences included a false negative for a legitimate player craft or a stale-context false positive on a non-player call.

## Accepted 1.2.1 fix

The production change is deliberately narrow:

1. resolve only the exact `DoAction(WorldGameObject, float, bool)` overload;
2. inject the current call's `other_obj` argument into the Harmony prefix;
3. pass that current actor directly into the existing manual-player classifier;
4. remove the classifier's read of `CraftComponent.other_obj`;
5. keep all accepted gameplay rules and the x2.00 `delta_time` multiplier unchanged.

A focused source regression test in `tests/verify_craft_prefix.py` guards the exact overload, current-argument injection, absence of the stale field read, unchanged x2 multiplier, and unchanged `delta_time` acceleration.

## Why `delta_time` was kept

The audit rechecked whether Well Fed should be implemented through a different craft coefficient hook.

The existing Graveyard Keeper 1.407 IL shows the same `delta_time` reaches:

- `TrySpendPlayerEnergy(other_obj, delta_time)`;
- `SpendPlayerSanity(other_obj, delta_time)`;
- craft progress, as `k * delta_time / craft_time`.

`TrySpendPlayerEnergy` computes the current energy slice proportionally to `delta_time / craft_time`.

Earlier accepted exact-energy testing measured the same `wooden_plank` total cost with and without Well Fed: 5 Energy in both cases. Therefore scaling `delta_time` accelerates real-time completion while preserving total vanilla craft Energy.

A `GetCraftCoeffForPlayer`-only multiplier would not be equivalent: it would increase progress without increasing the per-slice energy path and would discount total craft Energy.

**Closed conclusion:** keep the `delta_time` seam unless new runtime evidence contradicts this contract.

## Runtime acceptance of 1.2.1

A research-only diagnostic build was made from the 1.2.1 production code path. It added bounded decision logging only; it did not define a new gameplay implementation.

Accepted runtime evidence on 2026-09-19 showed:

| Scenario | Observed result |
| --- | --- |
| `wooden_plank_3` at `mf_workbench_2`, no Well Fed | eligible; multiplier 1.00; `delta_time` 0.0101 -> 0.0101 |
| Same craft, Well Fed active | eligible; multiplier 2.00; `delta_time` 0.0085 -> 0.0170 |
| `flour_from_wheat` at `cooking_table_2`, Well Fed active | eligible; multiplier 2.00; `delta_time` 0.0083 -> 0.0167 |
| Zombie performing `wooden_plank_3` | non-player; multiplier 1.00; not accelerated |
| Zombie mine production | non-player; not accelerated |
| Refugee hive/well production | non-player; not accelerated |
| Berry gathering | direct gather runs through the game's zero-HP world-resource activity path, outside the player craft-speed hook |
| Carrot/wheat harvesting | same outside-hook world-resource activity pattern |
| Mushroom gathering | direct gather outside the player craft-speed hook; later respawn craft is non-player |

No Well Fed craft-speed hook error was emitted.

This closes the changed actor-context wiring. The diagnostic log did not re-measure total completed-craft Energy; that property remains covered by the earlier exact-energy test because 1.2.1 does not change the `delta_time` mechanism.

## Audited mechanisms and final verdicts

| Area | Audit result | Final 1.2.1 decision |
| --- | --- | --- |
| Prefix actor source | Proven sequencing bug | **Fixed**: use current `other_obj` argument |
| Harmony target resolution | Broad name-only resolution was unnecessarily weak | **Fixed**: exact `WorldGameObject, float, bool` overload |
| Well Fed x2 multiplier | No defect found | **Keep** |
| `delta_time` acceleration | Native path + exact-energy evidence support it | **Keep** |
| Zombie/passive worker exclusion | Verified with current actor in runtime | **Keep** |
| World-resource gathering | Tested berry/garden/mushroom gathering is outside the player craft-speed progress hook | **Keep architecture** |
| Existing scope guards / WGO blacklist | No accepted-scope regression proven | **Keep** |
| Shallow cloning of custom buff templates | No shared-mutation defect found; mutable nested data that is changed is separated first | **Keep** |
| Fried Egg one-dose Sobering via `PlayerBuff.end_time` | Native timer/removal contract and prior runtime tests support it | **Keep** |
| Localization postfix | Event-bound; no polling/reload defect found | **Keep** |
| Well Fed resource lookup reflection | Recurring cost exists, but no measured player-facing defect | **Defer; hardening only if justified** |

## Scope notes future audits should preserve

- Manual Keeper workstation crafts are accelerated x2 when Well Fed is active and otherwise eligible.
- Non-player workers are not accelerated.
- Tested berry, carrot, wheat, and mushroom gathering actions are direct world-resource actions outside the player craft-speed progress path; background respawn/production crafts observed afterward are non-player.
- Player auto/hidden craft paths return before native progress and therefore do not gain effective acceleration from the prefix's local `delta_time`.
- Recipe IDs containing `:r:` and the existing WGO-family exclusions remain unchanged policy.
- Manual removal/dismantling is **not generically excluded** by a semantic removal flag. It can remain eligible unless an existing exclusion matches. This was known during the audit and intentionally not changed because it is a gameplay-scope decision, not a proven regression.
- `for_gratitude_points=true` manual crafts remain eligible when otherwise allowed. This also remains an explicit design ambiguity rather than a correctness defect.
- Well Fed stores no per-craft state; interrupted/paused work does not leave mod-side craft-speed state behind.

Do not silently reinterpret the two design ambiguities above as bugs in a later audit. Reopen them only if gameplay design is intentionally being reconsidered.

## Remaining hardening backlog

These were investigated and **not release blockers** for 1.2.1:

### 1. Reflection cost in Well Fed resource lookup

`ReadNumericResource` can enumerate methods and allocate invocation arguments during relevant `DoAction` calls. This is the clearest remaining hot-path optimization opportunity.

No player-facing performance problem has been measured, and the preferred native/custom-resource getter has not yet been established as a safer production contract. Optimize only after measuring or after verifying a narrower getter/cache seam.

### 2. Numeric balance projection atomicity

The older `ApplyNumericBalance` path mutates several targets incrementally rather than prevalidating every expected old value before the first mutation. Under an unexpected modded baseline this could partially apply the older balance module.

This is a robustness gap, not an observed vanilla 1.407 defect. A future hardening pass may convert the older module to discover -> validate all -> apply -> verify.

### 3. Existing custom-buff definition validation

`EnsureCustomBuffs` can reuse existing `gkfr_*` definitions without fully validating their shape. A namespace collision or stale foreign definition could therefore escape fail-closed validation.

This is unlikely and unobserved, but it is a legitimate future hardening target.

### 4. Missing-definition/uninstall save behavior

Normal save/load with the mod installed relies on native `PlayerBuff` state and has accepted historical runtime evidence. Behavior when the mod is removed while a custom buff is active, or when a save contains the active buff but its definition is absent, remains an evidence gap.

Do not claim that uninstall edge case is proven until it is directly investigated.

## Performance / save-safety conclusion

1.2.1 adds no background worker, permanent scan, per-frame global enumeration, file/network polling, or production hot-path logging.

The stable Well Fed hook performs only the existing classification/resource lookup plus the x2 arithmetic when applicable. The temporary diagnostic logging used for acceptance is not part of the production candidate.

The 1.2.1 fix introduces no new serialized state and does not change custom buff definitions or timer storage.

## Canonical identities

- Accepted production candidate source: `c638e83cacb71a2f079e6acdc418e6064748a0fa`
- Frozen candidate: `candidate/1.2.1`
- Candidate CI run: `35402538631`
- Candidate artifact: `FoodAndDrinkRebalance-1.2.1` / artifact ID `10571102014`
- Accepted DLL: `Food & Drink Rebalance 1.2.1.dll`
- Accepted DLL SHA-256: `bc0550cdc0f11a690b86cf85e34303881b168bc1722c38e34c5a81cca860f84a`
- Research diagnostic source: `3b704ca7566361f2efe427ca263ab2a785c84456`
- Frozen diagnostic: `diagnostic/1.2.1-well-fed`
- Diagnostic CI run: `35405962554`
- Diagnostic DLL SHA-256: `0e8e03d89f17c272c2fbd356c678a5642a5beba71f118891f9dd2505604c58c5`

The diagnostic DLL is research-only and must not be published as the stable release.

## Final decision

**Accept 1.2.1 as the stable correction.**

The audit's correct endpoint is not a rewrite. It is the existing architecture plus the targeted current-actor fix and exact overload resolution.

A future audit should treat the conclusions above as established evidence. Reopen a closed item only when at least one of the following is true:

- Graveyard Keeper/runtime version changes;
- production code affecting that mechanism changes;
- new runtime evidence contradicts the recorded contract;
- a measured performance or compatibility symptom justifies revisiting a deferred hardening item;
- the gameplay design is intentionally changed.
