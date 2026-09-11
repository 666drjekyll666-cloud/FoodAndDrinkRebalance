# Food & Drink Rebalance — Original Runtime Balance Baseline

Source: read-only Graveyard Keeper 1.407 diagnostic scan.

The scan exposed 2,102 `CraftDefinition` rows, 1,157 `ItemDefinition` rows, 34 `BuffDefinition` rows, and 187 `TechDefinition` rows. This baseline explains the original high-priority balance problems that led to the mod.

## Original red zones

### Beet slices vs grated beetroot

- Vanilla `beet_slice`: 2 beets + fire -> 1 serving, 18 visible Energy, oven time 35.
- Vanilla `grated_beetroot`: 1 beet -> 1 serving, 30 Energy, no fire, short manual craft.
- Two grated beets therefore yielded 60 Energy from the same crops that produced one weak cooked beet serving.

### Fried egg vs boiled egg

- Vanilla `fried_egg`: 1 egg + fire -> 13 visible Energy, stack 20.
- Vanilla `boiled_egg`: 1 egg + fire -> 20 Energy, stack 40.
- Same core resource/fuel, while boiled egg dominated both energy and inventory density.

### Carrot routes

- Vanilla carrot cutlets: 2 carrots + fire -> 4 × 15 Energy = 60 total.
- Vanilla grated carrot: 1 carrot -> 20 Energy with no fire and a short manual craft.
- This already suggested a useful role split: grated carrot for convenience, cutlets for crop efficiency.

## Implementation evidence

Older food definitions can contain different energy values in `on_use_expressions` and `params_on_use`. The visible/runtime values matched `params_on_use` in the diagnostic. Production rebalance code therefore synchronizes both representations for affected Energy values unless a specific runtime test establishes otherwise.

Graveyard Keeper 1.407 recipes use `CraftDefinition.needs` for ingredients and `CraftDefinition.output` for outputs.

The DLC `Simple snacks` technology introduces grated carrot, grated beetroot, and boiled egg, confirming that several strongest dominance cases arise from DLC additions crossing older vanilla progression.
