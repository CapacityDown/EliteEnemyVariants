using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteEnemyConfigMigration
{
    internal const int CurrentSchemaVersion = 7;

    private static readonly string[] EnemySections =
    {
        "Animal", "Birthday Boy", "Cleanup Crew", "Bowtie", "Peeper", "Apex Predator",
        "Elsa", "Mentalist", "Headman", "Headgrab", "Heart Hugger", "Hidden", "Oogly",
        "Robe", "Reaper", "Loom", "Spewer", "Trudge", "Tick", "Bella", "Chef",
        "Upscream", "Rugrat", "Clown", "Shadow Child", "Huntsman", "Gambit", "Banger", "Gnome"
    };

    private static readonly ConfigDefinition SchemaDefinition =
        new("Migration", "ConfigVersion");

    internal static void Apply(ConfigFile config)
    {
        string path = config.ConfigFilePath;
        string temporaryPath = path + ".elite-enemy-variants.tmp";
        try
        {
            Dictionary<ConfigDefinition, string> values = ReadValues(path);
            if (values.Count == 0 && !File.Exists(path))
            {
                return;
            }

            int sourceVersion = ReadSchemaVersion(values);
            if (sourceVersion > CurrentSchemaVersion)
            {
                EliteEnemyVariantsPlugin.ModLogger.LogWarning(
                    $"Config schema {sourceVersion} is newer than supported schema " +
                    $"{CurrentSchemaVersion}; values were loaded without migration.");
                return;
            }

            int transferred = 0;
            int removed = 0;
            if (sourceVersion < 2)
            {
                // Huntsman remains blind. Reuse the old Apex distance preference
                // only as the maximum range for its sound-originated follow-up shot.
                transferred += TransferIfMissing(
                    values,
                    "Huntsman",
                    "ApexSightDistance",
                    "Huntsman",
                    "ApexFollowUpMaxDistance");
            }
            if (sourceVersion < 3)
            {
                transferred += MergeTierChances(values, "Selection", "EnhancedChancePercent", 32f, 3f);
                foreach (string section in EnemySections)
                {
                    transferred += MergeTierChances(values, section, "ChancePercent", -1f, -1f);
                }
                transferred += TransferIfMissing(
                    values, "Banger", "ApexNearTargetFuseSeconds", "Banger", "NearTargetFuseSeconds");
                transferred += TransferIfMissing(
                    values, "Banger", "ApexNearTargetDistance", "Banger", "NearTargetDistance");
                transferred += TransferIfMissing(
                    values, "Gnome", "ApexConcurrentAttackers", "Gnome", "ConcurrentAttackers");
                RemoveObsoleteTierSettings(values);
            }
            if (values.Remove(new ConfigDefinition("Testing", "EnableTestCommands")))
            {
                removed++;
            }
            if (values.Remove(new ConfigDefinition("Selection", "GnomeCapCountsMembers")))
            {
                removed++;
            }
            ConfigDefinition oldCommonStatScale = new("Common", "CommonStatScale");
            ConfigDefinition commonStatsEnabled = new("Common", "CommonStatsEnabled");
            if (values.TryGetValue(oldCommonStatScale, out string? commonStatScale))
            {
                if (!values.ContainsKey(commonStatsEnabled))
                {
                    bool enabled = !float.TryParse(
                        commonStatScale,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float scale) || scale > 0f;
                    values[commonStatsEnabled] = enabled.ToString();
                    transferred++;
                }
                values.Remove(oldCommonStatScale);
                removed++;
            }
            if (sourceVersion < 6)
            {
                ConfigDefinition threatBudgetEnabled = new("Selection", "ThreatBudgetEnabled");
                if (!values.TryGetValue(threatBudgetEnabled, out string? serializedThreatBudget) ||
                    !bool.TryParse(serializedThreatBudget, out bool threatBudgetWasEnabled) ||
                    !threatBudgetWasEnabled)
                {
                    values[threatBudgetEnabled] = bool.TrueString;
                    transferred++;
                }
            }
            foreach (string section in EnemySections)
            {
                ConfigDefinition chancePercent = new(section, "ChancePercent");
                if (!values.TryGetValue(chancePercent, out string? serializedChance) ||
                    !float.TryParse(
                        serializedChance,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float parsedChance))
                {
                    continue;
                }

                int normalizedChance = parsedChance < 0f
                    ? -1
                    : Math.Min(
                        100,
                        Math.Max(
                            0,
                            (int)Math.Round(parsedChance, MidpointRounding.AwayFromZero)));
                string normalizedSerialized = normalizedChance.ToString(CultureInfo.InvariantCulture);
                if (!string.Equals(serializedChance, normalizedSerialized, StringComparison.Ordinal))
                {
                    values[chancePercent] = normalizedSerialized;
                    transferred++;
                }
            }
            bool schemaChanged = sourceVersion != CurrentSchemaVersion;
            values[SchemaDefinition] = CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture);
            if (!schemaChanged && transferred == 0 && removed == 0)
            {
                return;
            }

            if (File.Exists(path) && values.Count > 1)
            {
                string backupPath = path + $".pre-schema-{CurrentSchemaVersion}.bak";
                if (!File.Exists(backupPath))
                {
                    File.Copy(path, backupPath, overwrite: false);
                }
            }

            WriteValues(temporaryPath, values);
            File.Copy(temporaryPath, path, overwrite: true);
            File.Delete(temporaryPath);
            config.Reload();
            EliteEnemyVariantsPlugin.ModLogger.LogInfo(
                $"Config migrated from schema {sourceVersion} to {CurrentSchemaVersion}; " +
                $"{transferred} value(s) migrated, {removed} obsolete value(s) removed, " +
                "and all other existing values retained.");
        }
        catch (Exception exception)
        {
            TryDelete(temporaryPath);
            EliteEnemyVariantsPlugin.ModLogger.LogWarning(
                $"Config migration was skipped and the original file was retained: {exception}");
        }
    }

    internal static int TransferIfMissing(
        IDictionary<ConfigDefinition, string> values,
        string oldSection,
        string oldKey,
        string newSection,
        string newKey)
    {
        ConfigDefinition oldDefinition = new(oldSection, oldKey);
        ConfigDefinition newDefinition = new(newSection, newKey);
        if (!values.TryGetValue(oldDefinition, out string? value) ||
            values.ContainsKey(newDefinition))
        {
            return 0;
        }
        values[newDefinition] = value;
        return 1;
    }

    private static int MergeTierChances(
        IDictionary<ConfigDefinition, string> values,
        string section,
        string newKey,
        float defaultElite,
        float defaultApex)
    {
        ConfigDefinition target = new(section, newKey);
        if (values.ContainsKey(target))
        {
            return 0;
        }
        float elite = ReadFloat(values, section, "EliteChancePercent", defaultElite);
        float apex = ReadFloat(values, section, "ApexChancePercent", defaultApex);
        float merged = elite < 0f && apex < 0f
            ? -1f
            : Math.Min(100f, Math.Max(0f, elite) + Math.Max(0f, apex));
        values[target] = merged.ToString(CultureInfo.InvariantCulture);
        return 1;
    }

    private static float ReadFloat(
        IDictionary<ConfigDefinition, string> values,
        string section,
        string key,
        float fallback) =>
        values.TryGetValue(new ConfigDefinition(section, key), out string? serialized) &&
        float.TryParse(serialized, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
            ? result
            : fallback;

    private static void RemoveObsoleteTierSettings(IDictionary<ConfigDefinition, string> values)
    {
        values.Remove(new ConfigDefinition("Selection", "EliteChancePercent"));
        values.Remove(new ConfigDefinition("Selection", "ApexChancePercent"));
        values.Remove(new ConfigDefinition("Selection", "MaxApexPerLevel"));
        foreach (string section in EnemySections)
        {
            values.Remove(new ConfigDefinition(section, "EliteChancePercent"));
            values.Remove(new ConfigDefinition(section, "ApexChancePercent"));
        }
        string[] obsolete =
        {
            "Apex|HealthMultiplier", "Apex|NavMoveMultiplier", "Apex|NavAttackMoveMultiplier",
            "Apex|RigidbodyMoveMultiplier", "Visual|ApexPulseCycleSeconds", "Visual|ApexPulseGapSeconds",
            "Behavior|ApexWindupRate", "Behavior|ApexCooldownRate", "Behavior|ApexChaseRetention",
            "Clown|ApexTrackSpeed", "Clown|ApexBeamRange", "Clown|ApexBeamDurationMin",
            "Clown|ApexBeamDurationMax", "Clown|ApexSpinChancePercent",
            "Shadow Child|ApexEyeContactSeconds", "Shadow Child|ApexApproachSpeed",
            "Shadow Child|ApexRememberedTargets", "Huntsman|ApexFollowUpMaxDistance",
            "Huntsman|ApexBurstShots", "Huntsman|ApexBurstGapSeconds", "Huntsman|ApexLeadSeconds",
            "Gambit|ApexEscapeWaypoints", "Gambit|ApexAttackMoveMultiplier", "Gambit|ApexMaxCarrySeconds",
            "Gambit|ReleaseGrabDistance", "Gambit|PersistCarryToGoal", "Banger|ApexNearTargetFuseSeconds",
            "Banger|ApexNearTargetDistance", "Gnome|ApexHealth", "Gnome|ApexAttackOrbitSpeed",
            "Gnome|ApexJumpMultiplier", "Gnome|ApexAttackRadius", "Gnome|ApexConcurrentAttackers"
        };
        foreach (string entry in obsolete)
        {
            string[] parts = entry.Split('|');
            values.Remove(new ConfigDefinition(parts[0], parts[1]));
        }
    }

    private static Dictionary<ConfigDefinition, string> ReadValues(string path)
    {
        Dictionary<ConfigDefinition, string> values = new();
        if (!File.Exists(path))
        {
            return values;
        }

        string section = string.Empty;
        foreach (string sourceLine in File.ReadAllLines(path))
        {
            string line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }
            if (line.StartsWith("[", StringComparison.Ordinal) &&
                line.EndsWith("]", StringComparison.Ordinal))
            {
                section = line.Substring(1, line.Length - 2);
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }
            string key = line.Substring(0, separator).Trim();
            string value = line.Substring(separator + 1).Trim();
            values[new ConfigDefinition(section, key)] = value;
        }
        return values;
    }

    private static int ReadSchemaVersion(
        IReadOnlyDictionary<ConfigDefinition, string> values) =>
        values.TryGetValue(SchemaDefinition, out string? serialized) &&
        int.TryParse(
            serialized,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int version)
            ? Math.Max(0, version)
            : 0;

    private static void WriteValues(
        string path,
        IReadOnlyDictionary<ConfigDefinition, string> values)
    {
        List<string> lines = new()
        {
            $"## EliteEnemyVariants configuration schema {CurrentSchemaVersion}",
            "## Existing and unknown settings are retained during migration.",
            string.Empty
        };
        foreach (IGrouping<string, KeyValuePair<ConfigDefinition, string>> section in
                 values
                     .OrderBy(pair => pair.Key.Section, StringComparer.Ordinal)
                     .ThenBy(pair => pair.Key.Key, StringComparer.Ordinal)
                     .GroupBy(pair => pair.Key.Section))
        {
            lines.Add($"[{section.Key}]");
            lines.Add(string.Empty);
            foreach (KeyValuePair<ConfigDefinition, string> pair in section)
            {
                lines.Add($"{pair.Key.Key} = {pair.Value}");
            }
            lines.Add(string.Empty);
        }
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // The original config remains usable even if temporary cleanup fails.
        }
    }
}
