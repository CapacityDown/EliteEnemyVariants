using System;
using HarmonyLib;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal static class DedicatedBehaviorSupport
{
    internal static void OnSpawn(EliteState state)
    {
        state.AdapterState = new DedicatedEnemyRuntimeState
        {
            SpawnInitialized = true,
            LastPosition = state.Enemy.transform.position
        };
    }

    internal static DedicatedEnemyRuntimeState Runtime(EliteState state)
    {
        if (state.AdapterState is not DedicatedEnemyRuntimeState runtime)
        {
            runtime = new DedicatedEnemyRuntimeState
            {
                SpawnInitialized = true,
                LastPosition = state.Enemy.transform.position
            };
            state.AdapterState = runtime;
        }
        return runtime;
    }

    internal static bool IsValidTarget(PlayerAvatar? player) =>
        player != null && !GameFieldAccess.PlayerDisabled(player);

    internal static bool CanUseAsUnseenTarget(PlayerAvatar? player) =>
        IsValidTarget(player) &&
        RoleShuffleCompatibility.CanUseAsUnseenEnemyTarget(player);

    internal static bool TryFindNearestPlayer(
        Vector3 origin,
        float maxDistance,
        out PlayerAvatar result,
        PlayerAvatar? exclude = null,
        Enemy? lineOfSightEnemy = null)
    {
        result = null!;
        float bestDistance = maxDistance;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!CanUseAsUnseenTarget(player) || player == exclude)
            {
                continue;
            }
            Vector3 point = player.PlayerVisionTarget?.VisionTransform?.position ?? player.transform.position;
            Vector3 direction = point - origin;
            float distance = direction.magnitude;
            if (distance > bestDistance)
            {
                continue;
            }
            if (lineOfSightEnemy != null && distance > 0.01f && Physics.Raycast(
                    origin,
                    direction / distance,
                    distance,
                    GameFieldAccess.VisionMask(lineOfSightEnemy),
                    QueryTriggerInteraction.Ignore))
            {
                continue;
            }
            bestDistance = distance;
            result = player;
        }
        return result != null;
    }

    internal static void TrackState(DedicatedEnemyRuntimeState runtime, int currentState)
    {
        runtime.PreviousState = currentState;
    }

    internal static void Run<T>(T instance, EliteArchetype archetype, Action<EliteState, T> action)
        where T : Component
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteRegistry.TryGet(instance, out EliteState state) ||
            state.Archetype != archetype || !state.Spawned || state.AdapterTickFaulted)
        {
            return;
        }

        try
        {
            action(state, instance);
            EliteRegistry.RecordAdapterTickSuccess(state);
        }
        catch (Exception exception)
        {
            EliteRegistry.RecordAdapterTickFailure(state, exception, "AI postfix");
        }
    }

    internal static float WindupRate(EliteState state)
    {
        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        return config.BehaviorWindupRate.Value;
    }

    internal static float CooldownRate(EliteState state)
    {
        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        return config.BehaviorCooldownRate.Value;
    }

    internal static float Retention(EliteState state)
    {
        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        return config.BehaviorChaseRetention.Value;
    }

    internal static void Countdown<T>(T instance, AccessTools.FieldRef<T, float> field, float rate)
    {
        float value = field(instance);
        if (rate > 1f && value > 0f)
        {
            field(instance) = Mathf.Max(0f, value - Time.deltaTime * (rate - 1f));
        }
    }

    internal static void Countup<T>(T instance, AccessTools.FieldRef<T, float> field, float rate)
    {
        if (rate > 1f)
        {
            field(instance) = field(instance) + Time.deltaTime * (rate - 1f);
        }
    }

    internal static void RetainCountup<T>(T instance, AccessTools.FieldRef<T, float> field, float retention)
    {
        float value = field(instance);
        if (retention > 0f && value > 0f)
        {
            field(instance) = Mathf.Max(0f, value - Time.deltaTime * retention);
        }
    }

    internal static void RetainCountdown<T>(T instance, AccessTools.FieldRef<T, float> field, float retention)
    {
        float value = field(instance);
        if (retention > 0f && value > 0f)
        {
            field(instance) = value + Time.deltaTime * retention;
        }
    }

}
