using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal enum EnhancementTier
{
    Normal,
    Enhanced
}

internal readonly struct SelectionPolicy
{
    internal readonly float EnhancedChancePercent;
    internal readonly int MaxEnhancedPerLevel;
    internal readonly bool ThreatBudgetEnabled;
    internal readonly float ThreatBudget;

    internal SelectionPolicy(
        float enhancedChancePercent,
        int maxEnhancedPerLevel,
        bool threatBudgetEnabled = false,
        float threatBudget = float.MaxValue)
    {
        EnhancedChancePercent = enhancedChancePercent;
        MaxEnhancedPerLevel = maxEnhancedPerLevel;
        ThreatBudgetEnabled = threatBudgetEnabled;
        ThreatBudget = threatBudget;
    }
}

internal readonly struct SelectionCandidate
{
    internal readonly int Key;
    internal readonly EliteArchetype Archetype;
    internal readonly bool Enabled;
    internal readonly int CapCost;
    internal readonly float ThreatCost;

    internal SelectionCandidate(
        int key,
        EliteArchetype archetype,
        bool enabled,
        int capCost = 1,
        float threatCost = 0f)
    {
        Key = key;
        Archetype = archetype;
        Enabled = enabled;
        CapCost = capCost;
        ThreatCost = threatCost;
    }
}

internal readonly struct SelectionCounters
{
    internal readonly int Enhanced;
    internal readonly float ThreatSpent;

    internal SelectionCounters(int enhanced, float threatSpent = 0f)
    {
        Enhanced = enhanced;
        ThreatSpent = threatSpent;
    }
}

internal sealed class EliteState
{
    internal int Key;
    internal Enemy Enemy = null!;
    internal EliteArchetype Archetype;
    internal EnhancementTier Tier;
    internal bool Selected;
    internal bool Spawned;
    internal int BaseHealth;
    internal float LastHurtAt = float.NegativeInfinity;
    internal float LastRealHealAt = float.NegativeInfinity;
    internal float NextPulseAt;
    internal float NextPulseCycleAt;
    internal int PulsePart;
    internal bool EncounterPulseTriggered;
    internal int EncounterPulseRemaining;
    internal object? AdapterState;
    internal bool AdapterTickFaulted;
    internal int AdapterTickConsecutiveFailures;

    internal bool IsEnhanced => Tier != EnhancementTier.Normal;
}

internal sealed class ClownRuntimeState
{
    internal bool AttackInitialized;
    internal bool TargetSwitched;
    internal bool SpinChecked;
    internal bool Spinning;
    internal float SpinRemaining;
    internal float SpinYaw;
    internal Vector3 LastTargetPosition;
}

internal sealed class ShadowChildRuntimeState
{
    internal float LookAccumulation;
    internal Vector3 LastPosition;
    internal float StuckTimer;
    internal int LastTargetViewId;
    internal int PreviousTargetViewId;
    internal int PreviousState = -1;
}

internal sealed class HuntsmanRuntimeState
{
    internal bool PendingInvestigate;
    internal Vector3 PendingInvestigatePoint;
    internal bool PendingInvestigatePathfindOnly;
    internal bool AccuracyRolled;
    internal bool MissThisShot;
    internal float MissYaw;
    internal float MissPitch;
    internal Vector3 MissImpactPoint;
}

internal sealed class GambitRuntimeState
{
    internal bool EscapeActive;
    internal Vector3 EscapePoint;
    internal float StuckTimer;
    internal Vector3 LastPosition;
    internal int EscapeAttempt;
}

internal sealed class BangerRuntimeState
{
    internal float FuseStartedAt;
    internal float LastSyncAt;
    internal bool PreviousFuseActive;
}

internal sealed class GnomeRuntimeState
{
    internal bool StatsApplied;
}

internal sealed class DedicatedEnemyRuntimeState
{
    internal readonly TacticalRuntimeState Tactics = new();
    internal bool SpawnInitialized;
    internal int PreviousState = -1;
    internal bool ExtraActionUsed;
    internal float StuckTimer;
    internal Vector3 LastPosition;
    internal int LastTargetViewId;
    internal float NextActionAt;
    internal bool AlternateMode;
}
