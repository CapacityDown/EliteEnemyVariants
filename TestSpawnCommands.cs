using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal readonly struct TestEnemyCatalogEntry
{
    internal readonly int Id;
    internal readonly EliteArchetype Archetype;
    internal readonly string DisplayName;

    internal TestEnemyCatalogEntry(int id, EliteArchetype archetype)
    {
        Id = id;
        Archetype = archetype;
        DisplayName = EnemyDisplayNameResolver.Get(archetype);
    }
}

internal static class TestEnemyCatalog
{
    // Stable alphabetical numbering. Do not reorder within the v4 series.
    internal static readonly TestEnemyCatalogEntry[] Entries =
    {
        Entry(1, EliteArchetype.Animal),
        Entry(2, EliteArchetype.ApexPredator),
        Entry(3, EliteArchetype.Banger),
        Entry(4, EliteArchetype.Bella),
        Entry(5, EliteArchetype.BirthdayBoy),
        Entry(6, EliteArchetype.Bowtie),
        Entry(7, EliteArchetype.Chef),
        Entry(8, EliteArchetype.CleanupCrew),
        Entry(9, EliteArchetype.Clown),
        Entry(10, EliteArchetype.Elsa),
        Entry(11, EliteArchetype.Gambit),
        Entry(12, EliteArchetype.Gnome),
        Entry(13, EliteArchetype.Headgrab),
        Entry(14, EliteArchetype.Headman),
        Entry(15, EliteArchetype.HeartHugger),
        Entry(16, EliteArchetype.Hidden),
        Entry(17, EliteArchetype.Huntsman),
        Entry(18, EliteArchetype.Loom),
        Entry(19, EliteArchetype.Mentalist),
        Entry(20, EliteArchetype.Oogly),
        Entry(21, EliteArchetype.Peeper),
        Entry(22, EliteArchetype.Reaper),
        Entry(23, EliteArchetype.Robe),
        Entry(24, EliteArchetype.Rugrat),
        Entry(25, EliteArchetype.ShadowChild),
        Entry(26, EliteArchetype.Spewer),
        Entry(27, EliteArchetype.Tick),
        Entry(28, EliteArchetype.Trudge),
        Entry(29, EliteArchetype.Upscream)
    };

    internal static bool TryResolve(string value, out TestEnemyCatalogEntry entry)
    {
        if (int.TryParse(value.Trim(), out int id))
        {
            foreach (TestEnemyCatalogEntry candidate in Entries)
            {
                if (candidate.Id == id)
                {
                    entry = candidate;
                    return true;
                }
            }
        }
        else
        {
            string normalized = Normalize(value);
            foreach (TestEnemyCatalogEntry candidate in Entries)
            {
                if (Normalize(candidate.DisplayName) == normalized)
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }

    private static TestEnemyCatalogEntry Entry(int id, EliteArchetype archetype) => new(id, archetype);

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

internal static class TestSpawnCommands
{
    private const string SpawnCommandName = "eevspawn";
    private const string ShortSpawnCommandName = "eevs";
    private const string ListCommandName = "eevlist";
    private const string ShortListCommandName = "eevl";
    private const float InitialDespawnTimeoutSeconds = 2f;
    private const float TestSpawnProtectionSeconds = 15f * 60f;

    private static readonly MethodInfo EnemyParentSpawnMethod =
        AccessTools.DeclaredMethod(typeof(EnemyParent), "Spawn");

    private static DebugCommandHandler? _registeredHandler;
    private static readonly Dictionary<int, float> ProtectedParents = new();
    private static readonly HashSet<int> ReportedBlockedTimerDespawns = new();
    private static string? _responseText;
    private static Color _responseColor;
    private static Color _restoreChatColor = Color.white;
    private static float _responseUntil;
    private static bool _restoreColorCaptured;
    private static bool _responseWasShown;
    private static int _summonCreationDepth;

    internal static bool IsCreatingTestSummon => _summonCreationDepth > 0;

    internal static void Tick()
    {
        RegisterIfAvailable();
    }

    private static void RegisterIfAvailable()
    {
        DebugCommandHandler? handler = DebugCommandHandler.instance;
        if (handler == null || ReferenceEquals(handler, _registeredHandler))
        {
            return;
        }

        handler.Register(new DebugCommandHandler.ChatCommand(
            SpawnCommandName,
            "Spawn a forced Normal or Enhanced enemy by display name or ID",
            ExecuteSpawn,
            SuggestSpawn,
            CanInterceptCommands,
            debugOnly: false));
        handler.Register(new DebugCommandHandler.ChatCommand(
            ShortSpawnCommandName,
            "Short alias for /eevspawn",
            ExecuteSpawn,
            SuggestSpawn,
            CanInterceptCommands,
            debugOnly: false));
        handler.Register(new DebugCommandHandler.ChatCommand(
            ListCommandName,
            "List stable Elite Enemy Variants enemy IDs (page 1-3)",
            ExecuteList,
            SuggestList,
            CanInterceptCommands,
            debugOnly: false));
        handler.Register(new DebugCommandHandler.ChatCommand(
            ShortListCommandName,
            "Short alias for /eevlist",
            ExecuteList,
            SuggestList,
            CanInterceptCommands,
            debugOnly: false));
        _registeredHandler = handler;
    }

    internal static void RenderChatResponse()
    {
        if (string.IsNullOrEmpty(_responseText))
        {
            return;
        }

        ChatManager? manager = ChatManager.instance;
        ChatUI? chat = ChatUI.instance;
        if (manager == null || chat == null || chat.chatText == null)
        {
            return;
        }

        if (manager.StateIsActive())
        {
            ClearChatResponse(chat);
            return;
        }
        if (Time.unscaledTime >= _responseUntil)
        {
            ClearChatResponse(chat);
            return;
        }
        if (manager.StateIsSend() || manager.StateIsPossessed())
        {
            return;
        }

        if (!_restoreColorCaptured)
        {
            _restoreChatColor = chat.chatText.color;
            _restoreColorCaptured = true;
        }
        chat.chatText.text = _responseText;
        chat.chatText.color = _responseColor;
        chat.Show();
        if (!_responseWasShown)
        {
            chat.SemiUITextFlashColor(_responseColor, 0.2f);
            chat.SemiUISpringScale(0.05f, 5f, 0.2f);
            _responseWasShown = true;
        }
    }

    private static bool CanInterceptCommands()
    {
        EliteEnemyVariantsPlugin plugin = EliteEnemyVariantsPlugin.Instance;
        return plugin != null &&
               SemiFunc.IsMasterClientOrSingleplayer();
    }

    private static bool CanUseCommands()
    {
        EliteEnemyVariantsPlugin plugin = EliteEnemyVariantsPlugin.Instance;
        return CanInterceptCommands() &&
               plugin.Settings.Enabled.Value &&
               !SemiFunc.RunIsLobbyMenu() &&
               !SemiFunc.RunIsLobby() &&
               LevelGenerator.Instance != null &&
               LevelGenerator.Instance.Generated &&
               EnemyDirector.instance != null;
    }

    private static void ExecuteSpawn(bool isDebugConsole, string[] args)
    {
        if (!CanUseCommands())
        {
            Respond("Enemy summons require the enabled host mod and a generated gameplay level.", Color.red, 8f);
            return;
        }
        if (args.Length < 1)
        {
            Respond("Usage: /eevs [normal|enhanced|e] <ID|display name> [count]", Color.red, 8f);
            return;
        }
        int targetStart = TryParseTier(args[0], out EnhancementTier tier) ? 1 : 0;
        if (targetStart >= args.Length)
        {
            Respond("Usage: /eevs [normal|enhanced|e] <ID|display name> [count]", Color.red, 8f);
            return;
        }

        int count = 1;
        int targetEnd = args.Length;
        if (args.Length - targetStart >= 2 && int.TryParse(args[^1], out int parsedCount))
        {
            count = parsedCount;
            targetEnd--;
        }

        int maxCount = EliteEnemyVariantsPlugin.Instance.Settings.TestSpawnMaxGroups.Value;
        if (count < 1 || count > maxCount)
        {
            Respond($"Count must be between 1 and {maxCount}.", Color.red, 6f);
            return;
        }

        string target = string.Join(" ", args.Skip(targetStart).Take(targetEnd - targetStart));
        if (!TestEnemyCatalog.TryResolve(target, out TestEnemyCatalogEntry catalogEntry))
        {
            Respond($"Unknown enemy '{target}'. Use /eevl 1-3.", Color.red, 8f);
            return;
        }

        EnemySetup? setup = FindSetup(catalogEntry.Archetype);
        if (setup == null)
        {
            Respond($"{catalogEntry.DisplayName} is not present in the loaded enemy pools.", Color.red, 8f);
            return;
        }

        LevelPoint? point = SemiFunc.LevelPointsGetClosestToLocalPlayer();
        if (point == null)
        {
            Respond("No level point is available near the local player.", Color.red, 8f);
            return;
        }

        int forcedEnemies = 0;
        int roomObjects = 0;
        for (int groupIndex = 0; groupIndex < count; groupIndex++)
        {
            foreach (PrefabRef prefabRef in setup.spawnObjects)
            {
                if (prefabRef == null || !prefabRef.IsValid())
                {
                    continue;
                }

                GameObject spawned;
                _summonCreationDepth++;
                try
                {
                    spawned = GameManager.instance.gameMode != 0
                        ? PhotonNetwork.InstantiateRoomObject(prefabRef.ResourcePath, point.transform.position, Quaternion.identity, 0)
                        : UnityEngine.Object.Instantiate(prefabRef.Prefab, point.transform.position, Quaternion.identity);
                }
                finally
                {
                    _summonCreationDepth--;
                }
                if (spawned == null)
                {
                    continue;
                }
                roomObjects++;

                Enemy? enemy = spawned.GetComponentInChildren<Enemy>(true);
                if (enemy != null &&
                    EnemyDisplayNameResolver.TryResolve(enemy, out EliteArchetype spawnedArchetype) &&
                    spawnedArchetype == catalogEntry.Archetype &&
                    EliteRegistry.ForceTierForTest(enemy, tier))
                {
                    forcedEnemies++;
                }

                EnemyParent? parent = spawned.GetComponent<EnemyParent>();
                if (parent != null)
                {
                    // EnemyParent.Setup always performs one initial Despawn before its first real
                    // Spawn. Point that first Spawn back at the requested test location, then let
                    // the coroutine below resume it through EnemyParent's vanilla Spawn method.
                    GameFieldAccess.SetParentFirstSpawnPoint(parent, point);
                    GameFieldAccess.SetParentFirstSpawnPointUsed(parent, false);
                    GameFieldAccess.SetParentSetupDone(parent, true);
                    enemy?.EnemyTeleported(point.transform.position);
                    EliteEnemyVariantsPlugin.Instance.StartCoroutine(CompleteInitialTestSpawn(parent));
                }
            }
        }

        if (forcedEnemies == 0 && tier != EnhancementTier.Normal)
        {
            Respond($"Spawned {roomObjects} room object(s), but no {catalogEntry.DisplayName} tier could be forced.", Color.red, 8f);
            return;
        }

        string tierName = tier.ToString();
        Respond($"Spawned {count} {tierName} {catalogEntry.Id}: {catalogEntry.DisplayName} group(s) " +
                $"({forcedEnemies} enemy object(s)).", Color.green, 8f);
        EliteEnemyVariantsPlugin.ModLogger.LogInfo(
            $"Test command spawned {count} {tierName} {catalogEntry.DisplayName} group(s); " +
            $"roomObjects={roomObjects}, forcedEnemies={forcedEnemies}.");
    }

    private static IEnumerator CompleteInitialTestSpawn(EnemyParent parent)
    {
        float timeoutAt = Time.realtimeSinceStartup + InitialDespawnTimeoutSeconds;
        while (parent != null &&
               GameFieldAccess.ParentSpawned(parent) &&
               Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
        }

        if (parent == null)
        {
            yield break;
        }
        if (GameFieldAccess.ParentSpawned(parent))
        {
            EliteEnemyVariantsPlugin.ModLogger.LogWarning(
                "Test summon did not reach EnemyParent's initial despawn before the timeout; " +
                "the vanilla lifecycle will finish the spawn instead.");
            yield break;
        }

        try
        {
            // This initializes SpawnedTimer, broadcasts SpawnRPC to vanilla clients, and runs
            // the normal enemy-specific OnSpawn event. The forced tier was retained in registry.
            EnemyParentSpawnMethod.Invoke(parent, null);
            if (!GameFieldAccess.ParentSpawned(parent))
            {
                EliteEnemyVariantsPlugin.ModLogger.LogWarning(
                    "Test summon could not complete its first vanilla spawn; its normal retry lifecycle remains active.");
                yield break;
            }
            ProtectFromTimerDespawn(parent);
            if (EnemyDirector.instance != null)
            {
                // Test summons are expected to act immediately. Vanilla's stage-wide initial
                // idle pause otherwise leaves them inert for roughly two to three minutes.
                GameFieldAccess.EndEnemySpawnIdlePause(EnemyDirector.instance);
            }
        }
        catch (Exception exception)
        {
            EliteEnemyVariantsPlugin.ModLogger.LogError(
                $"Failed to complete test summon through EnemyParent.Spawn: {exception}");
        }
    }

    internal static bool TryGetTimerDespawnProtection(EnemyParent parent, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (parent == null || !ProtectedParents.TryGetValue(parent.GetInstanceID(), out float protectedUntil))
        {
            return false;
        }

        remainingSeconds = protectedUntil - Time.time;
        if (remainingSeconds <= 0f)
        {
            ProtectedParents.Remove(parent.GetInstanceID());
            ReportedBlockedTimerDespawns.Remove(parent.GetInstanceID());
            return false;
        }

        Enemy? enemy = parent.GetComponentInChildren<Enemy>(true);
        if (enemy == null)
        {
            return false;
        }
        if (GameFieldAccess.HasHealth(enemy))
        {
            EnemyHealth health = GameFieldAccess.Health(enemy);
            if (health == null || GameFieldAccess.IsDead(health) || GameFieldAccess.HealthCurrent(health) <= 0)
            {
                return false;
            }
        }

        // Peeper is intentionally a one-attack enemy. Its attack-finished despawn is part of
        // the behavior under test and must not be converted into a persistent inert object.
        return !EnemyDisplayNameResolver.TryResolve(enemy, out EliteArchetype archetype) ||
               archetype != EliteArchetype.Peeper;
    }

    internal static void ReportBlockedTimerDespawn(EnemyParent parent, float remainingSeconds)
    {
        if (ReportedBlockedTimerDespawns.Add(parent.GetInstanceID()))
        {
            EliteEnemyVariantsPlugin.ModLogger.LogInfo(
                $"Blocked an AI timer despawn for a test summon; protection remaining={remainingSeconds:F1}s.");
        }
    }

    internal static void ClearStage()
    {
        ProtectedParents.Clear();
        ReportedBlockedTimerDespawns.Clear();
        _summonCreationDepth = 0;
    }

    private static void ProtectFromTimerDespawn(EnemyParent parent)
    {
        ProtectedParents[parent.GetInstanceID()] = Time.time + TestSpawnProtectionSeconds;
        ReportedBlockedTimerDespawns.Remove(parent.GetInstanceID());
        parent.SpawnedTimerPause(TestSpawnProtectionSeconds);
    }

    private static void ExecuteList(bool isDebugConsole, string[] args)
    {
        int page = 1;
        if (args.Length > 0 && (!int.TryParse(args[0], out page) || page < 1 || page > 3))
        {
            Respond("Usage: /eevl [1|2|3]", Color.red, 6f);
            return;
        }

        const int pageSize = 10;
        IEnumerable<TestEnemyCatalogEntry> entries = TestEnemyCatalog.Entries
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
        string text = $"Enemy IDs {page}/3\n" +
                      string.Join("\n", entries.Select(entry => $"{entry.Id}: {entry.DisplayName}"));
        Respond(text, Color.white, 15f);
        EliteEnemyVariantsPlugin.ModLogger.LogInfo(text.Replace('\n', ' '));
    }

    private static List<string> SuggestSpawn(bool isDebugConsole, string partial, string[] args)
    {
        string[] tiers = { "normal", "enhanced", "e" };
        if (args.Length == 0)
        {
            return tiers.ToList();
        }

        bool hasTier = tiers.Contains(args[0].ToLowerInvariant());
        if (args.Length == 1 && !hasTier)
        {
            string value = args[0].ToLowerInvariant();
            List<string> tierMatches = tiers.Where(tier => tier.StartsWith(value)).ToList();
            if (tierMatches.Count > 0)
            {
                return tierMatches;
            }
        }
        string tierPrefix = hasTier ? args[0].ToLowerInvariant() + " " : string.Empty;
        string target = string.Join(" ", args.Skip(hasTier ? 1 : 0));
        return TestEnemyCatalog.Entries
            .Where(entry => string.IsNullOrWhiteSpace(target) ||
                            entry.Id.ToString().StartsWith(target, StringComparison.OrdinalIgnoreCase) ||
                            entry.DisplayName.StartsWith(target, StringComparison.OrdinalIgnoreCase))
            .Select(entry => $"{tierPrefix}{entry.Id}")
            .ToList();
    }

    private static List<string> SuggestList(bool isDebugConsole, string partial, string[] args) =>
        new[] { "1", "2", "3" }
            .Where(page => args.Length == 0 || page.StartsWith(args[0], StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static bool TryParseTier(string value, out EnhancementTier tier)
    {
        switch (value.ToLowerInvariant())
        {
            case "normal":
                tier = EnhancementTier.Normal;
                return true;
            case "enhanced":
            case "e":
                tier = EnhancementTier.Enhanced;
                return true;
            default:
                tier = EnhancementTier.Enhanced;
                return false;
        }
    }

    private static EnemySetup? FindSetup(EliteArchetype archetype)
    {
        IEnumerable<EnemySetup> setups = EnemyDirector.instance.enemiesDifficulty1
            .Concat(EnemyDirector.instance.enemiesDifficulty2)
            .Concat(EnemyDirector.instance.enemiesDifficulty3)
            .Where(setup => setup != null)
            .Distinct();

        foreach (EnemySetup setup in setups)
        {
            foreach (PrefabRef prefabRef in setup.spawnObjects)
            {
                GameObject? prefab = prefabRef?.Prefab;
                Enemy? enemy = prefab?.GetComponentInChildren<Enemy>(true);
                if (enemy != null &&
                    EnemyDisplayNameResolver.TryResolve(enemy, out EliteArchetype candidate) &&
                    candidate == archetype)
                {
                    return setup;
                }
            }
        }
        return null;
    }

    private static void Respond(string text, Color color, float duration)
    {
        _responseText = text;
        _responseColor = color;
        _responseUntil = Time.unscaledTime + duration;
        _responseWasShown = false;
        EliteEnemyVariantsPlugin.ModLogger.LogInfo(text.Replace('\n', ' '));
    }

    private static void ClearChatResponse(ChatUI chat)
    {
        chat.chatText.text = string.Empty;
        if (_restoreColorCaptured)
        {
            chat.chatText.color = _restoreChatColor;
        }
        chat.Hide();
        _responseText = null;
        _responseUntil = 0f;
        _restoreColorCaptured = false;
        _responseWasShown = false;
    }
}

[HarmonyPatch(typeof(ChatManager), "Update")]
internal static class TestCommandChatResponsePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        TestSpawnCommands.RenderChatResponse();
    }
}
