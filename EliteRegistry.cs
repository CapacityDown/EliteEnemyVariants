using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteRegistry
{
    private static readonly Dictionary<int, EliteState> StatesByEnemy = new();
    private static readonly Dictionary<int, EnhancementTier> GnomeGroupTiers = new();
    private static readonly Dictionary<int, EnhancementTier> BangerGroupTiers = new();
    private static readonly List<EliteState> StateSnapshot = new();

    private static ulong _stageSeed;
    private static int _stageOrdinal;
    private static int _enhancedCount;
    private static float _threatSpent;
    private static bool _stageSeedReady;
    private static bool _initialSelectionPlanBuilt;

    internal static IReadOnlyList<EliteState> States
    {
        get
        {
            StateSnapshot.Clear();
            StateSnapshot.AddRange(StatesByEnemy.Values);
            return StateSnapshot;
        }
    }

    internal static void BeginStage()
    {
        if (_stageSeedReady)
        {
            return;
        }
        _stageOrdinal++;
        _stageSeed = SelectionRollSource.CreateStageSeed(
            EliteEnemyVariantsPlugin.Instance.Settings.SelectionSeedValue,
            _stageOrdinal);
        _stageSeedReady = true;
        DebugLog($"Stage selection seed initialized: {_stageSeed}.");
    }

    internal static void BuildInitialSelectionPlan()
    {
        if (!IsHostEnabled() || !IsEligibleLevel() || _initialSelectionPlanBuilt)
        {
            return;
        }

        BeginStage();
        Enemy[] enemies = UnityEngine.Object.FindObjectsByType<Enemy>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies)
        {
            RegisterCandidate(enemy);
        }

        List<EliteState> representatives = new();
        HashSet<(EliteArchetype Archetype, int Key)> seen = new();
        foreach (EliteState state in StatesByEnemy.Values)
        {
            RefreshEncounterKey(state);
            if (!state.Selected && seen.Add((state.Archetype, state.Key)))
            {
                representatives.Add(state);
            }
        }
        if (representatives.Count == 0)
        {
            DebugLog("Initial selection plan deferred because no enemy encounters were available yet.");
            return;
        }

        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        List<EliteState> eligible = new();
        foreach (EliteState state in representatives)
        {
            EnemyChanceSettings chance = config.ChanceFor(state.Archetype);
            float chancePercent = chance.ChancePercent.Value < 0f
                ? config.EnhancedChancePercent.Value
                : chance.ChancePercent.Value;
            if (chance.Enabled.Value &&
                SelectionRollSource.RollPercent(_stageSeed, state.Key) < Mathf.Clamp(chancePercent, 0f, 100f))
            {
                eligible.Add(state);
            }
            else
            {
                ApplySelection(state, EnhancementTier.Normal);
            }
        }

        foreach (EliteState state in eligible.OrderBy(item => SelectionRollSource.RollPriority(_stageSeed, item.Key)))
        {
            int capCost = GetCapCost(state.Archetype);
            float threatCost = GetThreatCost(state.Archetype);
            bool hasCountRoom = _enhancedCount + capCost <= config.MaxEnhancedPerLevel.Value;
            bool hasThreatRoom = !config.ThreatBudgetEnabled.Value ||
                                 _threatSpent + threatCost <= config.ThreatBudget.Value + 0.0001f;
            EnhancementTier tier = hasCountRoom && hasThreatRoom
                ? EnhancementTier.Enhanced
                : EnhancementTier.Normal;
            ApplySelection(state, tier);
            if (tier == EnhancementTier.Enhanced)
            {
                _enhancedCount += capCost;
                _threatSpent += threatCost;
            }
        }

        _initialSelectionPlanBuilt = true;
        DebugLog($"Initial selection plan built for {representatives.Count} encounter(s); " +
                 $"enhanced={_enhancedCount}, threat={_threatSpent:0.00}.");
    }

    internal static void RegisterCandidate(Enemy enemy)
    {
        if (!IsHostEnabled() || enemy == null || StatesByEnemy.ContainsKey(enemy.GetInstanceID()))
        {
            return;
        }
        if (!EnemyDisplayNameResolver.TryResolve(enemy, out EliteArchetype archetype))
        {
            return;
        }

        int key = GetSelectionKey(enemy, archetype);
        EliteState state = new()
        {
            Key = key,
            Enemy = enemy,
            Archetype = archetype,
            Tier = EnhancementTier.Normal,
            BaseHealth = GameFieldAccess.HasHealth(enemy)
                ? Math.Max(1, GameFieldAccess.Health(enemy).health)
                : 1
        };
        StatesByEnemy.Add(enemy.GetInstanceID(), state);

        if (TryGetGroupTier(archetype, key, out EnhancementTier inheritedTier))
        {
            state.Selected = true;
            state.Tier = inheritedTier;
            PrepareHealth(state);
        }
    }

    internal static void SelectOnFirstSpawn(EnemyParent parent)
    {
        if (!IsHostEnabled() || !IsEligibleLevel() || parent == null)
        {
            return;
        }

        Enemy? enemy = parent.GetComponentInChildren<Enemy>(true);
        if (enemy == null)
        {
            return;
        }
        RegisterCandidate(enemy);
        if (!StatesByEnemy.TryGetValue(enemy.GetInstanceID(), out EliteState state))
        {
            return;
        }
        if (!_initialSelectionPlanBuilt && LevelGenerator.Instance != null && LevelGenerator.Instance.Generated)
        {
            BuildInitialSelectionPlan();
        }
        RefreshEncounterKey(state);
        if (!state.Selected && TryGetGroupTier(state.Archetype, state.Key, out EnhancementTier inheritedTier))
        {
            state.Selected = true;
            state.Tier = inheritedTier;
        }
        if (state.Selected)
        {
            PrepareHealth(state);
            return;
        }

        if (!_stageSeedReady)
        {
            BeginStage();
        }

        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        EnemyChanceSettings chance = config.ChanceFor(state.Archetype);
        float enhancedChance = chance.ChancePercent.Value < 0f
            ? config.EnhancedChancePercent.Value
            : chance.ChancePercent.Value;
        bool enabled = chance.Enabled.Value;
        int capCost = GetCapCost(state.Archetype);
        float threatCost = GetThreatCost(state.Archetype);

        SelectionCandidate candidate = new(state.Key, state.Archetype, enabled, capCost, threatCost);
        SelectionPolicy policy = new(
            enhancedChance,
            config.MaxEnhancedPerLevel.Value,
            config.ThreatBudgetEnabled.Value,
            config.ThreatBudget.Value);
        SelectionCounters counters = new(_enhancedCount, _threatSpent);
        EnhancementTier tier = EliteSelector.SelectTier(candidate, policy, counters, _stageSeed);

        ApplySelection(state, tier);

        if (tier != EnhancementTier.Normal)
        {
            _enhancedCount += capCost;
            _threatSpent += threatCost;
        }

        DebugLog($"Selected {EnemyDisplayNameResolver.Get(state.Archetype)} key={state.Key}: {tier}; " +
                 $"enhanced={_enhancedCount}.");
    }

    internal static void OnSpawnComplete(EnemyParent parent)
    {
        if (!IsHostEnabled() || parent == null)
        {
            return;
        }
        Enemy? enemy = parent.GetComponentInChildren<Enemy>(true);
        if (enemy == null ||
            !StatesByEnemy.TryGetValue(enemy.GetInstanceID(), out EliteState state) ||
            !state.IsEnhanced ||
            state.Spawned)
        {
            return;
        }

        state.Spawned = true;
        state.PulsePart = 0;
        state.NextPulseAt = Time.time;
        state.NextPulseCycleAt = Time.time + GetPulseCycle(state);
        state.EncounterPulseTriggered = false;
        state.EncounterPulseRemaining = 0;
        state.AdapterTickFaulted = false;
        state.AdapterTickConsecutiveFailures = 0;
        try
        {
            EnemyAdapterRuntime.OnSpawn(state);
        }
        catch (Exception exception)
        {
            state.AdapterTickFaulted = true;
            state.AdapterTickConsecutiveFailures = 3;
            EliteEnemyVariantsPlugin.ModLogger.LogError(
                $"Disabled only the adapter behavior for {EnemyDisplayNameResolver.Get(state.Archetype)} " +
                $"until its next spawn because OnSpawn failed; common stats and markers remain active: {exception}");
        }
    }

    internal static bool ForceTierForTest(Enemy enemy, EnhancementTier tier)
    {
        if (!IsHostEnabled() || enemy == null)
        {
            return false;
        }

        RegisterCandidate(enemy);
        if (!StatesByEnemy.TryGetValue(enemy.GetInstanceID(), out EliteState state))
        {
            return false;
        }

        state.Selected = true;
        state.Tier = tier;
        if (IsEncounterGroup(state.Archetype))
        {
            state.Key = GetSelectionKey(enemy, state.Archetype);
            SetGroupTier(state.Archetype, state.Key, tier);
            foreach (EliteState member in StatesByEnemy.Values.Where(item =>
                         item.Archetype == state.Archetype && item.Key == state.Key))
            {
                member.Selected = true;
                member.Tier = tier;
                PrepareHealth(member);
            }
        }
        else
        {
            PrepareHealth(state);
        }

        DebugLog($"Test command forced {EnemyDisplayNameResolver.Get(state.Archetype)} key={state.Key}: {tier}.");
        return true;
    }

    internal static void OnDespawn(EnemyParent parent)
    {
        if (parent == null)
        {
            return;
        }
        Enemy? enemy = parent.GetComponentInChildren<Enemy>(true);
        if (enemy != null && StatesByEnemy.TryGetValue(enemy.GetInstanceID(), out EliteState state))
        {
            state.Spawned = false;
            state.PulsePart = 0;
            state.EncounterPulseRemaining = 0;
            EnemyAdapterRuntime.Cleanup(state);
        }
    }

    internal static void MarkHurt(EnemyHealth health)
    {
        if (TryGet(health, out EliteState state))
        {
            state.LastHurtAt = Time.time;
        }
    }

    internal static void MarkRealHeal(EnemyHealth health)
    {
        if (TryGet(health, out EliteState state))
        {
            state.LastRealHealAt = Time.time;
        }
    }

    internal static bool TryGet(Enemy? enemy, out EliteState state)
    {
        if (enemy != null && StatesByEnemy.TryGetValue(enemy.GetInstanceID(), out state!))
        {
            return state.IsEnhanced;
        }
        state = null!;
        return false;
    }

    internal static bool TryGet(Component? component, out EliteState state)
    {
        Enemy? enemy = component != null ? component.GetComponent<Enemy>() : null;
        enemy ??= component != null ? component.GetComponentInParent<Enemy>() : null;
        return TryGet(enemy, out state);
    }

    internal static bool IsEnhanced(Enemy? enemy) => TryGet(enemy, out _);

    internal static float GetNormalMoveMultiplier(EliteState state)
    {
        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        return ApplyCommonStatsToggle(config.NavMoveMultiplier.Value, config.CommonStatsEnabled.Value);
    }

    internal static float GetAttackMoveMultiplier(EliteState state)
    {
        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        return ApplyCommonStatsToggle(config.NavAttackMoveMultiplier.Value, config.CommonStatsEnabled.Value);
    }

    internal static float GetRigidbodyMoveMultiplier(EliteState state)
    {
        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        return ApplyCommonStatsToggle(config.RigidbodyMoveMultiplier.Value, config.CommonStatsEnabled.Value);
    }

    internal static void RecordAdapterTickSuccess(EliteState state)
    {
        state.AdapterTickConsecutiveFailures = 0;
    }

    internal static void RecordAdapterTickFailure(EliteState state, Exception exception, string source)
    {
        state.AdapterTickConsecutiveFailures++;
        if (state.AdapterTickConsecutiveFailures >= 3)
        {
            state.AdapterTickFaulted = true;
            EliteEnemyVariantsPlugin.ModLogger.LogError(
                $"Disabled only the adapter behavior for {EnemyDisplayNameResolver.Get(state.Archetype)} " +
                $"after 3 consecutive {source} failures; common stats and markers remain active: {exception}");
            return;
        }

        EliteEnemyVariantsPlugin.ModLogger.LogWarning(
            $"Transient {source} failure for {EnemyDisplayNameResolver.Get(state.Archetype)} " +
            $"({state.AdapterTickConsecutiveFailures}/3); the adapter will retry: {exception.Message}");
    }

    internal static void ClearStage()
    {
        foreach (EliteState state in StatesByEnemy.Values)
        {
            EnemyAdapterRuntime.Cleanup(state);
        }
        StatesByEnemy.Clear();
        GnomeGroupTiers.Clear();
        BangerGroupTiers.Clear();
        StateSnapshot.Clear();
        TestSpawnCommands.ClearStage();
        _enhancedCount = 0;
        _threatSpent = 0f;
        _stageSeedReady = false;
        _initialSelectionPlanBuilt = false;
    }

    private static void PrepareHealth(EliteState state)
    {
        if (!GameFieldAccess.HasHealth(state.Enemy))
        {
            return;
        }
        if (state.Tier == EnhancementTier.Normal)
        {
            return;
        }

        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        int health;
        if (state.Archetype == EliteArchetype.Gnome)
        {
            health = config.GnomeHealth.Value;
        }
        else
        {
            float multiplier = ApplyCommonStatsToggle(config.HealthMultiplier.Value, config.CommonStatsEnabled.Value);
            health = Mathf.Max(1, Mathf.RoundToInt(state.BaseHealth * multiplier));
        }
        GameFieldAccess.Health(state.Enemy).health = health;
    }

    private static int GetSelectionKey(Enemy enemy, EliteArchetype archetype)
    {
        if (TryGetEncounterGroupKey(enemy, archetype, out int groupKey))
        {
            return groupKey;
        }
        PhotonView photonView = GameFieldAccess.PhotonView(enemy);
        if (GameManager.Multiplayer() && photonView != null && photonView.ViewID != 0)
        {
            return photonView.ViewID;
        }
        return unchecked(-1 - enemy.GetInstanceID());
    }

    private static void RefreshEncounterKey(EliteState state)
    {
        if (!IsEncounterGroup(state.Archetype))
        {
            return;
        }
        state.Key = GetSelectionKey(state.Enemy, state.Archetype);
    }

    private static bool IsEncounterGroup(EliteArchetype archetype) =>
        archetype is EliteArchetype.Banger or EliteArchetype.Gnome;

    private static bool TryGetEncounterGroupKey(Enemy enemy, EliteArchetype archetype, out int key)
    {
        if (archetype == EliteArchetype.Banger)
        {
            EnemyBang? unit = enemy.GetComponent<EnemyBang>();
            EnemyBangDirector? director = unit == null
                ? null
                : UnityEngine.Object.FindObjectsByType<EnemyBangDirector>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.units != null && candidate.units.Contains(unit));
            director ??= enemy.transform.root.GetComponentInChildren<EnemyBangDirector>(true) ??
                         EnemyBangDirector.instance;
            if (director != null)
            {
                key = unchecked((int)0xBACE0000 ^ director.GetInstanceID());
                return true;
            }
        }
        else if (archetype == EliteArchetype.Gnome)
        {
            EnemyGnome? unit = enemy.GetComponent<EnemyGnome>();
            EnemyGnomeDirector? director = unit == null
                ? null
                : UnityEngine.Object.FindObjectsByType<EnemyGnomeDirector>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.gnomes != null && candidate.gnomes.Contains(unit));
            director ??= enemy.transform.root.GetComponentInChildren<EnemyGnomeDirector>(true) ??
                         EnemyGnomeDirector.instance;
            if (director != null)
            {
                key = unchecked((int)0x6E0A0000 ^ director.GetInstanceID());
                return true;
            }
        }

        key = 0;
        return false;
    }

    private static bool TryGetGroupTier(
        EliteArchetype archetype,
        int key,
        out EnhancementTier tier)
    {
        if (archetype == EliteArchetype.Banger)
        {
            return BangerGroupTiers.TryGetValue(key, out tier);
        }
        if (archetype == EliteArchetype.Gnome)
        {
            return GnomeGroupTiers.TryGetValue(key, out tier);
        }
        tier = EnhancementTier.Normal;
        return false;
    }

    private static void SetGroupTier(EliteArchetype archetype, int key, EnhancementTier tier)
    {
        if (archetype == EliteArchetype.Banger)
        {
            BangerGroupTiers[key] = tier;
        }
        else if (archetype == EliteArchetype.Gnome)
        {
            GnomeGroupTiers[key] = tier;
        }
    }

    private static void ApplySelection(EliteState state, EnhancementTier tier)
    {
        if (!IsEncounterGroup(state.Archetype))
        {
            state.Selected = true;
            state.Tier = tier;
            PrepareHealth(state);
            return;
        }

        SetGroupTier(state.Archetype, state.Key, tier);
        foreach (EliteState member in StatesByEnemy.Values.Where(item =>
                     item.Archetype == state.Archetype && item.Key == state.Key))
        {
            member.Selected = true;
            member.Tier = tier;
            PrepareHealth(member);
        }
    }

    private static int GetCapCost(EliteArchetype archetype) => 1;

    private static float GetThreatCost(EliteArchetype archetype) => archetype switch
    {
        EliteArchetype.Animal => 0.75f,
        EliteArchetype.ApexPredator => 1.00f,
        EliteArchetype.Banger => 1.00f,
        EliteArchetype.Bella => 1.50f,
        EliteArchetype.BirthdayBoy => 1.25f,
        EliteArchetype.Bowtie => 1.00f,
        EliteArchetype.Chef => 1.50f,
        EliteArchetype.CleanupCrew => 1.25f,
        EliteArchetype.Clown => 2.00f,
        EliteArchetype.Elsa => 1.50f,
        EliteArchetype.Gambit => 1.25f,
        EliteArchetype.Gnome => 1.50f,
        EliteArchetype.Headgrab => 1.25f,
        EliteArchetype.Headman => 1.00f,
        EliteArchetype.HeartHugger => 1.25f,
        EliteArchetype.Hidden => 1.25f,
        EliteArchetype.Huntsman => 1.25f,
        EliteArchetype.Loom => 1.25f,
        EliteArchetype.Mentalist => 1.50f,
        EliteArchetype.Oogly => 1.00f,
        EliteArchetype.Peeper => 0.75f,
        EliteArchetype.Pony => 1.50f,
        EliteArchetype.Reaper => 1.25f,
        EliteArchetype.Robe => 1.75f,
        EliteArchetype.Rugrat => 1.25f,
        EliteArchetype.ShadowChild => 1.50f,
        EliteArchetype.Spewer => 2.00f,
        EliteArchetype.Tick => 0.75f,
        EliteArchetype.Trudge => 1.00f,
        EliteArchetype.Upscream => 1.25f,
        _ => 1.25f
    };

    private static float ApplyCommonStatsToggle(float configuredMultiplier, bool enabled) =>
        enabled ? configuredMultiplier : 1f;

    private static bool IsEligibleLevel()
    {
        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        if (config.ExcludeShop.Value && SemiFunc.RunIsShop())
        {
            return false;
        }
        if (config.ExcludeArena.Value && SemiFunc.RunIsArena())
        {
            return false;
        }
        return !SemiFunc.RunIsLobby() && !SemiFunc.RunIsLobbyMenu();
    }

    private static bool IsHostEnabled() =>
        EliteEnemyVariantsPlugin.Instance != null &&
        EliteEnemyVariantsPlugin.Instance.Settings.Enabled.Value &&
        SemiFunc.IsMasterClientOrSingleplayer();

    private static float GetPulseCycle(EliteState state)
    {
        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        return config.PulseCycleSeconds.Value;
    }

    private static void DebugLog(string message)
    {
        if (EliteEnemyVariantsPlugin.Instance.Settings.DebugLog.Value)
        {
            EliteEnemyVariantsPlugin.ModLogger.LogInfo(message);
        }
    }
}
