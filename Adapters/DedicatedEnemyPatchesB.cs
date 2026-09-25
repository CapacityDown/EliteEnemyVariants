using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class OoglyElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyOogly, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyOogly, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyOogly, float> AttackCooldown =
        AccessTools.FieldRefAccess<EnemyOogly, float>("attackCooldownTimer");
    private static readonly AccessTools.FieldRef<EnemyOogly, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyOogly, PlayerAvatar>("targetPlayer");
    private static readonly Action<EnemyOogly, EnemyOogly.State> StateSet =
        AccessTools.MethodDelegate<Action<EnemyOogly, EnemyOogly.State>>(
            AccessTools.Method(typeof(EnemyOogly), "StateSet"));
    private static readonly Action<EnemyOogly> InitializeFlightSystem =
        AccessTools.MethodDelegate<Action<EnemyOogly>>(
            AccessTools.Method(typeof(EnemyOogly), "InitializeFlightSystem"));
    private static readonly AccessTools.FieldRef<EnemyOogly, Vector3> RoamDestination =
        AccessTools.FieldRefAccess<EnemyOogly, Vector3>("roamDestination");
    private static readonly AccessTools.FieldRef<EnemyOogly, Vector3> CurrentRoamTarget =
        AccessTools.FieldRefAccess<EnemyOogly, Vector3>("currentRoamTarget");
    private static readonly AccessTools.FieldRef<EnemyOogly, Quaternion> HeadWorldTargetRotation =
        AccessTools.FieldRefAccess<EnemyOogly, Quaternion>("headWorldTargetRotation");

    internal static void OnSpawn(EliteState state)
    {
        DedicatedBehaviorSupport.OnSpawn(state);
        EnemyOogly? enemy = state.Enemy.GetComponent<EnemyOogly>();
        if (enemy == null)
        {
            return;
        }
        enemy.minFlightHeight = 0.75f;
        enemy.preferredHeight = 2f;
        enemy.maxFlightHeight = 8f;
        InitializeFlightSystem(enemy);
        DedicatedBehaviorSupport.Runtime(state).NextActionAt =
            Time.time + EliteEnemyVariantsPlugin.Instance.Settings.OoglyLowPatrolIntervalSeconds.Value;
    }

    private static void Tick(EliteState state, EnemyOogly enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        DedicatedBehaviorSupport.Countdown(enemy, AttackCooldown, DedicatedBehaviorSupport.CooldownRate(state));
        if (enemy.currentState == EnemyOogly.State.PlayerSpotted)
        {
            DedicatedBehaviorSupport.Countup(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        if (runtime.PreviousState == (int)EnemyOogly.State.Dive &&
            enemy.currentState == EnemyOogly.State.FindCeiling && !runtime.ExtraActionUsed &&
            DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(enemy)))
        {
            runtime.ExtraActionUsed = true;
            StateSet(enemy, EnemyOogly.State.PlayerSpotted);
        }
        if (enemy.currentState == EnemyOogly.State.PlayerSpotted &&
            runtime.PreviousState is not (int)EnemyOogly.State.Dive and not (int)EnemyOogly.State.FindCeiling)
        {
            runtime.ExtraActionUsed = false;
        }
        TickLowPatrol(state, enemy, runtime);
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    private static void TickLowPatrol(
        EliteState state,
        EnemyOogly enemy,
        DedicatedEnemyRuntimeState runtime)
    {
        if (enemy.currentState != EnemyOogly.State.CeilingRoam)
        {
            runtime.AlternateMode = false;
            runtime.StuckTimer = 0f;
            return;
        }
        if (runtime.AlternateMode)
        {
            runtime.StuckTimer += Time.deltaTime;
            if (Vector3.Distance(enemy.rb.position, CurrentRoamTarget(enemy)) <= 1.25f || runtime.StuckTimer >= 8f)
            {
                runtime.AlternateMode = false;
                runtime.StuckTimer = 0f;
                runtime.NextActionAt = Time.time +
                    EliteEnemyVariantsPlugin.Instance.Settings.OoglyLowPatrolIntervalSeconds.Value;
            }
            else
            {
                RoamDestination(enemy) = CurrentRoamTarget(enemy);
            }
            return;
        }
        if (Time.time < runtime.NextActionAt || !TryChooseLowPatrolPoint(enemy, out Vector3 point))
        {
            return;
        }
        CurrentRoamTarget(enemy) = point;
        RoamDestination(enemy) = point;
        runtime.AlternateMode = true;
        runtime.StuckTimer = 0f;
    }

    private static bool TryChooseLowPatrolPoint(EnemyOogly enemy, out Vector3 result)
    {
        result = default;
        PlayerAvatar? nearest = null;
        float bestPlayerDistance = 30f;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (!DedicatedBehaviorSupport.IsValidTarget(player))
            {
                continue;
            }
            float distance = Vector3.Distance(enemy.rb.position, player.transform.position);
            if (distance < bestPlayerDistance)
            {
                bestPlayerDistance = distance;
                nearest = player;
            }
        }
        Vector3 origin = nearest != null ? nearest.transform.position : enemy.rb.position;
        List<LevelPoint> points = SemiFunc.LevelPointGetWithinDistance(origin, 2f, 16f);
        if (points == null)
        {
            return false;
        }
        float height = EliteEnemyVariantsPlugin.Instance.Settings.OoglyLowPatrolHeight.Value;
        foreach (LevelPoint point in points)
        {
            if (point == null || !Physics.Raycast(
                    point.transform.position + Vector3.up * 3f,
                    Vector3.down,
                    out RaycastHit hit,
                    6f,
                    LayerMask.GetMask("Default"),
                    QueryTriggerInteraction.Ignore))
            {
                continue;
            }
            Vector3 candidate = hit.point + Vector3.up * height;
            if (!Physics.CheckSphere(candidate, 0.4f, enemy.environmentMask, QueryTriggerInteraction.Ignore))
            {
                result = candidate;
                return true;
            }
        }
        return false;
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyOogly), "Update")]
    private static void UpdatePostfix(EnemyOogly __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Oogly, Tick);

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyOogly), "SetHeadTargetRotation")]
    private static void SetHeadTargetRotationPostfix(EnemyOogly __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteRegistry.TryGet(__instance, out EliteState state) ||
            __instance.currentState != EnemyOogly.State.CeilingRoam ||
            !DedicatedBehaviorSupport.Runtime(state).AlternateMode ||
            __instance.headTransform == null)
        {
            return;
        }
        Vector3 direction = CurrentRoamTarget(__instance) - __instance.headTransform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = __instance.transform.forward;
            direction.y = 0f;
        }
        if (direction.sqrMagnitude > 0.01f)
        {
            HeadWorldTargetRotation(__instance) = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}

[HarmonyPatch]
internal static class RobeElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyRobe, float> ChaseTime =
        AccessTools.FieldRefAccess<EnemyRobe, float>("chaseTime");
    private static readonly AccessTools.FieldRef<EnemyRobe, float> IdleBreakTimer =
        AccessTools.FieldRefAccess<EnemyRobe, float>("idleBreakTimer");
    private static readonly AccessTools.FieldRef<EnemyRobe, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyRobe, PlayerAvatar>("targetPlayer");
    private static readonly AccessTools.FieldRef<EnemyRobe, bool> IsOnScreen =
        AccessTools.FieldRefAccess<EnemyRobe, bool>("isOnScreen");
    private static readonly Action<EnemyRobe, EnemyRobe.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyRobe, EnemyRobe.State>>(
            AccessTools.Method(typeof(EnemyRobe), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyRobe enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        DedicatedBehaviorSupport.Countdown(enemy, IdleBreakTimer, DedicatedBehaviorSupport.CooldownRate(state));
        if (enemy.currentState == EnemyRobe.State.TargetPlayer)
        {
            DedicatedBehaviorSupport.RetainCountup(enemy, ChaseTime, DedicatedBehaviorSupport.Retention(state));
        }
        PlayerAvatar target = PlayerTarget(enemy);
        if (runtime.PreviousState == (int)EnemyRobe.State.TargetPlayer &&
            enemy.currentState is EnemyRobe.State.SeekPlayer or EnemyRobe.State.Leave &&
            !runtime.ExtraActionUsed && DedicatedBehaviorSupport.IsValidTarget(target) &&
            GameFieldAccess.OnScreen(state.Enemy).GetOnScreen(target))
        {
            runtime.ExtraActionUsed = true;
            UpdateState(enemy, EnemyRobe.State.TargetPlayer);
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyRobe), "Update")]
    private static void UpdatePostfix(EnemyRobe __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Robe, Tick);

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyRobe), "MoveTowardPlayer")]
    private static void MoveTowardPlayerPostfix(EnemyRobe __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || IsOnScreen(__instance) ||
            !EliteRegistry.TryGet(__instance, out EliteState state))
        {
            return;
        }
        float speed = EliteEnemyVariantsPlugin.Instance.Settings.RobeUnseenMoveSpeed.Value;
        EnemyNavMeshAgent nav = GameFieldAccess.NavMeshAgent(state.Enemy);
        nav.OverrideAgent(speed, GameFieldAccess.NavDefaultAcceleration(nav), 0.2f);
        GameFieldAccess.Rigidbody(state.Enemy).OverrideFollowPosition(1f, speed);
    }
}

[HarmonyPatch]
internal static class ReaperElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyRunner, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyRunner, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyRunner, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyRunner, PlayerAvatar>("targetPlayer");
    private static readonly AccessTools.FieldRef<EnemyRunner, bool> StateImpulse =
        AccessTools.FieldRefAccess<EnemyRunner, bool>("stateImpulse");
    private static readonly AccessTools.FieldRef<EnemyRunner, Vector3> TargetPosition =
        AccessTools.FieldRefAccess<EnemyRunner, Vector3>("targetPosition");
    private static readonly AccessTools.FieldRef<EnemyRunner, float> AgentSpeedCurrent =
        AccessTools.FieldRefAccess<EnemyRunner, float>("agentSpeedCurrent");
    private static readonly Action<EnemyRunner, EnemyRunner.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyRunner, EnemyRunner.State>>(
            AccessTools.Method(typeof(EnemyRunner), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyRunner enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (runtime.AlternateMode && enemy.currentState != EnemyRunner.State.AttackPlayer)
        {
            runtime.AlternateMode = false;
            runtime.Tactics.Sweeping = false;
        }
        if (enemy.currentState is EnemyRunner.State.Notice or EnemyRunner.State.StuckAttackNotice)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        else if (enemy.currentState == EnemyRunner.State.AttackPlayer && !runtime.AlternateMode)
        {
            DedicatedBehaviorSupport.RetainCountdown(enemy, StateTimer, DedicatedBehaviorSupport.Retention(state));
        }
        if (runtime.PreviousState == (int)EnemyRunner.State.AttackPlayer &&
            enemy.currentState == EnemyRunner.State.SeekPlayer && !runtime.ExtraActionUsed &&
            runtime.LastTargetViewId != 0)
        {
            runtime.ExtraActionUsed = true;
            runtime.AlternateMode = true;
            runtime.Tactics.SweepUsed = false;
            runtime.Tactics.Sweeping = false;
            runtime.NextActionAt = Time.time + 4f;
            UpdateState(enemy, EnemyRunner.State.AttackPlayer);
        }
        if (enemy.currentState == EnemyRunner.State.Notice &&
            runtime.PreviousState is (int)EnemyRunner.State.Idle or
                (int)EnemyRunner.State.Roam or (int)EnemyRunner.State.Investigate or
                (int)EnemyRunner.State.SeekPlayer or (int)EnemyRunner.State.Sneak)
        {
            runtime.ExtraActionUsed = false;
            runtime.AlternateMode = false;
            runtime.Tactics.Sweeping = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyRunner), nameof(EnemyRunner.OnVision))]
    private static void OnVisionPostfix(EnemyRunner __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteRegistry.TryGet(__instance, out EliteState state))
        {
            return;
        }
        PlayerAvatar target = PlayerTarget(__instance);
        if (!DedicatedBehaviorSupport.IsValidTarget(target))
        {
            return;
        }
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        runtime.LastPosition = target.transform.position;
        runtime.LastTargetViewId = target.photonView.ViewID;
        runtime.Tactics.Sweeping = false;
        if (runtime.AlternateMode)
        {
            runtime.AlternateMode = false;
            runtime.ExtraActionUsed = false;
            StateTimer(__instance) = Mathf.Max(StateTimer(__instance), 2f);
        }
    }

    [HarmonyPrefix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyRunner), nameof(EnemyRunner.StateAttackPlayer))]
    private static bool StateAttackPlayerPrefix(EnemyRunner __instance)
    {
        if (!TacticalPositioning.IsActive(__instance, out EliteState state))
        {
            return true;
        }
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (!runtime.AlternateMode)
        {
            return true;
        }
        if (StateImpulse(__instance))
        {
            StateImpulse(__instance) = false;
            StateTimer(__instance) = 4f;
            AgentSpeedCurrent(__instance) = 3f;
            GameFieldAccess.NavMeshAgent(state.Enemy).Warp(__instance.feetTransform.position);
            GameFieldAccess.NavMeshAgent(state.Enemy).ResetPath();
        }

        TacticalRuntimeState tactics = runtime.Tactics;
        Vector3 lastSeen = tactics.Sweeping ? tactics.SweepPoint : runtime.LastPosition;
        TargetPosition(__instance) = lastSeen;
        EnemyNavMeshAgent nav = GameFieldAccess.NavMeshAgent(state.Enemy);
        nav.SetDestination(lastSeen);
        nav.OverrideAgent(4f, Mathf.Max(10f, GameFieldAccess.NavDefaultAcceleration(nav)), 0.2f);
        StateTimer(__instance) -= Time.deltaTime;
        bool searched = Vector3.Distance(__instance.feetTransform.position, lastSeen) <= 1.25f;
        bool failed = StateTimer(__instance) <= 0f ||
                      GameFieldAccess.NotMovingTimer(GameFieldAccess.Rigidbody(state.Enemy)) > 2f;
        if (searched && !failed && !tactics.SweepUsed)
        {
            tactics.SweepUsed = true;
            if (TacticalPositioning.SweepCorner(state, runtime.LastPosition, out Vector3 corner))
            {
                tactics.Sweeping = true;
                tactics.SweepPoint = corner;
                StateTimer(__instance) = 2f;
                TargetPosition(__instance) = corner;
                nav.SetDestination(corner);
                return false;
            }
        }
        if (searched || failed)
        {
            runtime.AlternateMode = false;
            tactics.Sweeping = false;
            UpdateState(__instance, EnemyRunner.State.SeekPlayer);
        }
        return false;
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyRunner), "RotationLogic")]
    private static void RotationLogicPostfix(EnemyRunner __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteRegistry.TryGet(__instance, out EliteState state) ||
            !DedicatedBehaviorSupport.Runtime(state).AlternateMode)
        {
            return;
        }
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        Vector3 searchPoint = runtime.Tactics.Sweeping ? runtime.Tactics.SweepPoint : runtime.LastPosition;
        Vector3 direction = searchPoint - __instance.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            __instance.transform.rotation = Quaternion.RotateTowards(
                __instance.transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                720f * Time.deltaTime);
        }
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyRunner), "Update")]
    private static void UpdatePostfix(EnemyRunner __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Reaper, Tick);
}

[HarmonyPatch]
internal static class LoomElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyShadow, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyShadow, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyShadow, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyShadow, PlayerAvatar>("playerTarget");
    private static readonly Action<EnemyShadow, PlayerAvatar> UpdatePlayerTarget =
        AccessTools.MethodDelegate<Action<EnemyShadow, PlayerAvatar>>(
            AccessTools.Method(typeof(EnemyShadow), "UpdatePlayerTarget"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyShadow enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState == EnemyShadow.State.CoolDown &&
            DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(enemy)))
        {
            runtime.LastTargetViewId = PlayerTarget(enemy).photonView.ViewID;
            runtime.ExtraActionUsed = false;
        }
        if (enemy.currentState == EnemyShadow.State.ChooseTarget)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        else if (enemy.currentState == EnemyShadow.State.CoolDown)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.CooldownRate(state));
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyShadow), "StateChooseTarget")]
    private static void StateChooseTargetPostfix(EnemyShadow __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteRegistry.TryGet(__instance, out EliteState state))
        {
            return;
        }
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        PlayerAvatar current = PlayerTarget(__instance);
        if (runtime.ExtraActionUsed || !DedicatedBehaviorSupport.IsValidTarget(current))
        {
            return;
        }
        if (current.photonView.ViewID == runtime.LastTargetViewId &&
            DedicatedBehaviorSupport.TryFindNearestPlayer(
                __instance.transform.position,
                999f,
                out PlayerAvatar next,
                current))
        {
            UpdatePlayerTarget(__instance, next);
            current = next;
        }
        runtime.ExtraActionUsed = true;
        if (current.photonView.ViewID != runtime.LastTargetViewId &&
            Vector3.Distance(__instance.transform.position, current.transform.position) > 30f)
        {
            TeleportNearTarget(state, current);
        }
    }

    private static void TeleportNearTarget(EliteState state, PlayerAvatar target)
    {
        if (TacticalPositioning.LoomPosition(state, target, out Vector3 point))
            state.Enemy.EnemyTeleported(point);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyShadow), "Update")]
    private static void UpdatePostfix(EnemyShadow __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Loom, Tick);
}

[HarmonyPatch]
internal static class SpewerElitePatches
{
    private static readonly AccessTools.FieldRef<EnemySlowMouth, float> StateTimer =
        AccessTools.FieldRefAccess<EnemySlowMouth, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemySlowMouth, float> IdlePukeCooldown =
        AccessTools.FieldRefAccess<EnemySlowMouth, float>("idlePukeCooldown");
    private static readonly AccessTools.FieldRef<EnemySlowMouth, float> PossessCooldown =
        AccessTools.FieldRefAccess<EnemySlowMouth, float>("possessCooldown");
    private static readonly AccessTools.FieldRef<EnemySlowMouth, float> AttachedTimer =
        AccessTools.FieldRefAccess<EnemySlowMouth, float>("attachedTimer");
    private static readonly Action<EnemySlowMouth, PlayerAvatar> UpdatePlayerTarget =
        AccessTools.MethodDelegate<Action<EnemySlowMouth, PlayerAvatar>>(
            AccessTools.Method(typeof(EnemySlowMouth), "UpdatePlayerTarget"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemySlowMouth enemy)
    {
        if (enemy.currentState == EnemySlowMouth.State.Notice)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        float rate = DedicatedBehaviorSupport.CooldownRate(state);
        DedicatedBehaviorSupport.Countdown(enemy, IdlePukeCooldown, rate);
        DedicatedBehaviorSupport.Countdown(enemy, PossessCooldown, rate);
        if (enemy.currentState == EnemySlowMouth.State.Attached)
        {
            AttachedTimer(enemy) = Mathf.Min(AttachedTimer(enemy), 75f);
            enemy.UpdateState(EnemySlowMouth.State.Puke);
        }
        else if (enemy.currentState == EnemySlowMouth.State.Puke)
        {
            AttachedTimer(enemy) -= Time.deltaTime;
            if (AttachedTimer(enemy) <= 0f)
            {
                enemy.UpdateState(EnemySlowMouth.State.Detach);
            }
        }
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.OnVision))]
    private static void OnVisionPostfix(EnemySlowMouth __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteRegistry.TryGet(__instance, out _))
        {
            return;
        }
        Enemy enemy = __instance.GetComponent<Enemy>();
        PlayerAvatar target = GameFieldAccess.Vision(enemy).onVisionTriggeredPlayer;
        if (!DedicatedBehaviorSupport.IsValidTarget(target))
        {
            return;
        }
        if (target.playerAvatarVisuals.GetComponentInChildren<EnemySlowMouthAttached>() != null)
        {
            return;
        }
        UpdatePlayerTarget(__instance, target);
        PossessCooldown(__instance) = 0f;
        __instance.UpdateState(EnemySlowMouth.State.Attack);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemySlowMouth), "Update")]
    private static void UpdatePostfix(EnemySlowMouth __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Spewer, Tick);
}

[HarmonyPatch]
internal static class TrudgeElitePatches
{
    private static readonly AccessTools.FieldRef<EnemySlowWalker, float> StateTimer =
        AccessTools.FieldRefAccess<EnemySlowWalker, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemySlowWalker, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemySlowWalker, PlayerAvatar>("targetPlayer");
    private static readonly Action<EnemySlowWalker, EnemySlowWalker.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemySlowWalker, EnemySlowWalker.State>>(
            AccessTools.Method(typeof(EnemySlowWalker), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemySlowWalker enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState == EnemySlowWalker.State.Notice)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        if (runtime.PreviousState == (int)EnemySlowWalker.State.Attack &&
            enemy.currentState == EnemySlowWalker.State.Idle && !runtime.ExtraActionUsed &&
            DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(enemy)))
        {
            runtime.ExtraActionUsed = true;
            UpdateState(enemy, EnemySlowWalker.State.GoToPlayer);
        }
        if (enemy.currentState == EnemySlowWalker.State.Notice &&
            runtime.PreviousState is (int)EnemySlowWalker.State.Idle or
                (int)EnemySlowWalker.State.Roam or (int)EnemySlowWalker.State.Investigate)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemySlowWalker), "Update")]
    private static void UpdatePostfix(EnemySlowWalker __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Trudge, Tick);
}

[HarmonyPatch]
internal static class TickElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyTick, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyTick, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyTick, float> SuckTimer =
        AccessTools.FieldRefAccess<EnemyTick, float>("suckTimer");

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void TickBehavior(EliteState state, EnemyTick enemy)
    {
        TacticalRuntimeState tactics = DedicatedBehaviorSupport.Runtime(state).Tactics;
        if (!enemy.anim.hasBeenFull) tactics.RescueEvaded = false;
        else if (enemy.currentState == EnemyTick.State.RunAway)
        {
            EnemyNavMeshAgent nav = GameFieldAccess.NavMeshAgent(state.Enemy);
            if (nav.HasPath() && TacticalPositioning.EvadePursuer(state, nav.GetDestination(), tactics, out Vector3 escape))
                nav.SetDestination(escape);
        }
        if (enemy.currentState == EnemyTick.State.Notice)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        else if (enemy.currentState == EnemyTick.State.Bite)
        {
            DedicatedBehaviorSupport.Countdown(enemy, SuckTimer, DedicatedBehaviorSupport.CooldownRate(state));
        }
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyTick), "Update")]
    private static void UpdatePostfix(EnemyTick __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Tick, TickBehavior);
}

[HarmonyPatch]
internal static class BellaElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyTricycle, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyTricycle, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyTricycle, float> AttackCooldown =
        AccessTools.FieldRefAccess<EnemyTricycle, float>("attackCooldown");
    private static readonly Action<EnemyTricycle, EnemyTricycle.State> SetState =
        AccessTools.MethodDelegate<Action<EnemyTricycle, EnemyTricycle.State>>(
            AccessTools.Method(typeof(EnemyTricycle), "SetState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyTricycle enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        DedicatedBehaviorSupport.Countdown(enemy, AttackCooldown, DedicatedBehaviorSupport.CooldownRate(state));
        if (enemy.currentState is EnemyTricycle.State.StateBeforeAttack or EnemyTricycle.State.AttackTell or EnemyTricycle.State.AttackTellShort)
        {
            DedicatedBehaviorSupport.Countup(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        if (runtime.PreviousState == (int)EnemyTricycle.State.AttackOutro &&
            enemy.currentState == EnemyTricycle.State.BackToNavmesh && !runtime.ExtraActionUsed)
        {
            runtime.ExtraActionUsed = true;
            SetState(enemy, EnemyTricycle.State.StateBeforeAttack);
        }
        if (enemy.currentState == EnemyTricycle.State.BellRing &&
            runtime.PreviousState != (int)EnemyTricycle.State.BellRing)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyTricycle), "Update")]
    private static void UpdatePostfix(EnemyTricycle __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Bella, Tick);
}

[HarmonyPatch]
internal static class ChefElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyTumbler, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyTumbler, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyTumbler, float> RoamWaitTimer =
        AccessTools.FieldRefAccess<EnemyTumbler, float>("roamWaitTimer");
    private static readonly AccessTools.FieldRef<EnemyTumbler, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyTumbler, PlayerAvatar>("targetPlayer");
    private static readonly Action<EnemyTumbler, EnemyTumbler.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyTumbler, EnemyTumbler.State>>(
            AccessTools.Method(typeof(EnemyTumbler), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyTumbler enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState is EnemyTumbler.State.Notice or EnemyTumbler.State.Tell)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        DedicatedBehaviorSupport.Countdown(enemy, RoamWaitTimer, DedicatedBehaviorSupport.CooldownRate(state));
        if (enemy.currentState == EnemyTumbler.State.Tumble &&
            DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(enemy)))
        {
            if (runtime.PreviousState != (int)EnemyTumbler.State.Tumble)
            {
                runtime.NextActionAt = Time.time + 6f;
            }
            Rigidbody rb = GameFieldAccess.Rigidbody(state.Enemy).rb;
            Vector3 direction = PlayerTarget(enemy).transform.position - rb.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                direction.Normalize();
                Vector3 horizontalVelocity = rb.velocity;
                horizontalVelocity.y = 0f;
                Vector3 desiredVelocity = direction * 18f;
                Vector3 velocityChange = Vector3.ClampMagnitude(
                    desiredVelocity - horizontalVelocity,
                    72f * Time.deltaTime);
                rb.AddForce(velocityChange, ForceMode.VelocityChange);
                rb.AddTorque(Vector3.Cross(Vector3.up, direction) * 25f * Time.deltaTime,
                    ForceMode.VelocityChange);
            }
            if (Time.time >= runtime.NextActionAt)
            {
                UpdateState(enemy, EnemyTumbler.State.TumbleEnd);
            }
        }
        if (runtime.PreviousState == (int)EnemyTumbler.State.TumbleEnd &&
            enemy.currentState == EnemyTumbler.State.BackToNavmesh && !runtime.ExtraActionUsed &&
            DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(enemy)))
        {
            runtime.ExtraActionUsed = true;
            UpdateState(enemy, EnemyTumbler.State.Tell);
        }
        if (enemy.currentState == EnemyTumbler.State.Notice &&
            runtime.PreviousState != (int)EnemyTumbler.State.Notice)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyTumbler), "Update")]
    private static void UpdatePostfix(EnemyTumbler __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Chef, Tick);
}

[HarmonyPatch]
internal static class UpscreamElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyUpscream, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyUpscream, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyUpscream, float> RoamWaitTimer =
        AccessTools.FieldRefAccess<EnemyUpscream, float>("roamWaitTimer");
    private static readonly AccessTools.FieldRef<EnemyUpscream, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyUpscream, PlayerAvatar>("targetPlayer");
    private static readonly Action<EnemyUpscream, EnemyUpscream.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyUpscream, EnemyUpscream.State>>(
            AccessTools.Method(typeof(EnemyUpscream), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyUpscream enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState == EnemyUpscream.State.PlayerNotice)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        DedicatedBehaviorSupport.Countdown(enemy, RoamWaitTimer, DedicatedBehaviorSupport.CooldownRate(state));
        if (runtime.PreviousState == (int)EnemyUpscream.State.Attack &&
            enemy.currentState is EnemyUpscream.State.Idle or EnemyUpscream.State.Leave &&
            !runtime.ExtraActionUsed && DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(enemy)))
        {
            runtime.ExtraActionUsed = true;
            runtime.AlternateMode = true;
            runtime.NextActionAt = Time.time + 8f;
            UpdateState(enemy, EnemyUpscream.State.GoToPlayer);
        }
        if (runtime.AlternateMode && enemy.currentState == EnemyUpscream.State.GoToPlayer)
        {
            float remaining = runtime.NextActionAt - Time.time;
            if (remaining <= 0f || !DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(enemy)))
            {
                runtime.AlternateMode = false;
                UpdateState(enemy, EnemyUpscream.State.Leave);
            }
            else
            {
                StateTimer(enemy) = Mathf.Max(StateTimer(enemy), remaining);
                EnemyNavMeshAgent nav = GameFieldAccess.NavMeshAgent(state.Enemy);
                nav.OverrideAgent(7f, Mathf.Max(15f, GameFieldAccess.NavDefaultAcceleration(nav)), 0.2f);
            }
        }
        else if (runtime.AlternateMode && enemy.currentState == EnemyUpscream.State.Attack)
        {
            // This is the single follow-up blow. Keep ExtraActionUsed set so the
            // same engagement cannot schedule a third chase.
            runtime.AlternateMode = false;
        }
        if (enemy.currentState == EnemyUpscream.State.PlayerNotice &&
            runtime.PreviousState != (int)EnemyUpscream.State.PlayerNotice)
        {
            runtime.ExtraActionUsed = false;
            runtime.AlternateMode = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyUpscream), "Update")]
    private static void UpdatePostfix(EnemyUpscream __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Upscream, Tick);
}

[HarmonyPatch]
internal static class RugratElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyValuableThrower, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyValuableThrower, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyValuableThrower, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyValuableThrower, PlayerAvatar>("playerTarget");
    private static readonly AccessTools.FieldRef<EnemyValuableThrower, PhysGrabObject> ValuableTarget =
        AccessTools.FieldRefAccess<EnemyValuableThrower, PhysGrabObject>("valuableTarget");
    private static readonly Action<EnemyValuableThrower, EnemyValuableThrower.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyValuableThrower, EnemyValuableThrower.State>>(
            AccessTools.Method(typeof(EnemyValuableThrower), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyValuableThrower enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState != EnemyValuableThrower.State.TargetPlayer) runtime.Tactics.ResetFlank();
        if (enemy.currentState == EnemyValuableThrower.State.PlayerNotice)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        if (runtime.PreviousState == (int)EnemyValuableThrower.State.Throw &&
            enemy.currentState == EnemyValuableThrower.State.Leave && !runtime.ExtraActionUsed &&
            DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(enemy)))
        {
            runtime.ExtraActionUsed = true;
            UpdateState(enemy, EnemyValuableThrower.State.GetValuable);
        }
        if (enemy.currentState == EnemyValuableThrower.State.PlayerNotice &&
            runtime.PreviousState != (int)EnemyValuableThrower.State.PlayerNotice)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyValuableThrower), "StateGetValuable")]
    private static void StateGetValuablePostfix(EnemyValuableThrower __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteRegistry.TryGet(__instance, out _) ||
            !DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(__instance)))
        {
            return;
        }
        PhysGrabObject? best = null;
        float bestValue = float.MinValue;
        foreach (Collider collider in Physics.OverlapSphere(
                     PlayerTarget(__instance).transform.position,
                     10f,
                     LayerMask.GetMask("PhysGrabObject")))
        {
            ValuableObject? valuable = collider.GetComponentInParent<ValuableObject>();
            PhysGrabObject? physGrab = valuable != null ? GameFieldAccess.ValuablePhysGrabObject(valuable) : null;
            PhysGrabObjectImpactDetector? impact = physGrab != null ? GameFieldAccess.ImpactDetector(physGrab) : null;
            if (valuable == null || physGrab == null || impact == null ||
                GameFieldAccess.IsIndestructible(impact))
            {
                continue;
            }
            if (!NavMesh.SamplePosition(physGrab.transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                continue;
            }
            NavMeshPath path = new();
            if (!NavMesh.CalculatePath(__instance.transform.position, hit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }
            float value = GameFieldAccess.ValuableDollarValue(valuable);
            if (value > bestValue)
            {
                bestValue = value;
                best = physGrab;
            }
        }
        if (best != null)
        {
            ValuableTarget(__instance) = best;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.EnemyCartJump))]
    private static bool EnemyCartJumpPrefix(Enemy enemy)
    {
        EnemyValuableThrower? rugrat = enemy != null ? enemy.GetComponent<EnemyValuableThrower>() : null;
        return rugrat == null || rugrat.currentState != EnemyValuableThrower.State.GoToTarget ||
               !EliteRegistry.TryGet(rugrat, out _);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyJump), nameof(EnemyJump.StuckTrigger))]
    private static bool StuckTriggerPrefix(EnemyJump __instance)
    {
        EnemyValuableThrower? rugrat = __instance.GetComponent<EnemyValuableThrower>();
        if (rugrat == null || rugrat.currentState != EnemyValuableThrower.State.GoToTarget ||
            !EliteRegistry.TryGet(rugrat, out _))
        {
            return true;
        }
        __instance.StuckReset();
        return false;
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyValuableThrower), "Update")]
    private static void UpdatePostfix(EnemyValuableThrower __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Rugrat, Tick);

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyValuableThrower), "StateTargetPlayer")]
    private static void TargetPlayerPostfix(EnemyValuableThrower __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Rugrat, (state, enemy) =>
        {
            if (!TacticalPositioning.IsActive(enemy, out _) || enemy.currentState != EnemyValuableThrower.State.TargetPlayer ||
                ValuableTarget(enemy) == null || StateTimer(enemy) <= 0.25f ||
                !DedicatedBehaviorSupport.CanUseAsUnseenTarget(PlayerTarget(enemy))) return;
            TacticalRuntimeState tactics = DedicatedBehaviorSupport.Runtime(state).Tactics;
            if (TacticalPositioning.ClearLine(enemy.transform.position + Vector3.up, PlayerTarget(enemy).transform.position + Vector3.up))
            { tactics.FlankMoving = false; return; }
            if (!tactics.FlankAttempted)
            {
                tactics.FlankAttempted = true;
                if (TacticalPositioning.ThrowingFlank(state, PlayerTarget(enemy), out Vector3 flank)) tactics.BeginFlank(flank);
            }
            if (tactics.ContinueFlank(enemy.transform.position))
                GameFieldAccess.NavMeshAgent(state.Enemy).SetDestination(tactics.FlankPoint);
        });
}
