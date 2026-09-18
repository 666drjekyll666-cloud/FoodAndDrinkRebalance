# Test Build Log

Numbered binaries handed to the user are immutable. Candidate and accepted entries record exact source and artifact identity.

## 1.1.6 — accepted legacy stable baseline

- Legacy source: `GKFoodRebalance-legacy-private`, `version/1.1.6`, commit `0c266d1c773c355f225f4c3ecf4f7bf6097c3b00`.
- CI run: `34318434374`; artifact `10090925731`.
- Accepted DLL SHA-256: `62fea3da190a984a054e7307e68f68bf4fea627f9ff70aea6f52bc9d2422df74`.
- Player result: accepted; Well Fed x2.00 felt correct and no new regression was reported.

## 1.2.0 — accepted public release

- Goal: preserve accepted 1.1.6 gameplay while moving to the clean public Food & Drink Rebalance identity.
- Runtime changes intended: none.
- Preserved: BepInEx GUID, gameplay values, buffs, patch targets, localization, validation behavior, and craft exclusions.
- Changed: public plugin name, project/assembly/DLL name, and version 1.1.6 -> 1.2.0.
- Exact build/source commit: `95c96cce8d1e0d3c9f5e3208bc4deb0bce652a40`.
- Accepted source freeze: `baseline/1.2.0-accepted` at the same commit.
- CI: run `34621534690`; artifact `FoodAndDrinkRebalance-1.2.0` (`10272985482`); build succeeded with 0 warnings / 0 errors.
- Accepted DLL SHA-256: `8faafa011b79376bc3687b879a7c4ff93d38b89c8f76434cd54df2b33c729a1c`.
- Player evidence, 2026-09-11: the candidate loaded as `Food & Drink Rebalance 1.2.0`, completed all startup rebalance modules, and reached `FOOD & DRINK REBALANCE READY`. A Wine use produced `gkfr_inebriated` with the expected +30 HP / +50 Energy effect bubble in the supplied runtime log.
- Investigated unrelated anomalies before acceptance: an older save showed malformed craft UI / control loss and a Vegetable Salad tooltip missing its normal effect text. The craft failure was reproduced without Queue Everything; the supplied stack trace originated in `MaxButtonsRedux.MaxButtonCrafting.SetAmount` -> `CraftItemGUI.Redraw`, not Food & Drink Rebalance. The Vegetable Salad tooltip issue persisted after Food & Drink Rebalance was removed. The user's current save behaved normally.
- User result: explicitly approved stable release after isolating those anomalies from this mod.
- Publication: GitHub Release `v1.2.0`, release `387206239`, publication workflow run `34625537009`, asset `557652959`; GitHub reports the same SHA-256 `8faafa011b79376bc3687b879a7c4ff93d38b89c8f76434cd54df2b33c729a1c` for the 44,032-byte DLL.
- Result: **accepted stable and published**.


## 1.2.1 — candidate / runtime acceptance pending

- Goal: correct the Well Fed `CraftComponent.DoAction` actor-context sequencing bug found by the post-audit review without changing balance or gameplay scope.
- Proven root cause: GK 1.407 `DoAction(WorldGameObject other_obj, float delta_time, bool for_gratitude_points)` stores `this.other_obj = other_obj` only inside the original method. Harmony Prefix runs first, so the 1.2.0 helper could read null or stale `CraftComponent.other_obj`.
- Changed: the prefix now receives the current `other_obj` argument directly and passes it to the existing manual-player classifier; the patch resolver is narrowed to the exact `DoAction(WorldGameObject, float, bool)` overload.
- Unchanged: Well Fed x2.00 multiplier, `delta_time` mechanism, eligibility/exclusion rules, food/alcohol values, custom buff definitions, Inebriated/Sobering timer arithmetic, localization, and stable `main`.
- Static/native evidence: existing GK 1.407 IL confirms current-argument assignment ordering; the same `delta_time` feeds player Energy/Sanity and progress. Existing accepted exact-energy evidence remains the basis for keeping the `delta_time` seam.
- Automated regression: `tests/verify_craft_prefix.py` passed in the candidate CI and asserts current-argument injection, absence of the stale field read, exact overload resolution, unchanged x2 multiplier, and unchanged `delta_time` acceleration.
- Exact candidate source commit: `c638e83cacb71a2f079e6acdc418e6064748a0fa`.
- Frozen candidate ref: `candidate/1.2.1` at the same commit.
- CI: run `35402538631`; artifact `FoodAndDrinkRebalance-1.2.1` (`10571102014`); Release build and regression test succeeded.
- Handed raw DLL: `Food & Drink Rebalance 1.2.1.dll`, 44,544 bytes, SHA-256 `bc0550cdc0f11a690b86cf85e34303881b168bc1722c38e34c5a81cca860f84a`.
- Requested runtime acceptance: compare one reasonably long eligible manual workstation craft without/with Well Fed (target approximately x2); confirm equal total Keeper Energy; confirm one worker/zombie or passive/auto production path is unaffected; confirm one excluded world/garden/removal-style action is unaffected. Report any Well Fed hook warnings from the BepInEx log.
- Buff save/reload: not repeated for this candidate because 1.2.1 does not change buff definitions, `PlayerBuff.end_time`, native add/remove lifecycle, or serialization-related code. Uninstall/missing-custom-definition behavior remains a separate evidence gap, not a change in this candidate.
- Player result: **pending**.
- Result: **candidate only; do not merge or publish before player acceptance**.
