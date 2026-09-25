using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class ShadowChildElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyThinMan, Enemy> EnemyRef =
        AccessTools.FieldRefAccess<EnemyThinMan, Enemy>("enemy");
    private static readonly AccessTools.FieldRef<EnemyThinMan, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyThinMan, PlayerAvatar>("playerTarget");
    private static readonly AccessTools.FieldRef<EnemyThinMan, Vector3> TeleportPosition =
        AccessTools.FieldRefAccess<EnemyThinMan, Vector3>("teleportPosition");
    private static readonly Action<EnemyThinMan, EnemyThinMan.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyThinMan, EnemyThinMan.State>>(
            AccessTools.Method(typeof(EnemyThinMan), "UpdateState"));
    private static readonly Action<EnemyThinMan, PlayerAvatar> SetTarget =
        AccessTools.MethodDelegate<Action<EnemyThinMan, PlayerAvatar>>(
            AccessTools.Method(typeof(EnemyThinMan), "SetTarget"));

    internal static void OnSpawn(EliteState state)
    {
        state.AdapterState = new ShadowChildRuntimeState
        {
            LastPosition = state.Enemy.transform.position
        };
    }

    internal static void Tick(EliteState state)
    {
        EnemyThinMan? shadow = state.Enemy.GetComponent<EnemyThinMan>();
        if (shadow == null)
        {
            return;
        }
        ShadowChildRuntimeState runtime = GetRuntime(state);
        if (state.Enemy.IsStunned() || shadow.currentState == EnemyThinMan.State.Stunned)
        {
            ResetRuntime(runtime, shadow.transform.position);
            return;
        }

        PlayerAvatar? target = PlayerTarget(shadow);
        if (target != null && target.photonView != null && target.photonView.ViewID != runtime.LastTargetViewId)
        {
            runtime.PreviousTargetViewId = runtime.LastTargetViewId;
            runtime.LastTargetViewId = target.photonView.ViewID;
        }

        EnemyOnScreen onScreen = GameFieldAccess.OnScreen(state.Enemy);
        bool watchedByAnyone = onScreen != null && GameFieldAccess.OnScreenAny(onScreen);
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (player != null && !GameFieldAccess.PlayerDisabled(player) &&
                GameFieldAccess.OnScreen(state.Enemy).GetOnScreen(player))
            {
                watchedByAnyone = true;
                break;
            }
        }

        if (watchedByAnyone && !DedicatedBehaviorSupport.IsValidTarget(target) &&
            DedicatedBehaviorSupport.TryFindNearestPlayer(
                shadow.transform.position,
                EliteEnemyVariantsPlugin.Instance.Settings.ShadowApproachRadius.Value,
                out PlayerAvatar visibleTarget,
                null,
                state.Enemy))
        {
            SetTarget(shadow, visibleTarget);
            target = visibleTarget;
        }

        if (watchedByAnyone && DedicatedBehaviorSupport.IsValidTarget(target) &&
            shadow.currentState is EnemyThinMan.State.Stand or EnemyThinMan.State.OnScreen)
        {
            runtime.LookAccumulation += Time.deltaTime;
            float required = EliteEnemyVariantsPlugin.Instance.Settings.ShadowEyeContactSeconds.Value;
            if (runtime.LookAccumulation >= required)
            {
                UpdateState(shadow, EnemyThinMan.State.Notice);
                runtime.LookAccumulation = 0f;
            }
        }
        else
        {
            runtime.LookAccumulation = Mathf.Max(0f, runtime.LookAccumulation - Time.deltaTime * 0.5f);
        }

        if (!watchedByAnyone && shadow.currentState is EnemyThinMan.State.Stand or EnemyThinMan.State.OnScreen &&
            TryChooseApproachTarget(state, runtime, out PlayerAvatar approachTarget))
        {
            if (target != approachTarget)
            {
                SetTarget(shadow, approachTarget);
                target = approachTarget;
            }
            float speed = EliteEnemyVariantsPlugin.Instance.Settings.ShadowApproachSpeed.Value;
            Vector3 current = TeleportPosition(shadow);
            Vector3 desired = Vector3.MoveTowards(current, approachTarget.transform.position, speed * Time.deltaTime);
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                TeleportPosition(shadow) = hit.position;
            }
        }

        float moved = Vector3.Distance(runtime.LastPosition, shadow.transform.position);
        runtime.StuckTimer = moved < 0.05f ? runtime.StuckTimer + Time.deltaTime : 0f;
        runtime.LastPosition = shadow.transform.position;
        if (runtime.StuckTimer >= 2f && target != null)
        {
            Vector3 lateral = Vector3.Cross(Vector3.up, target.transform.position - shadow.transform.position).normalized;
            Vector3 retry = target.transform.position + lateral * 2f;
            if (NavMesh.SamplePosition(retry, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                TeleportPosition(shadow) = hit.position;
            }
            runtime.StuckTimer = 0f;
        }
        runtime.PreviousState = (int)shadow.currentState;
    }

    private static bool TryChooseApproachTarget(
        EliteState state,
        ShadowChildRuntimeState runtime,
        out PlayerAvatar result)
    {
        result = null!;
        float radius = EliteEnemyVariantsPlugin.Instance.Settings.ShadowApproachRadius.Value;
        PlayerAvatar current = PlayerTarget(state.Enemy.GetComponent<EnemyThinMan>());
        if (DedicatedBehaviorSupport.IsValidTarget(current) &&
            Vector3.Distance(state.Enemy.transform.position, current.transform.position) <= radius)
        {
            result = current;
            return true;
        }
        float bestDistance = float.MaxValue;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!DedicatedBehaviorSupport.CanUseAsUnseenTarget(player))
            {
                continue;
            }
            float distance = Vector3.Distance(state.Enemy.transform.position, player.transform.position);
            if (distance <= radius && distance < bestDistance)
            {
                bestDistance = distance;
                result = player;
            }
        }
        if (result != null)
        {
            return true;
        }

        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!DedicatedBehaviorSupport.CanUseAsUnseenTarget(player))
            {
                continue;
            }
            float distance = Vector3.Distance(state.Enemy.transform.position, player.transform.position);
            if (distance <= radius && distance < bestDistance)
            {
                bestDistance = distance;
                result = player;
            }
        }
        return result != null;
    }

    private static ShadowChildRuntimeState GetRuntime(EliteState state)
    {
        if (state.AdapterState is not ShadowChildRuntimeState runtime)
        {
            runtime = new ShadowChildRuntimeState { LastPosition = state.Enemy.transform.position };
            state.AdapterState = runtime;
        }
        return runtime;
    }

    private static void ResetRuntime(ShadowChildRuntimeState runtime, Vector3 position)
    {
        runtime.LookAccumulation = 0f;
        runtime.StuckTimer = 0f;
        runtime.LastTargetViewId = 0;
        runtime.PreviousTargetViewId = 0;
        runtime.LastPosition = position;
    }
}
