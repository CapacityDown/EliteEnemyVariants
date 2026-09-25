using System;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class BangerElitePatches
{
    private static readonly Action<EnemyBang, EnemyBang.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyBang, EnemyBang.State>>(
            AccessTools.Method(typeof(EnemyBang), "UpdateState"));

    internal static void OnSpawn(EliteState state)
    {
        state.AdapterState = new BangerRuntimeState();
    }

    internal static void Tick(EliteState state)
    {
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyBang), "FuseLogic")]
    private static bool FuseLogicPrefix(EnemyBang __instance)
    {
        if (!EliteRegistry.TryGet(__instance, out EliteState state))
        {
            return true;
        }
        if (!SemiFunc.IsMasterClientOrSingleplayer())
        {
            return true;
        }
        if (!GameFieldAccess.BangFuseActive(__instance))
        {
            GetRuntime(state).PreviousFuseActive = false;
            return false;
        }

        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        BangerRuntimeState runtime = GetRuntime(state);
        if (!runtime.PreviousFuseActive)
        {
            runtime.PreviousFuseActive = true;
            runtime.FuseStartedAt = Time.time;
            runtime.LastSyncAt = float.NegativeInfinity;
        }

        if (!EnemyBangDirector.instance.debugNoFuseProgress)
        {
            float duration = Mathf.Max(0.5f, config.BangerFuseSeconds.Value);
            if (HasNearVisibleTarget(state, config.BangerNearTargetDistance.Value))
            {
                duration = Mathf.Min(duration, Mathf.Max(0.5f, config.BangerNearTargetFuseSeconds.Value));
            }
            GameFieldAccess.SetBangFuseLerp(
                __instance,
                GameFieldAccess.BangFuseLerp(__instance) + Time.deltaTime / duration);
        }
        GameFieldAccess.SetBangFuseLerp(
            __instance,
            Mathf.Clamp01(GameFieldAccess.BangFuseLerp(__instance)));

        if (GameManager.Multiplayer() &&
            Time.time >= runtime.LastSyncAt + config.BangerFuseSyncIntervalSeconds.Value)
        {
            runtime.LastSyncAt = Time.time;
            GameFieldAccess.BangPhotonView(__instance).RPC(
                "FuseRPC", RpcTarget.Others, true, GameFieldAccess.BangFuseLerp(__instance));
        }

        if (GameFieldAccess.BangFuseLerp(__instance) < 1f)
        {
            return false;
        }

        if (GameManager.Multiplayer())
        {
            GameFieldAccess.BangPhotonView(__instance).RPC("ExplodeRPC", RpcTarget.All);
        }
        else
        {
            AccessTools.Method(typeof(EnemyBang), "ExplodeRPC").Invoke(
                __instance,
                new object[] { default(PhotonMessageInfo) });
        }
        UpdateState(__instance, EnemyBang.State.Despawn);
        GameFieldAccess.Parent(GameFieldAccess.BangEnemy(__instance)).Despawn();
        runtime.PreviousFuseActive = false;
        return false;
    }

    private static bool HasNearVisibleTarget(EliteState state, float maxDistance)
    {
        PlayerAvatar? target = GameFieldAccess.TargetPlayer(state.Enemy);
        if (target == null || GameFieldAccess.PlayerDisabled(target))
        {
            float best = float.MaxValue;
            foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
            {
                if (!DedicatedBehaviorSupport.CanUseAsUnseenTarget(player))
                {
                    continue;
                }
                float distance = Vector3.Distance(state.Enemy.CenterTransform.position, player.transform.position);
                if (distance < best)
                {
                    best = distance;
                    target = player;
                }
            }
        }
        if (target == null)
        {
            return false;
        }
        Vector3 point = target.PlayerVisionTarget?.VisionTransform?.position ?? target.transform.position;
        Vector3 direction = point - state.Enemy.CenterTransform.position;
        if (direction.magnitude > maxDistance)
        {
            return false;
        }
        return !Physics.Raycast(state.Enemy.CenterTransform.position, direction.normalized, direction.magnitude,
            GameFieldAccess.VisionMask(state.Enemy), QueryTriggerInteraction.Ignore);
    }

    private static BangerRuntimeState GetRuntime(EliteState state)
    {
        if (state.AdapterState is not BangerRuntimeState runtime)
        {
            runtime = new BangerRuntimeState();
            state.AdapterState = runtime;
        }
        return runtime;
    }
}
