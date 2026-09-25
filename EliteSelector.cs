using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteSelector
{
    internal static EnhancementTier SelectTier(
        in SelectionCandidate candidate,
        in SelectionPolicy policy,
        in SelectionCounters counters,
        ulong stageSelectionSeed)
    {
        if (!candidate.Enabled || candidate.CapCost <= 0)
        {
            return EnhancementTier.Normal;
        }

        float enhancedChance = Mathf.Clamp(policy.EnhancedChancePercent, 0f, 100f);
        float roll = SelectionRollSource.RollPercent(stageSelectionSeed, candidate.Key);

        if (roll < enhancedChance)
        {
            bool hasEnhancedRoom = counters.Enhanced + candidate.CapCost <= policy.MaxEnhancedPerLevel;
            bool hasThreatRoom = !policy.ThreatBudgetEnabled ||
                                 counters.ThreatSpent + candidate.ThreatCost <= policy.ThreatBudget + 0.0001f;
            return hasEnhancedRoom && hasThreatRoom
                ? EnhancementTier.Enhanced
                : EnhancementTier.Normal;
        }

        return EnhancementTier.Normal;
    }
}
