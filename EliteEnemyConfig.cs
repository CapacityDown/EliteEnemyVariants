using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;

namespace REPOJP.EliteEnemyVariants;

internal sealed class EnemyChanceSettings
{
    internal ConfigEntry<bool> Enabled { get; }
    internal ConfigEntry<int> ChancePercent { get; }

    internal EnemyChanceSettings(ConfigFile config, string section)
    {
        Enabled = config.Bind(section, "Enabled", true, $"Enable {section} enhancement selection.");
        ChancePercent = config.Bind(section, "ChancePercent", -1,
            new ConfigDescription(
                "-1 uses the common enhanced chance; 0 to 100 uses a whole-number override.",
                new AcceptableValueRange<int>(-1, 100)));
    }
}

internal sealed class EliteEnemyConfig
{
    private readonly Dictionary<EliteArchetype, EnemyChanceSettings> _expandedEnemyChances = new();

    internal ConfigEntry<bool> Enabled { get; }
    internal ConfigEntry<bool> VanillaEnemiesOnly { get; }
    internal ConfigEntry<bool> ExcludeShop { get; }
    internal ConfigEntry<bool> ExcludeArena { get; }
    internal ConfigEntry<bool> DebugLog { get; }
    internal ConfigEntry<int> ConfigSchemaVersion { get; }
    internal ConfigEntry<int> TestSpawnMaxGroups { get; }

    internal ConfigEntry<float> EnhancedChancePercent { get; }
    internal ConfigEntry<int> MaxEnhancedPerLevel { get; }
    internal ConfigEntry<bool> ThreatBudgetEnabled { get; }
    internal ConfigEntry<float> ThreatBudget { get; }
    internal ConfigEntry<string> SelectionSeed { get; }
    internal long SelectionSeedValue =>
        long.TryParse(
            SelectionSeed.Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long value) && value > 0L
            ? value
            : 0L;

    internal ConfigEntry<float> HealthMultiplier { get; }
    internal ConfigEntry<float> NavMoveMultiplier { get; }
    internal ConfigEntry<float> NavAttackMoveMultiplier { get; }
    internal ConfigEntry<float> RigidbodyMoveMultiplier { get; }
    internal ConfigEntry<bool> CommonStatsEnabled { get; }

    internal ConfigEntry<bool> EnableVanillaPulse { get; }
    internal ConfigEntry<float> PulseCycleSeconds { get; }
    internal ConfigEntry<float> PulseGapSeconds { get; }
    internal ConfigEntry<float> DelayAfterHurtSeconds { get; }
    internal ConfigEntry<float> DelayAfterRealHealSeconds { get; }

    internal ConfigEntry<float> BehaviorWindupRate { get; }
    internal ConfigEntry<float> BehaviorCooldownRate { get; }
    internal ConfigEntry<float> BehaviorChaseRetention { get; }

    internal EnemyChanceSettings Clown { get; }
    internal ConfigEntry<float> ClownTrackSpeed { get; }
    internal ConfigEntry<float> ClownBeamRange { get; }
    internal ConfigEntry<float> ClownBeamDurationMin { get; }
    internal ConfigEntry<float> ClownBeamDurationMax { get; }
    internal ConfigEntry<bool> ClownWalkWhileAttacking { get; }
    internal ConfigEntry<bool> ClownTargetSwitch { get; }
    internal ConfigEntry<float> ClownSpinChancePercent { get; }
    internal ConfigEntry<float> ClownSpinSpeedDegPerSec { get; }

    internal EnemyChanceSettings ShadowChild { get; }
    internal ConfigEntry<float> ShadowEyeContactSeconds { get; }
    internal ConfigEntry<float> ShadowApproachRadius { get; }
    internal ConfigEntry<float> ShadowApproachSpeed { get; }

    internal EnemyChanceSettings Huntsman { get; }
    internal ConfigEntry<float> HuntsmanSoundInvestigateRange { get; }

    internal EnemyChanceSettings Gambit { get; }
    internal ConfigEntry<float> GambitRouletteDurationSeconds { get; }
    internal ConfigEntry<bool> GambitRouletteStunImmune { get; }

    internal EnemyChanceSettings Banger { get; }
    internal ConfigEntry<float> BangerFuseSeconds { get; }
    internal ConfigEntry<float> BangerNearTargetFuseSeconds { get; }
    internal ConfigEntry<float> BangerNearTargetDistance { get; }
    internal ConfigEntry<float> BangerFuseSyncIntervalSeconds { get; }

    internal EnemyChanceSettings Gnome { get; }
    internal ConfigEntry<int> GnomeHealth { get; }
    internal ConfigEntry<float> GnomeAttackOrbitSpeed { get; }
    internal ConfigEntry<float> GnomeJumpMultiplier { get; }
    internal ConfigEntry<float> GnomeAttackRadius { get; }
    internal ConfigEntry<bool> GnomeZeroNoticeDelay { get; }
    internal ConfigEntry<bool> GnomeZeroAttackCooldown { get; }
    internal ConfigEntry<float> GnomeValuablePriorityRadius { get; }
    internal ConfigEntry<int> GnomeConcurrentAttackers { get; }

    internal ConfigEntry<float> OoglyLowPatrolHeight { get; }
    internal ConfigEntry<float> OoglyLowPatrolIntervalSeconds { get; }
    internal ConfigEntry<float> RobeUnseenMoveSpeed { get; }

    internal EliteEnemyConfig(ConfigFile config)
    {
        EliteEnemyConfigMigration.Apply(config);
        ConfigSchemaVersion = config.Bind(
            "Migration",
            "ConfigVersion",
            EliteEnemyConfigMigration.CurrentSchemaVersion,
            new ConfigDescription(
                "Internal config migration schema. Do not edit manually.",
                null,
                "HideFromREPOConfig"));

        Enabled = config.Bind("General", "Enabled", true, "Master switch.");
        VanillaEnemiesOnly = config.Bind("General", "VanillaEnemiesOnly", true, "Only select the 29 audited vanilla enemy types.");
        ExcludeShop = config.Bind("General", "ExcludeShop", true, "Disable selection in shop levels.");
        ExcludeArena = config.Bind("General", "ExcludeArena", true, "Disable selection in arena levels.");
        DebugLog = config.Bind("General", "DebugLog", false, "Enable verbose diagnostic logging.");

        EnhancedChancePercent = Percent(config, "Selection", "EnhancedChancePercent", 35f);
        MaxEnhancedPerLevel = config.Bind("Selection", "MaxEnhancedPerLevel", 3,
            new ConfigDescription("Maximum enhanced enemies per level.", new AcceptableValueRange<int>(0, 100)));
        ThreatBudgetEnabled = config.Bind(
            "Selection",
            "ThreatBudgetEnabled",
            true,
            "Limit the combined threat cost of enhanced encounters.");
        ThreatBudget = Number(config, "Selection", "ThreatBudget", 4f, 0f, 100f);
        SelectionSeed = config.Bind(
            "Selection",
            "SelectionSeed",
            "0",
            "Numeric text. 0, negative, or invalid values use a fresh per-stage seed; positive 64-bit values are reproducible.");

        CommonStatsEnabled = config.Bind(
            "Common",
            "CommonStatsEnabled",
            true,
            "Enable common health and movement bonuses. Dedicated enemy behavior and identification remain active when disabled.");
        HealthMultiplier = Multiplier(config, "Common", "HealthMultiplier", 1.50f);
        NavMoveMultiplier = Multiplier(config, "Common", "NavMoveMultiplier", 1.15f);
        NavAttackMoveMultiplier = Multiplier(config, "Common", "NavAttackMoveMultiplier", 1.20f);
        RigidbodyMoveMultiplier = Multiplier(config, "Common", "RigidbodyMoveMultiplier", 1.15f);

        BehaviorWindupRate = Multiplier(config, "Behavior", "WindupRate", 1.35f);
        BehaviorCooldownRate = Multiplier(config, "Behavior", "CooldownRate", 1.30f);
        BehaviorChaseRetention = Number(config, "Behavior", "ChaseRetention", 0.25f, 0f, 0.95f);

        EnableVanillaPulse = config.Bind("Visual", "EnableVanillaPulse", true, "Use two vanilla green heal flashes to identify enhanced enemies.");
        PulseCycleSeconds = Seconds(config, "Visual", "PulseCycleSeconds", 2.50f, 0.5f, 30f);
        PulseGapSeconds = Seconds(config, "Visual", "PulseGapSeconds", 0.22f, 0.05f, 1f);
        DelayAfterHurtSeconds = Seconds(config, "Visual", "DelayAfterHurtSeconds", 0.80f, 0f, 10f);
        DelayAfterRealHealSeconds = Seconds(config, "Visual", "DelayAfterRealHealSeconds", 0.80f, 0f, 10f);

        AddExpandedChance(config, EliteArchetype.Animal);
        AddExpandedChance(config, EliteArchetype.ApexPredator);

        Banger = new EnemyChanceSettings(config, "Banger");
        BangerFuseSeconds = Seconds(config, "Banger", "FuseSeconds", 5f, 0.5f, 30f);
        BangerNearTargetFuseSeconds = Seconds(config, "Banger", "NearTargetFuseSeconds", 3.5f, 0.5f, 30f);
        BangerNearTargetDistance = Number(config, "Banger", "NearTargetDistance", 6f, 0.5f, 50f);
        BangerFuseSyncIntervalSeconds = Seconds(config, "Banger", "FuseSyncIntervalSeconds", 0.5f, 0.1f, 5f);

        AddExpandedChance(config, EliteArchetype.Bella);
        AddExpandedChance(config, EliteArchetype.BirthdayBoy);
        AddExpandedChance(config, EliteArchetype.Bowtie);
        AddExpandedChance(config, EliteArchetype.Chef);
        AddExpandedChance(config, EliteArchetype.CleanupCrew);

        Clown = new EnemyChanceSettings(config, "Clown");
        ClownWalkWhileAttacking = config.Bind("Clown", "WalkWhileAttacking", true, "Move toward the current target while beaming.");
        ClownTargetSwitch = config.Bind("Clown", "TargetSwitch", true, "Allow one target switch per attack.");
        ClownTrackSpeed = Number(config, "Clown", "TrackSpeed", 15f, 1f, 300f);
        ClownBeamRange = Number(config, "Clown", "BeamRange", 10f, 1f, 100f);
        ClownBeamDurationMin = Seconds(config, "Clown", "BeamDurationMin", 1f, 0.1f, 30f);
        ClownBeamDurationMax = Seconds(config, "Clown", "BeamDurationMax", 2f, 0.1f, 30f);
        ClownSpinChancePercent = Percent(config, "Clown", "SpinChancePercent", 10f);
        ClownSpinSpeedDegPerSec = Number(config, "Clown", "SpinSpeedDegPerSec", 300f, 30f, 3000f);

        AddExpandedChance(config, EliteArchetype.Elsa);

        Gambit = new EnemyChanceSettings(config, "Gambit");
        GambitRouletteStunImmune = config.Bind("Gambit", "RouletteStunImmune", true, "Ignore stun only while roulette is active.");
        GambitRouletteDurationSeconds = Seconds(config, "Gambit", "RouletteDurationSeconds", 5f, 1f, 30f);

        Gnome = new EnemyChanceSettings(config, "Gnome");
        GnomeZeroNoticeDelay = config.Bind("Gnome", "ZeroNoticeDelay", true, "Remove the enhanced group's notice delay.");
        GnomeZeroAttackCooldown = config.Bind("Gnome", "ZeroAttackCooldown", true, "Remove the enhanced group's re-attack cooldown.");
        GnomeHealth = config.Bind("Gnome", "Health", 25, new ConfigDescription("Enhanced Gnome health.", new AcceptableValueRange<int>(1, 10000)));
        GnomeConcurrentAttackers = config.Bind("Gnome", "ConcurrentAttackers", 2,
            new ConfigDescription("Maximum simultaneous enhanced Gnome attackers.", new AcceptableValueRange<int>(1, 8)));
        GnomeValuablePriorityRadius = Number(config, "Gnome", "ValuablePriorityRadius", 5f, 0f, 50f);
        GnomeAttackOrbitSpeed = Number(config, "Gnome", "AttackOrbitSpeed", 4f, 0.1f, 30f);
        GnomeJumpMultiplier = Multiplier(config, "Gnome", "JumpMultiplier", 1.5f);
        GnomeAttackRadius = Number(config, "Gnome", "AttackRadius", 1.1f, 0.1f, 10f);

        AddExpandedChance(config, EliteArchetype.Headgrab);
        AddExpandedChance(config, EliteArchetype.Headman);
        AddExpandedChance(config, EliteArchetype.HeartHugger);
        AddExpandedChance(config, EliteArchetype.Hidden);

        Huntsman = new EnemyChanceSettings(config, "Huntsman");
        HuntsmanSoundInvestigateRange = Number(config, "Huntsman", "SoundInvestigateRange", 45f, 1f, 500f);

        AddExpandedChance(config, EliteArchetype.Loom);
        AddExpandedChance(config, EliteArchetype.Mentalist);

        AddExpandedChance(config, EliteArchetype.Oogly);
        OoglyLowPatrolHeight = Number(config, "Oogly", "LowPatrolHeight", 1.0f, 0.5f, 4f);
        OoglyLowPatrolIntervalSeconds = Seconds(config, "Oogly", "LowPatrolIntervalSeconds", 10f, 3f, 60f);

        AddExpandedChance(config, EliteArchetype.Peeper);
        AddExpandedChance(config, EliteArchetype.Reaper);

        AddExpandedChance(config, EliteArchetype.Robe);
        RobeUnseenMoveSpeed = Number(config, "Robe", "UnseenMoveSpeed", 8f, 1f, 25f);

        AddExpandedChance(config, EliteArchetype.Rugrat);

        ShadowChild = new EnemyChanceSettings(config, "Shadow Child");
        ShadowEyeContactSeconds = Seconds(config, "Shadow Child", "EyeContactSeconds", 3f, 0.2f, 30f);
        ShadowApproachRadius = Number(config, "Shadow Child", "ApproachRadius", 50f, 1f, 200f);
        ShadowApproachSpeed = Number(config, "Shadow Child", "ApproachSpeed", 4f, 0.1f, 30f);

        AddExpandedChance(config, EliteArchetype.Spewer);
        AddExpandedChance(config, EliteArchetype.Tick);
        AddExpandedChance(config, EliteArchetype.Trudge);
        AddExpandedChance(config, EliteArchetype.Upscream);

        TestSpawnMaxGroups = config.Bind("Testing", "MaxSpawnGroupsPerCommand", 10,
            new ConfigDescription("Maximum setup groups spawned by one command.", new AcceptableValueRange<int>(1, 50)));

        // Regenerates current descriptions after a raw-file migration while
        // retaining orphaned/unknown entries for forward and backward compatibility.
        config.Save();
    }

    internal EnemyChanceSettings ChanceFor(EliteArchetype archetype)
    {
        if (_expandedEnemyChances.TryGetValue(archetype, out EnemyChanceSettings? settings))
        {
            return settings;
        }
        return archetype switch
        {
            EliteArchetype.Clown => Clown,
            EliteArchetype.ShadowChild => ShadowChild,
            EliteArchetype.Huntsman => Huntsman,
            EliteArchetype.Gambit => Gambit,
            EliteArchetype.Banger => Banger,
            EliteArchetype.Gnome => Gnome,
            _ => Clown
        };
    }

    private void AddExpandedChance(ConfigFile config, EliteArchetype archetype)
    {
        _expandedEnemyChances.Add(
            archetype,
            new EnemyChanceSettings(config, EnemyDisplayNameResolver.Get(archetype)));
    }

    private static ConfigEntry<float> Percent(ConfigFile config, string section, string key, float value) =>
        Number(config, section, key, value, 0f, 100f);

    private static ConfigEntry<float> Multiplier(ConfigFile config, string section, string key, float value) =>
        Number(config, section, key, value, 0.1f, 10f);

    private static ConfigEntry<float> Seconds(ConfigFile config, string section, string key, float value, float min, float max) =>
        Number(config, section, key, value, min, max);

    private static ConfigEntry<float> Number(ConfigFile config, string section, string key, float value, float min, float max) =>
        config.Bind(section, key, value, new ConfigDescription(key, new AcceptableValueRange<float>(min, max)));
}
