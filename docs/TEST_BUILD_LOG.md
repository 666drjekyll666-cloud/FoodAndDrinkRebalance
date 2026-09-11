# Test Build Log

Numbered binaries handed to the user are immutable. Candidate and accepted entries record exact source and artifact identity.

## 1.1.6 — accepted legacy stable baseline

- Legacy source: `GKFoodRebalance-legacy-private`, `version/1.1.6`, commit `0c266d1c773c355f225f4c3ecf4f7bf6097c3b00`.
- CI run: `34318434374`; artifact `10090925731`.
- Accepted DLL SHA-256: `62fea3da190a984a054e7307e68f68bf4fea627f9ff70aea6f52bc9d2422df74`.
- Player result: accepted; Well Fed x2.00 felt correct and no new regression was reported.

## 1.2.0 — public identity migration candidate

- Goal: preserve accepted 1.1.6 gameplay while moving to the clean public Food & Drink Rebalance identity.
- Runtime changes intended: none.
- Preserved: BepInEx GUID, gameplay values, buffs, patch targets, localization, validation behavior, and craft exclusions.
- Changed: public plugin name, project/assembly/DLL name, and version 1.1.6 -> 1.2.0.
- Candidate source commit: `95c96cce8d1e0d3c9f5e3208bc4deb0bce652a40`; frozen as `candidate/1.2.0`.
- CI: run `34621534690`; artifact `FoodAndDrinkRebalance-1.2.0` (`10272985482`); build succeeded with 0 warnings / 0 errors.
- Candidate DLL SHA-256: `8faafa011b79376bc3687b879a7c4ff93d38b89c8f76434cd54df2b33c729a1c`.
- Smoke test: confirm `Food & Drink Rebalance 1.2.0` loads cleanly and reaches the ready log; check representative accepted behavior such as Omelette/Well Fed, Wine/Inebriated, Fried Egg Sobering, or Green Jelly Speed.
- Result: clean candidate ready for in-game smoke test.
