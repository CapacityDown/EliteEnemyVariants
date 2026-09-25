using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;

namespace REPOJP.EliteEnemyVariants;

internal static class StageFluxCompatibility
{
    internal const string PluginGuid = "REPOJP.StagePhysicsEvents";
}

internal static class RoleShuffleCompatibility
{
    internal const string PluginGuid = "REPOJP.RoleShuffle";
    internal const int MinimumApiVersion = 2;
    internal const int MaximumApiVersion = 2;
    private const string ApiTypeName =
        "REPOJP.StageRoles.RoleShuffleCompatibilityApi";
    private static readonly long ResolutionRetryTicks = TimeSpan.FromSeconds(2).Ticks;

    private enum ResolutionStatus
    {
        Unknown,
        PluginUnavailable,
        ApiTypeMissing,
        ApiVersionMissing,
        ApiVersionUnsupported,
        SignatureMismatch,
        Ready
    }

    private static Assembly? _resolvedAssembly;
    private static MethodInfo? _canUseAsUnseenEnemyTarget;
    private static ResolutionStatus _status;
    private static long _nextResolutionAttemptTicks;
    private static string _lastResolutionLog = string.Empty;
    private static bool _readyLoggedForAssembly;
    private static bool _invocationFailureLoggedForAssembly;

    internal static bool CanUseAsUnseenEnemyTarget(PlayerAvatar? player)
    {
        if (player == null)
        {
            return false;
        }

        Resolve();
        if (_canUseAsUnseenEnemyTarget == null)
        {
            return true;
        }
        try
        {
            return _canUseAsUnseenEnemyTarget.Invoke(null, new object?[] { player })
                is not false;
        }
        catch (Exception exception)
        {
            if (!_invocationFailureLoggedForAssembly)
            {
                _invocationFailureLoggedForAssembly = true;
                EliteEnemyVariantsPlugin.ModLogger?.LogDebug(
                    $"RoleShuffle unseen-target query failed open; the API will be resolved again: " +
                    exception.GetBaseException().Message);
            }

            // A transient invocation failure must not permanently poison the
            // compatibility cache. Retry after a short cooldown while keeping
            // vanilla-equivalent target eligibility in the meantime.
            _canUseAsUnseenEnemyTarget = null;
            _nextResolutionAttemptTicks = DateTime.UtcNow.Ticks + ResolutionRetryTicks;
            return true;
        }
    }

    private static void Resolve()
    {
        if (!Chainloader.PluginInfos.TryGetValue(
                PluginGuid,
                out BepInEx.PluginInfo pluginInfo) ||
            pluginInfo.Instance == null)
        {
            if (_resolvedAssembly != null)
            {
                ResetForAssembly(null);
            }
            SetFailure(
                ResolutionStatus.PluginUnavailable,
                "RoleShuffle compatibility is unavailable; unseen-target checks fail open.");
            return;
        }

        Assembly assembly = pluginInfo.Instance.GetType().Assembly;
        bool assemblyChanged = !ReferenceEquals(_resolvedAssembly, assembly);
        if (assemblyChanged)
        {
            ResetForAssembly(assembly);
        }

        if (_status == ResolutionStatus.Ready &&
            _canUseAsUnseenEnemyTarget != null)
        {
            return;
        }

        long now = DateTime.UtcNow.Ticks;
        if (!assemblyChanged && now < _nextResolutionAttemptTicks)
        {
            return;
        }
        _nextResolutionAttemptTicks = now + ResolutionRetryTicks;

        try
        {
            Type? apiType = assembly.GetType(ApiTypeName, false);
            if (apiType == null || !apiType.IsPublic)
            {
                SetFailure(
                    ResolutionStatus.ApiTypeMissing,
                    "RoleShuffle compatibility API type was not found; unseen-target checks fail open.");
                return;
            }

            if (!TryReadApiVersion(apiType, out int apiVersion))
            {
                SetFailure(
                    ResolutionStatus.ApiVersionMissing,
                    "RoleShuffle compatibility ApiVersion was not readable; unseen-target checks fail open.");
                return;
            }
            if (apiVersion < MinimumApiVersion ||
                apiVersion > MaximumApiVersion)
            {
                SetFailure(
                    ResolutionStatus.ApiVersionUnsupported,
                    $"RoleShuffle compatibility API v{apiVersion} is outside the supported " +
                    $"range v{MinimumApiVersion}-v{MaximumApiVersion}; " +
                    "unseen-target checks fail open.");
                return;
            }

            MethodInfo? method = apiType.GetMethod(
                "CanUseAsUnseenEnemyTarget",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(PlayerAvatar) },
                null);
            if (method == null || method.ReturnType != typeof(bool))
            {
                SetFailure(
                    ResolutionStatus.SignatureMismatch,
                    "RoleShuffle CanUseAsUnseenEnemyTarget signature is incompatible; " +
                    "unseen-target checks fail open.");
                return;
            }

            _canUseAsUnseenEnemyTarget = method;
            _status = ResolutionStatus.Ready;
            _nextResolutionAttemptTicks = 0;
            if (!_readyLoggedForAssembly)
            {
                _readyLoggedForAssembly = true;
                EliteEnemyVariantsPlugin.ModLogger?.LogInfo(
                    $"RoleShuffle compatibility API v{apiVersion} resolved " +
                    $"(supported range v{MinimumApiVersion}-v{MaximumApiVersion}).");
            }
        }
        catch (Exception exception)
        {
            SetFailure(
                ResolutionStatus.SignatureMismatch,
                "RoleShuffle compatibility resolution failed open: " +
                exception.GetBaseException().Message);
        }
    }

    private static bool TryReadApiVersion(Type apiType, out int apiVersion)
    {
        const BindingFlags flags = BindingFlags.Public |
                                   BindingFlags.Static |
                                   BindingFlags.FlattenHierarchy;

        FieldInfo? field = apiType.GetField("ApiVersion", flags);
        if (field?.FieldType == typeof(int))
        {
            object? value = field.IsLiteral
                ? field.GetRawConstantValue()
                : field.GetValue(null);
            if (value is int fieldVersion)
            {
                apiVersion = fieldVersion;
                return true;
            }
        }

        PropertyInfo? property = apiType.GetProperty("ApiVersion", flags);
        if (property?.PropertyType == typeof(int) &&
            property.GetIndexParameters().Length == 0 &&
            property.GetMethod?.IsStatic == true &&
            property.GetValue(null) is int propertyVersion)
        {
            apiVersion = propertyVersion;
            return true;
        }

        apiVersion = 0;
        return false;
    }

    private static void ResetForAssembly(Assembly? assembly)
    {
        _resolvedAssembly = assembly;
        _canUseAsUnseenEnemyTarget = null;
        _status = ResolutionStatus.Unknown;
        _nextResolutionAttemptTicks = 0;
        _lastResolutionLog = string.Empty;
        _readyLoggedForAssembly = false;
        _invocationFailureLoggedForAssembly = false;
    }

    private static void SetFailure(ResolutionStatus status, string message)
    {
        _canUseAsUnseenEnemyTarget = null;
        _status = status;
        if (string.Equals(_lastResolutionLog, message, StringComparison.Ordinal))
        {
            return;
        }

        _lastResolutionLog = message;
        EliteEnemyVariantsPlugin.ModLogger?.LogDebug(message);
    }
}

internal static class CompatibilityGuard
{
    private static readonly Dictionary<EliteArchetype, string> ConflictingAdapters = new()
    {
        [EliteArchetype.Clown] = "REPOJP.ClownBeamTrackingEnhancements",
        [EliteArchetype.ShadowChild] = "REPOJP.ShadowChildEnhancements",
        [EliteArchetype.Huntsman] = "REPOJP.HuntsmanEyesOpened",
        [EliteArchetype.Gambit] = "REPOJP.GambitEnhancements",
        [EliteArchetype.Banger] = "REPOJP.GnomeBangerEnhancements",
        [EliteArchetype.Gnome] = "REPOJP.GnomeBangerEnhancements"
    };

    private static readonly HashSet<EliteArchetype> DisabledAdapters = new();
    internal static bool CommonSpeedDisabled { get; private set; }

    internal static void Refresh(ManualLogSource logger)
    {
        DisabledAdapters.Clear();
        foreach ((EliteArchetype archetype, string guid) in ConflictingAdapters)
        {
            if (!Chainloader.PluginInfos.ContainsKey(guid))
            {
                continue;
            }
            DisabledAdapters.Add(archetype);
            logger.LogWarning(
                $"{EnemyDisplayNameResolver.Get(archetype)} adapter disabled because conflicting plugin '{guid}' is loaded.");
        }

        CommonSpeedDisabled = Chainloader.PluginInfos.ContainsKey("REPOJP.EnemySpeedUp");
        if (CommonSpeedDisabled)
        {
            logger.LogWarning("Common movement multipliers disabled because REPOJP.EnemySpeedUp is loaded.");
        }

        logger.LogInfo($"Stage Flux detected: {Chainloader.PluginInfos.ContainsKey(StageFluxCompatibility.PluginGuid)}; " +
                       $"Role Shuffle detected: {Chainloader.PluginInfos.ContainsKey(RoleShuffleCompatibility.PluginGuid)}.");
    }

    internal static bool IsAdapterEnabled(EliteArchetype archetype) => !DisabledAdapters.Contains(archetype);

    internal static void DisableAdapter(EliteArchetype archetype, System.Exception exception, ManualLogSource logger)
    {
        DisabledAdapters.Add(archetype);
        logger.LogError($"{EnemyDisplayNameResolver.Get(archetype)} adapter disabled after patch validation failed: {exception}");
    }
}
