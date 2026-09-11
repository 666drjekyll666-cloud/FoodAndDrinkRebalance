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
- Result: **accepted stable**.
