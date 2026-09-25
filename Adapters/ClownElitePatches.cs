using System;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class ClownElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyBeamer, Quaternion> AimHorizontalTarget =
        AccessTools.FieldRefAccess<EnemyBeamer, Quaternion>("aimHorizontalTarget");
    private static readonly AccessTools.FieldRef<EnemyBeamer, float> AimHorizontalResult =
        AccessTools.FieldRefAccess<EnemyBeamer, float>("aimHorizontalResult");
    private static readonly AccessTools.FieldRef<EnemyBeamer, float> LaserRange =
        AccessTools.FieldRefAccess<EnemyBeamer, float>("laserRange");
    private static readonly AccessTools.FieldRef<EnemyBeamer, Quaternion> AimVerticalTarget =
        AccessTools.FieldRefAccess<EnemyBeamer, Quaternion>("aimVerticalTarget");

    internal static void OnSpawn(EliteState state)
    {
        state.AdapterState = new ClownRuntimeState();
    }

    internal static void Tick(EliteState state)
    {
        EnemyBeamer? clown = state.Enemy.GetComponent<EnemyBeamer>();
        if (clown == null)
        {
            return;
        }

        bool attack = clown.currentState is EnemyBeamer.State.AttackStart or
            EnemyBeamer.State.Attack or EnemyBeamer.State.AttackEnd;
        ClownRuntimeState runtime = GetRuntime(state);
        if (!attack)
        {
            runtime.AttackInitialized = false;
            runtime.TargetSwitched = false;
            runtime.SpinChecked = false;
            runtime.Spinning = false;
            return;
        }

        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        LaserRange(clown) = config.ClownBeamRange.Value;

        PlayerAvatar? playerTarget = GameFieldAccess.BeamerTarget(clown);
        if (playerTarget != null)
        {
            runtime.LastTargetPosition = playerTarget.PlayerVisionTarget != null &&
                                         playerTarget.PlayerVisionTarget.VisionTransform != null
                ? playerTarget.PlayerVisionTarget.VisionTransform.position
                : playerTarget.transform.position;
        }

        if (clown.currentState == EnemyBeamer.State.Attack && !runtime.AttackInitialized)
        {
            runtime.AttackInitialized = true;
            float min = config.ClownBeamDurationMin.Value;
            float max = config.ClownBeamDurationMax.Value;
            clown.stateTimer = Mathf.Lerp(min, max, StableUnit(state.Key, 17));
        }

        if (config.ClownWalkWhileAttacking.Value && playerTarget != null)
        {
            EnemyNavMeshAgent nav = GameFieldAccess.NavMeshAgent(state.Enemy);
            nav.Stop(0f);
            nav.SetDestination(playerTarget.transform.position);
            nav.OverrideAgent(
                GameFieldAccess.NavDefaultSpeed(nav),
                GameFieldAccess.NavDefaultAcceleration(nav),
                0.1f);
        }

        if (config.ClownTargetSwitch.Value && clown.currentState == EnemyBeamer.State.Attack &&
            !runtime.TargetSwitched && TryFindAlternateTarget(clown, LaserRange(clown), out PlayerAvatar alternate))
        {
            runtime.TargetSwitched = true;
            GameFieldAccess.SetBeamerTarget(clown, alternate);
            state.Enemy.SetChaseTarget(alternate);
            if (GameManager.Multiplayer())
            {
                GameFieldAccess.BeamerPhotonView(clown).RPC(
                    "UpdatePlayerTargetRPC", RpcTarget.Others, alternate.photonView.ViewID);
            }
        }

        if (!runtime.SpinChecked && clown.currentState == EnemyBeamer.State.Attack)
        {
            runtime.SpinChecked = true;
            float chance = config.ClownSpinChancePercent.Value;
            runtime.Spinning = StableUnit(state.Key, 41) * 100f < chance;
            runtime.SpinRemaining = runtime.Spinning ? 180f : 0f;
            runtime.SpinYaw = clown.transform.eulerAngles.y;
        }
        if (clown.currentState == EnemyBeamer.State.Attack &&
            GameFieldAccess.BeamerTarget(clown) == null &&
            runtime.LastTargetPosition != Vector3.zero && !runtime.Spinning)
        {
            runtime.Spinning = true;
            runtime.SpinRemaining = 180f;
            runtime.SpinYaw = clown.transform.eulerAngles.y;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyBeamer), "RotationLogic")]
    private static void RotationLogicPrefix(EnemyBeamer __instance)
    {
        if (!EliteRegistry.TryGet(__instance, out EliteState state))
        {
            return;
        }
        bool attack = __instance.currentState is EnemyBeamer.State.AttackStart or
            EnemyBeamer.State.Attack or EnemyBeamer.State.AttackEnd;
        if (!attack)
        {
            return;
        }

        ClownRuntimeState runtime = GetRuntime(state);
        PlayerAvatar? playerTarget = GameFieldAccess.BeamerTarget(__instance);
        Vector3 target = playerTarget != null
            ? (playerTarget.PlayerVisionTarget?.VisionTransform?.position ?? playerTarget.transform.position)
            : runtime.LastTargetPosition;
        Vector3 direction = target - __instance.bottomTransform.position;
        if (direction.sqrMagnitude > 0.001f && !runtime.Spinning)
        {
            Quaternion rotation = Quaternion.LookRotation(direction);
            AimHorizontalTarget(__instance) = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            AimHorizontalResult(__instance) = 0f;
        }
        __instance.horizontalRotationSpring.speed =
            EliteEnemyVariantsPlugin.Instance.Settings.ClownTrackSpeed.Value;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyBeamer), "RotationLogic")]
    private static void RotationLogicPostfix(EnemyBeamer __instance)
    {
        if (!EliteRegistry.TryGet(__instance, out EliteState state))
        {
            return;
        }
        ClownRuntimeState runtime = GetRuntime(state);
        if (!runtime.Spinning || runtime.SpinRemaining <= 0f)
        {
            PlayerAvatar? playerTarget = GameFieldAccess.BeamerTarget(__instance);
            if (playerTarget != null)
            {
                Vector3 point = playerTarget.PlayerVisionTarget?.VisionTransform?.position ??
                                playerTarget.transform.position;
                Vector3 direction = point - __instance.transform.position;
                if (direction.sqrMagnitude > 0.001f)
                {
                    Quaternion target = Quaternion.Euler(0f, Quaternion.LookRotation(direction).eulerAngles.y, 0f);
                    __instance.transform.rotation = Quaternion.RotateTowards(
                        __instance.transform.rotation,
                        target,
                        EliteEnemyVariantsPlugin.Instance.Settings.ClownTrackSpeed.Value * Time.deltaTime);
                }
            }
            return;
        }

        float step = Mathf.Min(runtime.SpinRemaining,
            EliteEnemyVariantsPlugin.Instance.Settings.ClownSpinSpeedDegPerSec.Value * Time.deltaTime);
        runtime.SpinYaw = Mathf.Repeat(runtime.SpinYaw + step, 360f);
        runtime.SpinRemaining -= step;
        __instance.transform.rotation = Quaternion.Euler(0f, runtime.SpinYaw, 0f);
        if (runtime.SpinRemaining <= 0f)
        {
            runtime.Spinning = false;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyBeamer), "VerticalAimLogic")]
    private static void VerticalAimLogicPostfix(EnemyBeamer __instance)
    {
        PlayerAvatar? playerTarget = GameFieldAccess.BeamerTarget(__instance);
        if (!EliteRegistry.TryGet(__instance, out EliteState state) || playerTarget == null ||
            __instance.currentState is not (EnemyBeamer.State.AttackStart or EnemyBeamer.State.Attack or EnemyBeamer.State.AttackEnd))
        {
            return;
        }
        Vector3 point = playerTarget.PlayerVisionTarget?.VisionTransform?.position ??
                        playerTarget.transform.position;
        Vector3 direction = point - __instance.laserRayTransform.position;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }
        Quaternion world = Quaternion.LookRotation(direction);
        Quaternion parentRotation = __instance.aimVerticalTransform.parent != null
            ? __instance.aimVerticalTransform.parent.rotation
            : Quaternion.identity;
        Quaternion local = Quaternion.Inverse(parentRotation) * world;
        local = Quaternion.Euler(local.eulerAngles.x, 0f, 0f);
        float track = EliteEnemyVariantsPlugin.Instance.Settings.ClownTrackSpeed.Value;
        AimVerticalTarget(__instance) = Quaternion.RotateTowards(AimVerticalTarget(__instance), local, track * Time.deltaTime);
        __instance.aimVerticalTransform.localRotation = AimVerticalTarget(__instance);
        __instance.laserRayTransform.localRotation = AimVerticalTarget(__instance);
    }

    private static ClownRuntimeState GetRuntime(EliteState state)
    {
        if (state.AdapterState is not ClownRuntimeState runtime)
        {
            runtime = new ClownRuntimeState();
            state.AdapterState = runtime;
        }
        return runtime;
    }

    private static bool TryFindAlternateTarget(EnemyBeamer clown, float range, out PlayerAvatar result)
    {
        result = null!;
        List<PlayerAvatar> candidates = new();
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!DedicatedBehaviorSupport.CanUseAsUnseenTarget(player) ||
                player == GameFieldAccess.BeamerTarget(clown))
            {
                continue;
            }
            Vector3 point = player.PlayerVisionTarget?.VisionTransform?.position ?? player.transform.position;
            Vector3 direction = point - clown.bottomTransform.position;
            if (direction.magnitude > range || Physics.Raycast(
                    clown.bottomTransform.position,
                    direction.normalized,
                    direction.magnitude,
                    GameFieldAccess.VisionMask(GameFieldAccess.BeamerEnemy(clown)),
                    QueryTriggerInteraction.Ignore))
            {
                continue;
            }
            candidates.Add(player);
        }
        if (candidates.Count == 0)
        {
            return false;
        }
        int index = Mathf.Abs(clown.GetInstanceID()) % candidates.Count;
        result = candidates[index];
        return true;
    }

    private static float StableUnit(int key, int salt)
    {
        uint value = unchecked((uint)(key * 1103515245 + salt * 12345));
        value ^= value >> 16;
        return (value & 0x00FFFFFF) / 16777216f;
    }
}
