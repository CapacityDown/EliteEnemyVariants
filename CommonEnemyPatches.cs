using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class CommonEnemyPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyOnScreen), nameof(EnemyOnScreen.GetOnScreen))]
    private static void EnemyOnScreenGetOnScreenPrefix(EnemyOnScreen __instance, PlayerAvatar _playerAvatar)
    {
        if (_playerAvatar != null && _playerAvatar.photonView != null)
        {
            // Test-spawned enemies can be created after the vanilla PlayerAdded pass.
            // Register lazily before vanilla indexes its per-player dictionaries.
            __instance.PlayerAdded(_playerAvatar.photonView.ViewID);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(SemiFunc),
        nameof(SemiFunc.LevelPointListPurgeObstructed),
        new[] { typeof(List<LevelPoint>), typeof(CapsuleCollider) })]
    private static bool LevelPointListPurgeObstructedPrefix(
        List<LevelPoint> _levelPoints,
        CapsuleCollider _capsuleCollider,
        ref List<LevelPoint> __result)
    {
        if (_capsuleCollider != null || !TestSpawnCommands.IsCreatingTestSummon)
        {
            return true;
        }
        __result = _levelPoints?.FindAll(point => point != null) ?? new List<LevelPoint>();
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Enemy), "Start")]
    private static void EnemyStartPostfix(Enemy __instance)
    {
        EliteRegistry.RegisterCandidate(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyParent), "SpawnRPC")]
    private static void EnemyParentSpawnRpcPrefix(EnemyParent __instance)
    {
        EliteRegistry.SelectOnFirstSpawn(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyParent), "SpawnRPC")]
    private static void EnemyParentSpawnRpcPostfix(EnemyParent __instance)
    {
        EliteRegistry.OnSpawnComplete(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyParent), "DespawnRPC")]
    private static void EnemyParentDespawnRpcPostfix(EnemyParent __instance)
    {
        EliteRegistry.OnDespawn(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyParent), nameof(EnemyParent.SpawnedTimerSet))]
    private static bool EnemyParentSpawnedTimerSetPrefix(EnemyParent __instance, float _time)
    {
        if (_time > 0f ||
            !TestSpawnCommands.TryGetTimerDespawnProtection(__instance, out float remainingSeconds))
        {
            return true;
        }

        // Several vanilla AIs bypass SpawnedTimerPause by writing zero directly when a leave
        // point or recovery path cannot be found. Keep test summons alive for the advertised
        // protection window, while death and designed one-shot despawns remain untouched.
        __instance.SpawnedTimerReset();
        __instance.SpawnedTimerPause(remainingSeconds);
        TestSpawnCommands.ReportBlockedTimerDespawn(__instance, remainingSeconds);
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.HurtRPC))]
    private static void EnemyHealthHurtRpcPostfix(EnemyHealth __instance)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            EliteRegistry.MarkHurt(__instance);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Heal))]
    private static void EnemyHealthHealPrefix(EnemyHealth __instance, int _amount)
    {
        if (_amount > 0 && GameFieldAccess.HealthCurrent(__instance) < __instance.health &&
            SemiFunc.IsMasterClientOrSingleplayer())
        {
            EliteRegistry.MarkRealHeal(__instance);
        }
    }

    [HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyNavMeshAgent), nameof(EnemyNavMeshAgent.UpdateAgent))]
    private static void EnemyNavMeshAgentUpdateAgentPrefix(
        EnemyNavMeshAgent __instance,
        ref float speed,
        ref float acceleration)
    {
        if (!CompatibilityGuard.CommonSpeedDisabled && EliteRegistry.TryGet(__instance, out EliteState state))
        {
            float multiplier = EliteRegistry.GetNormalMoveMultiplier(state);
            speed = Mathf.Max(0.1f, speed * multiplier);
            acceleration = Mathf.Max(0f, acceleration * multiplier);
        }
    }

    [HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyNavMeshAgent), nameof(EnemyNavMeshAgent.OverrideAgent))]
    private static void EnemyNavMeshAgentOverrideAgentPrefix(
        EnemyNavMeshAgent __instance,
        ref float speed,
        ref float acceleration)
    {
        if (!CompatibilityGuard.CommonSpeedDisabled && EliteRegistry.TryGet(__instance, out EliteState state))
        {
            float multiplier = EliteRegistry.GetAttackMoveMultiplier(state);
            speed = Mathf.Max(0.1f, speed * multiplier);
            acceleration = Mathf.Max(0f, acceleration * multiplier);
        }
    }

    [HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyRigidbody), nameof(EnemyRigidbody.OverrideFollowPosition))]
    private static void EnemyRigidbodyOverrideFollowPositionPrefix(EnemyRigidbody __instance, ref float speed)
    {
        if (!CompatibilityGuard.CommonSpeedDisabled && EliteRegistry.TryGet(__instance, out EliteState state))
        {
            speed = Mathf.Max(0.1f, speed * EliteRegistry.GetRigidbodyMoveMultiplier(state));
        }
    }

    [HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyRigidbody), nameof(EnemyRigidbody.OverrideFollowRotation))]
    private static void EnemyRigidbodyOverrideFollowRotationPrefix(EnemyRigidbody __instance, ref float speed)
    {
        if (!CompatibilityGuard.CommonSpeedDisabled && EliteRegistry.TryGet(__instance, out EliteState state))
        {
            speed = Mathf.Max(0.1f, speed * EliteRegistry.GetRigidbodyMoveMultiplier(state));
        }
    }
}
