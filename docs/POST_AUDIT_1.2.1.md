# Food & Drink Rebalance 1.2.1 — post-audit engineering pass

Date: 2026-09-19

This pass starts from the accepted 1.2.0 runtime source (`95c96cce8d1e0d3c9f5e3208bc4deb0bce652a40`) and current `main`. The six later `main` commits are repository/release documentation and workflow maintenance; they do not change `src/GKFoodRebalancePlugin.cs`.

## Research verdict

| Current mechanism | Problem proven? | Better seam? | Behavior difference | Runtime cost | Save impact | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Well Fed prefix reads `CraftComponent.other_obj` | **Yes.** GK 1.407 `DoAction` assigns `this.other_obj = other_obj` only after method entry; a Harmony prefix can therefore see null/stale actor state. | Inject the current `other_obj` argument into the prefix. | None intended; this corrects call context only. | Slightly cheaper: one reflective field read is removed. | None. | **Fix now.** |
| Well Fed multiplies `delta_time` by x2 | No correctness problem. Exact-energy runtime testing already proved the completed craft retains vanilla total Energy. GK IL also passes the same `delta_time` to energy/sanity and progress paths. | No narrower equivalent seam is proven. `GetCraftCoeffForPlayer` alone would accelerate progress without scaling the energy slice. | Changing to craft coefficient would discount completed-craft Energy and change accepted gameplay. | Negligible arithmetic. | None. | **Keep `delta_time`.** |
| Eligibility uses player check, `:r:` exclusion and bounded WGO-ID blacklist | No incorrect accepted case is proven. Native `DoAction` itself rejects player auto craft at entry, but the mod's additional scope exclusions encode accepted gameplay policy. | No verified semantic replacement currently covers the full accepted exclusion set. | A scope rewrite could expand/contract Well Fed unexpectedly. | Small string/LINQ cost per eligible call. | None. | **Keep scope unchanged in 1.2.1.** |
| Well Fed state is read through reflection-heavy fallback helpers | **Cost exists**, but no performance defect is proven. `ReadNumericResource` may enumerate methods and allocate argument arrays in the recurring craft path. Food & Drink Rebalance 1.2.0 did not reproduce the investigated ~0.7 s freeze class in isolation. | Cache verified bindings or use a verified native resource getter, once its exact contract for the custom resource is established. | Should be none, but an incorrect cached/native seam could make Well Fed undetectable. | This is the main remaining hot-path hardening opportunity. | None. | **Defer from the sequencing fix; targeted hardening candidate.** |
| Custom buffs use shallow template clones | No shared-mutation defect found in current code. Every mutable nested object that the mod changes is separated first: `length` is cloned; `res` is cloned; resource lists are replaced or cloned before mutation. | None needed for current fields. | None. | Startup only. | Native buff state only. | **Keep.** |
| Fried Egg reduces `PlayerBuff.end_time` by one dose and calls native `RemoveBuff` for the last dose | No defect found. Existing GK 1.407 runtime IL establishes `end_time` as authoritative timer state; native timer UI reads it; `RemoveBuff` removes save-list state, resource effect, finish expression and redraws UI. Accepted 1.1.5 runtime testing confirmed one-dose then final-dose behavior. | No native “subtract duration” API is known. | Replacing it would add complexity without proven benefit. | Event-only. | Uses native active-buff/save state. | **Keep.** |
| Localization postfix on `GJL.LoadLanguageResource` | No recurring-cost or reload defect found in accepted behavior. | None needed. | None. | Event-only. | None. | **Keep.** |

## Native sequencing evidence

Existing Graveyard Keeper 1.407 assembly IL in `SoulDLCRebalance-semantics-audit.txt` gives the exact method:

`CraftComponent.DoAction(WorldGameObject other_obj, float delta_time, bool for_gratitude_points)`

At method entry it checks `other_obj.is_player` and only later executes:

- IL `002C`: load `this`;
- IL `002D`: load current `other_obj` argument;
- IL `002E`: store `CraftComponent.other_obj`.

A Harmony prefix necessarily executes before those original instructions. The 1.2.0 prefix therefore had a real sequencing bug because its helper read `__instance.other_obj` before the original method refreshed it.

The same IL shows:

- player `GetCraftCoeffForPlayer(out k)` is resolved separately;
- `TrySpendPlayerEnergy(other_obj, delta_time)` receives the current `delta_time`;
- `SpendPlayerSanity(other_obj, delta_time)` receives the current `delta_time`;
- progress adds `k * delta_time / craft_time`.

`TrySpendPlayerEnergy` separately computes the energy slice as evaluated craft Energy multiplied by `delta_time / craft_time` (then by tool energy coefficient when applicable). This is why the accepted `delta_time` acceleration preserves total Energy across the shorter real-time completion.

## Well Fed scope trace

The current scope is now explicit:

| Path | Current 1.2.1 behavior | Evidence / reason |
| --- | --- | --- |
| Manual Keeper craft at a normal production station | **Accelerated x2** when Well Fed is active. | Current `other_obj` is the player, craft is present, and no exclusion matches. |
| Player-side auto craft | **Not accelerated in effect.** | Native `DoAction` returns before progress for player `current_craft.is_auto`; the prefix may execute first, but the changed local `delta_time` is then unused by the original call. |
| Hidden player craft | **Not accelerated in effect.** | Native `DoAction` similarly returns before progress for a hidden player craft. |
| Zombie / linked worker / other non-player worker | **Not accelerated.** | The prefix now classifies the current call's `other_obj`; non-player actors fail the `is_player` check. This is the stale-context regression fixed in 1.2.1. |
| Refugee/remote worker craft | **Not accelerated when actor is non-player.** | Same current-actor check. |
| Recipe ID containing `:r:` | **Explicitly excluded.** | Existing accepted recipe-ID guard is unchanged. |
| Garden / planting and listed world-resource families | **Explicitly excluded by current WGO-ID fragments.** | Existing blacklist is unchanged; it remains heuristic rather than a proven universal semantic flag. |
| Manual removal / dismantling | **Not universally excluded.** | Native `GetCraftCoeffForPlayer` has an explicit removal path with coefficient 1. The mod has no generic `wgo.is_removing` exclusion, so a manual removal action can be accelerated unless its WGO ID hits an existing exclusion. Historical `destroy_wd_fence` testing demonstrated this class. |
| Gratitude-point manual craft | **Currently accelerated if otherwise eligible.** | When `for_gratitude_points=true`, native `DoAction` sets craft coefficient 0.125 and skips player Energy/Sanity, but still advances progress with `delta_time`. The mod does not exclude this flag. The accepted balance docs do not separately define gratitude-craft policy, so changing it would be a gameplay-scope decision rather than a correctness fix. |
| Tool-required manual craft | **Accelerated x2.** | Native `GetCraftCoeffForPlayer` resolves tool coefficient/efficiency; Well Fed scales the later shared `delta_time`. |
| Manual craft without a tool | **Accelerated x2 when native craft coefficient resolves successfully.** | Same shared progress input. |
| Paused / interrupted craft | **No persistent Well Fed mutation.** | The prefix only changes the current call's by-ref `delta_time`; it stores no craft state. Calls that do not reach native progress do not accumulate mod-side state. |
| Completion boundary | **Uses vanilla finish path.** | Native code calls `FinishCurrentCraft` after progress reaches at least 1. The mod changes only the current time slice. The x2 candidate still requires the requested total-Energy runtime check at the completion boundary. |
| Special station | **Eligible by default if it is a player manual craft and no existing exclusion matches.** | There is no broad special-station allowlist/denylist. |

Two scope details are therefore design ambiguities, not bugs proven by this pass: gratitude-point manual crafting is currently included, and manual removal is not generically excluded. 1.2.1 intentionally preserves both behaviors.

## 1.2.1 production change

The 1.2.1 fix is deliberately narrow:

1. patch only the exact `DoAction(WorldGameObject, float, bool)` overload;
2. inject the current call's `other_obj` into the Harmony prefix;
3. pass that object to the existing manual-player classifier;
4. remove the classifier's reflective read of `CraftComponent.other_obj`;
5. keep x2 `delta_time`, accepted eligibility/exclusions, buffs, balance values and localization unchanged.

A focused source-contract regression test guards those properties in `tests/verify_craft_prefix.py`.

## Additional B-D findings

Three real hardening gaps remain, but none justifies broadening 1.2.1:

- **Recurring reflection cost:** `ReadNumericResource` can scan methods on each relevant `DoAction` call. This is avoidable work, but there is no measured player-facing performance defect and the exact preferred native/custom-resource getter has not yet been made a production contract.
- **Numeric projection atomicity:** the older `ApplyNumericBalance` path mutates targets incrementally and generally does not prevalidate every expected old value before applying the module. On an unexpected modded baseline it can therefore partially project the existing accepted balance. This is a robustness issue, not a newly observed 1.407 failure.
- **Pre-existing custom-ID validation:** `EnsureCustomBuffs` reuses an already present `gkfr_*` definition without validating its full shape. A namespace collision or stale foreign definition is unlikely but would not fail closed. The newer alcohol module is stricter.

A fourth item remains an **evidence gap rather than a proven defect**: behavior when the mod is removed while a custom buff is active, or when a save contains an active custom buff but its definition is absent, has not been statically established here. Normal save/load with the mod installed uses native `PlayerBuff` state and the accepted 1.1.5/1.2.0 architecture.

## Decision

**Keep current architecture + targeted bug fix.**

Do not replace the `delta_time` mechanism, change Well Fed gameplay scope, rewrite custom buffs, or add speculative performance machinery in 1.2.1.

## Acceptance required before release

Static evidence is sufficient for the sequencing bug and energy-path choice. A short installed-game acceptance pass is still required for the changed Harmony call-context wiring:

1. ordinary eligible manual workstation craft without Well Fed: vanilla speed and normal completion;
2. same craft with one Omelette / Well Fed: approximately x2 completion speed;
3. same craft: same total Keeper Energy with and without Well Fed;
4. worker/zombie or passive/auto production remains unaffected;
5. one excluded world/garden/removal-style action remains unaffected.

Active Well Fed/Inebriated save-reload does not need to be repeated solely because of this source change: 1.2.1 does not touch buff definitions, serialization, timer arithmetic or removal lifecycle. A separate uninstall/missing-definition test would be required only if that compatibility guarantee becomes an explicit target.
