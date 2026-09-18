#!/usr/bin/env python3
from pathlib import Path
import re
import sys

source = Path("src/GKFoodRebalancePlugin.cs").read_text(encoding="utf-8")

checks = [
    (
        "prefix receives current other_obj argument",
        "private static void CraftDoActionPrefix(object __instance, object other_obj, ref float delta_time)" in source,
    ),
    (
        "prefix passes current other_obj to manual-craft classifier",
        "TryGetManualPlayerCraft(" in source and
        "__instance," in source and
        "other_obj," in source and
        "out player," in source and
        "out currentCraft," in source and
        "out craftId," in source and
        "out diagnosticReason," in source and
        "out wgoId)" in source,
    ),
    (
        "manual-craft classifier uses the supplied current actor",
        "player = currentOtherObj;" in source,
    ),
    (
        "stale CraftComponent.other_obj read is absent",
        'GetMember(craftComponent, "other_obj")' not in source,
    ),
    (
        "Well Fed still accelerates delta_time",
        "delta_time *= WellFedCraftSpeedMultiplier;" in source,
    ),
    (
        "accepted x2 multiplier is unchanged",
        "private const float WellFedCraftSpeedMultiplier = 2.00f;" in source,
    ),,
    (
        "diagnostic build records applied/skip decisions",
        '"WELLFED_DIAGNOSTIC"' in source
        and '" applied="' in source
        and '" reason="' in source,
    ),
    (
        "diagnostic logging is bounded",
        "private const int MaxCraftDiagnosticEntries = 64;" in source
        and "CraftDiagnosticSeen.Count >= MaxCraftDiagnosticEntries" in source,
    ),
]

resolver = re.search(
    r'private static void InstallCraftSpeedPatch\(\).*?'
    r'private static void CraftDoActionPrefix',
    source,
    re.S,
)
checks.extend([
    (
        "craft hook resolves a three-argument DoAction overload",
        resolver is not None and "p.Length == 3" in resolver.group(0),
    ),
    (
        "craft hook validates WorldGameObject as argument zero",
        resolver is not None and "p[0].ParameterType == worldGameObjectType" in resolver.group(0),
    ),
    (
        "craft hook validates float delta_time",
        resolver is not None and "p[1].ParameterType == typeof(float)" in resolver.group(0),
    ),
    (
        "craft hook validates bool gratitude flag",
        resolver is not None and "p[2].ParameterType == typeof(bool)" in resolver.group(0),
    ),
])

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + ": " + name)

if failed:
    print("\nRegression contract failed:", file=sys.stderr)
    for name in failed:
        print(" - " + name, file=sys.stderr)
    sys.exit(1)

print("\nCraft-prefix sequencing regression contract passed.")
