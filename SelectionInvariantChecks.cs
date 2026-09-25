using System;
using System.Collections.Generic;

namespace REPOJP.EliteEnemyVariants;

internal static class SelectionInvariantChecks
{
    internal static void Run()
    {
        SelectionCandidate enabled = new(12345, EliteArchetype.Clown, true);
        SelectionCandidate disabled = new(12345, EliteArchetype.Clown, false);
        ulong seed = 0x1122334455667788UL;

        AssertTier(disabled, new SelectionPolicy(100f, 3), new SelectionCounters(0), seed,
            EnhancementTier.Normal, "disabled candidate");
        AssertTier(enabled, new SelectionPolicy(0f, 3), new SelectionCounters(0), seed,
            EnhancementTier.Normal, "zero percent");
        AssertTier(enabled, new SelectionPolicy(100f, 3), new SelectionCounters(0), seed,
            EnhancementTier.Enhanced, "enhanced 100 percent");
        AssertTier(enabled, new SelectionPolicy(100f, 0), new SelectionCounters(0), seed,
            EnhancementTier.Normal, "enhanced cap");
        SelectionCandidate budgeted = new(12345, EliteArchetype.Clown, true, 1, 2f);
        AssertTier(budgeted, new SelectionPolicy(100f, 3, true, 4f), new SelectionCounters(1, 2.25f), seed,
            EnhancementTier.Normal, "threat budget");
        AssertTier(budgeted, new SelectionPolicy(100f, 3, true, 4f), new SelectionCounters(1, 2f), seed,
            EnhancementTier.Enhanced, "threat budget exact fit");

        float first = SelectionRollSource.RollPercent(seed, enabled.Key);
        float second = SelectionRollSource.RollPercent(seed, enabled.Key);
        if (first != second || first < 0f || first >= 100f)
        {
            throw new InvalidOperationException("Stable selection roll invariant failed.");
        }
        if (SelectionRollSource.RollPriority(seed, enabled.Key) !=
            SelectionRollSource.RollPriority(seed, enabled.Key))
        {
            throw new InvalidOperationException("Stable priority roll invariant failed.");
        }

        TestEnemyCatalogEntry[] catalog = TestEnemyCatalog.Entries;
        int archetypeCount = Enum.GetValues(typeof(EliteArchetype)).Length;
        if (catalog.Length != archetypeCount)
        {
            throw new InvalidOperationException($"Test enemy catalog has {catalog.Length} entries; expected {archetypeCount}.");
        }
        HashSet<EliteArchetype> catalogArchetypes = new();
        for (int index = 0; index < catalog.Length; index++)
        {
            if (catalog[index].Id != index + 1 || !catalogArchetypes.Add(catalog[index].Archetype))
            {
                throw new InvalidOperationException($"Test enemy catalog invariant failed at index {index}.");
            }
        }
    }

    private static void AssertTier(
        SelectionCandidate candidate,
        SelectionPolicy policy,
        SelectionCounters counters,
        ulong seed,
        EnhancementTier expected,
        string name)
    {
        EnhancementTier actual = EliteSelector.SelectTier(candidate, policy, counters, seed);
        if (actual != expected)
        {
            throw new InvalidOperationException($"Selection invariant '{name}' failed: expected {expected}, got {actual}.");
        }
    }
}
