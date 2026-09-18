# Food & Drink Rebalance — Project Rules

The global engineering baseline is `666drjekyll666-cloud/DevRules`. Read its `ENGINEERING_RULES.md`, `CI_POLICY.md`, `GIT_WORKFLOW.md`, and `PROJECT_BOOTSTRAP.md` before substantive work. This file contains project-specific additions.

## Identity and scope

- Project: **Food & Drink Rebalance**.
- Game: **Graveyard Keeper 1.407**.
- Repository: `666drjekyll666-cloud/FoodAndDrinkRebalance`.
- Project/assembly/DLL: `FoodAndDrinkRebalance` / `FoodAndDrinkRebalance.dll`.
- Canonical runtime source: `src/GKFoodRebalancePlugin.cs`.
- BepInEx GUID: `nikich.graveyardkeeper.gkfoodrebalance`; preserve this GUID across branding/version changes.

This is a targeted food, drink, recipe, and directly related buff-balance mod. Do not broaden it into a general Graveyard Keeper overhaul without explicit approval.

## Evidence and runtime contract

Do not infer internal IDs, recipe schema, buff/timer representation, technology data, resource names, expressions, or runtime callbacks from display names. Use accepted repository evidence first; durable facts belong in `docs/VERIFIED_RUNTIME_DATA.md` and balance decisions in `docs/BALANCE_DESIGN.md`.

For a new balance module: discover the exact target/runtime schema, validate required targets and expected pre-change state, mutate only after validation succeeds, then verify startup/runtime behavior. Multi-target features should fail closed rather than partially apply when their required contract does not match.

Prefer deterministic startup/event-bound mutations over polling. Avoid permanent global scans, per-frame reflection/enumeration, background workers, hot-path logging, and save-data changes unless separately justified.

## Stable behavior baseline

Public stable **1.2.1** preserves the accepted 1.1.6/1.2.0 gameplay balance. Its only runtime behavior correction is the Well Fed actor-context sequencing fix documented in `docs/POST_AUDIT_1.2.1.md`; gameplay constants, effect strengths/durations, eligibility policy, food/alcohol values, localization behavior, and fail-closed validation remain unchanged unless a later accepted version explicitly changes them.

## Git, builds, and acceptance

- `main` is accepted stable state only.
- Runtime work normally uses `dev/X.Y.Z`.
- Freeze a handed candidate at `candidate/X.Y.Z` and an accepted source at `baseline/X.Y.Z-accepted`.
- Every handed DLL is immutable and tied to an exact source SHA and CI artifact.
- Candidate/test handoffs should be a ready raw versioned DLL, not a ZIP.
- Stable GitHub Release assets should use the canonical installed filename `FoodAndDrinkRebalance.dll`; a filename-only packaging rename of the accepted bytes does not create a new code version.
- Stable public binaries are published through GitHub Releases using the exact accepted artifact bytes after SHA-256 verification; do not rebuild accepted bytes merely for release publication.

## CI

Hosted CI is a candidate/handoff gate, not a per-commit service. `ubuntu-latest` is the proven canonical build environment for this managed-code project. Prefer manual `workflow_dispatch`; do not trigger builds for documentation/bookkeeping-only changes. Temporary candidate artifacts use short retention.

## Sources of truth

- `docs/VERIFIED_RUNTIME_DATA.md` — verified Graveyard Keeper/runtime contracts used by production.
- `docs/BALANCE_DESIGN.md` — accepted gameplay design and values.
- `docs/TEST_BUILD_LOG.md` — exact handed-build identity and player evidence.
- `docs/balance-baseline.md` — compact original runtime balance context.
- `docs/MIGRATION_PROVENANCE.md` — identity of the private legacy baseline used for this public repository.
