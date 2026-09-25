using System;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class AnimalElitePatches
{
    private static readonly Action<EnemyAnimal, EnemyAnimal.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyAnimal, EnemyAnimal.State>>(
            AccessTools.Method(typeof(EnemyAnimal), "UpdateState"));
    private static readonly AccessTools.FieldRef<EnemyAnimal, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyAnimal, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyAnimal, float> HavocTimer =
        AccessTools.FieldRefAccess<EnemyAnimal, float>("havocTimer");
    private static readonly AccessTools.FieldRef<EnemyAnimal, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyAnimal, PlayerAvatar>("playerTarget");
    private static readonly AccessTools.FieldRef<EnemyAnimal, Vector3> AgentDestination =
        AccessTools.FieldRefAccess<EnemyAnimal, Vector3>("agentDestination");

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyAnimal animal)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (animal.currentState == EnemyAnimal.State.PlayerNotice)
        {
            DedicatedBehaviorSupport.Countdown(animal, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        else if (animal.currentState == EnemyAnimal.State.WreakHavoc)
        {
            DedicatedBehaviorSupport.Countdown(animal, HavocTimer, DedicatedBehaviorSupport.CooldownRate(state));
            if (runtime.PreviousState != (int)EnemyAnimal.State.WreakHavoc) runtime.Tactics.NextScanAt = 0f;
            if (TacticalPositioning.AnimalDestination(state, PlayerTarget(animal), runtime.Tactics, out Vector3 point))
            {
                AgentDestination(animal) = point;
                GameFieldAccess.NavMeshAgent(state.Enemy).SetDestination(point);
                HavocTimer(animal) = Mathf.Max(HavocTimer(animal), 0.3f);
            }
        }
        if (runtime.PreviousState == (int)EnemyAnimal.State.WreakHavoc &&
            animal.currentState == EnemyAnimal.State.Leave && !runtime.ExtraActionUsed &&
            DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(animal)))
        {
            runtime.ExtraActionUsed = true;
            UpdateState(animal, EnemyAnimal.State.GoToPlayer);
        }
        if (animal.currentState == EnemyAnimal.State.PlayerNotice &&
            runtime.PreviousState != (int)EnemyAnimal.State.PlayerNotice)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)animal.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyAnimal), "Update")]
    private static void UpdatePostfix(EnemyAnimal __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Animal, Tick);
}

[HarmonyPatch]
internal static class BirthdayBoyElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyBirthdayBoy, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyBirthdayBoy, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyBirthdayBoy, float> SecondStateTimer =
        AccessTools.FieldRefAccess<EnemyBirthdayBoy, float>("secondStateTimer");
    private static readonly AccessTools.FieldRef<EnemyBirthdayBoy, PhotonView> PhotonViewRef =
        AccessTools.FieldRefAccess<EnemyBirthdayBoy, PhotonView>("photonView");
    private static readonly Action<EnemyBirthdayBoy, PlayerAvatar> UpdatePlayerTarget =
        AccessTools.MethodDelegate<Action<EnemyBirthdayBoy, PlayerAvatar>>(
            AccessTools.Method(typeof(EnemyBirthdayBoy), "UpdatePlayerTarget"));
    private static readonly Action<EnemyBirthdayBoy, EnemyBirthdayBoy.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyBirthdayBoy, EnemyBirthdayBoy.State>>(
            AccessTools.Method(typeof(EnemyBirthdayBoy), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyBirthdayBoy enemy)
    {
        if (enemy.currentState == EnemyBirthdayBoy.State.PlayerNotice)
        {
            DedicatedBehaviorSupport.Countdown(enemy, SecondStateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        else if (enemy.currentState == EnemyBirthdayBoy.State.Attack)
        {
            // stateTimer is the pursuit lifetime, not an attack cooldown. Retaining it
            // keeps the Birthday Boy engaged instead of making it abandon attacks sooner.
            DedicatedBehaviorSupport.RetainCountdown(enemy, StateTimer, DedicatedBehaviorSupport.Retention(state));
        }
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyBirthdayBoy), "Update")]
    private static void UpdatePostfix(EnemyBirthdayBoy __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.BirthdayBoy, Tick);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyBirthdayBoy), "CheckForPoppedBalloons")]
    private static void CheckForPoppedBalloonsPrefix(EnemyBirthdayBoy __instance, ref PlayerAvatar? __state)
    {
        __state = null;
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !EliteRegistry.TryGet(__instance, out _))
        {
            return;
        }
        foreach (GameObject balloonObject in __instance.balloons.Values)
        {
            BirthdayBoyBalloon? balloon = balloonObject != null
                ? balloonObject.GetComponentInChildren<BirthdayBoyBalloon>()
                : null;
            if (balloon != null && balloon.popped && DedicatedBehaviorSupport.IsValidTarget(balloon.popper))
            {
                __state = balloon.popper;
                return;
            }
        }
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyBirthdayBoy), "CheckForPoppedBalloons")]
    private static void CheckForPoppedBalloonsPostfix(EnemyBirthdayBoy __instance, PlayerAvatar? __state)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __state == null ||
            !EliteRegistry.TryGet(__instance, out _))
        {
            return;
        }
        UpdatePlayerTarget(__instance, __state);
        if (GameManager.Multiplayer())
        {
            PhotonViewRef(__instance).RPC("UpdatePlayerTargetRPC", RpcTarget.Others, __state.photonView.ViewID);
        }
        StateTimer(__instance) = 20f;
        UpdateState(__instance, EnemyBirthdayBoy.State.Attack);
    }
}

[HarmonyPatch]
internal static class CleanupCrewElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyBombThrowerHead, EnemyBombThrowerHead.State> HeadState =
        AccessTools.FieldRefAccess<EnemyBombThrowerHead, EnemyBombThrowerHead.State>("currentState");
    private static readonly AccessTools.FieldRef<EnemyBombThrower, EnemyBombThrower.State> CurrentState =
        AccessTools.FieldRefAccess<EnemyBombThrower, EnemyBombThrower.State>("currentState");
    private static readonly AccessTools.FieldRef<EnemyBombThrower, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyBombThrower, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyBombThrower, float> HeadCooldown =
        AccessTools.FieldRefAccess<EnemyBombThrower, float>("headCooldown");
    private static readonly AccessTools.FieldRef<EnemyBombThrower, float> HeadAttackCooldown =
        AccessTools.FieldRefAccess<EnemyBombThrower, float>("headAttackCooldown");
    private static readonly AccessTools.FieldRef<EnemyBombThrower, float> MeleeCooldown =
        AccessTools.FieldRefAccess<EnemyBombThrower, float>("meleeCooldown");
    private static readonly AccessTools.FieldRef<EnemyBombThrower, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyBombThrower, PlayerAvatar>("playerTarget");
    private static readonly Action<EnemyBombThrower, EnemyBombThrower.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyBombThrower, EnemyBombThrower.State>>(
            AccessTools.Method(typeof(EnemyBombThrower), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyBombThrower enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (CurrentState(enemy) == EnemyBombThrower.State.Attack && runtime.PreviousState != (int)EnemyBombThrower.State.Attack)
            runtime.Tactics.ResetFlank();
        if (CurrentState(enemy) == EnemyBombThrower.State.Notice)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        float rate = DedicatedBehaviorSupport.CooldownRate(state);
        DedicatedBehaviorSupport.Countdown(enemy, HeadCooldown, rate);
        DedicatedBehaviorSupport.Countdown(enemy, HeadAttackCooldown, rate);
        DedicatedBehaviorSupport.Countdown(enemy, MeleeCooldown, rate);
        if (runtime.PreviousState == (int)EnemyBombThrower.State.Attack &&
            CurrentState(enemy) == EnemyBombThrower.State.BackAwayHead &&
            !runtime.ExtraActionUsed && DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(enemy)) &&
            Vector3.Distance(enemy.transform.position, PlayerTarget(enemy).transform.position) <= 4f)
        {
            runtime.ExtraActionUsed = true;
            UpdateState(enemy, EnemyBombThrower.State.Melee);
        }
        if (CurrentState(enemy) == EnemyBombThrower.State.Notice &&
            runtime.PreviousState != (int)EnemyBombThrower.State.Notice)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)CurrentState(enemy));
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyBombThrower), "Update")]
    private static void UpdatePostfix(EnemyBombThrower __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.CleanupCrew, Tick);

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyBombThrower), "StateBackAwayHead")]
    private static void BackAwayHeadPostfix(EnemyBombThrower __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.CleanupCrew, (state, enemy) =>
        {
            if (!TacticalPositioning.IsActive(enemy, out _) || CurrentState(enemy) != EnemyBombThrower.State.BackAwayHead) return;
            TacticalRuntimeState tactics = DedicatedBehaviorSupport.Runtime(state).Tactics;
            if (enemy.head == null || HeadState(enemy.head) != EnemyBombThrowerHead.State.Active)
            {
                if (tactics.FlankMoving) GameFieldAccess.NavMeshAgent(state.Enemy).ResetPath();
                tactics.FlankMoving = false;
                return;
            }
            Vector3 bomb = enemy.head.transform.position;
            if (Vector3.Distance(enemy.transform.position, bomb) < 6f)
            { tactics.FlankMoving = false; return; }
            if (!TacticalPositioning.VisiblePlayer(state.Enemy, PlayerTarget(enemy)))
            {
                if (tactics.FlankMoving) GameFieldAccess.NavMeshAgent(state.Enemy).ResetPath();
                tactics.FlankMoving = false;
                return;
            }
            if (!tactics.FlankAttempted)
            {
                tactics.FlankAttempted = true;
                if (TacticalPositioning.BombPincer(state, PlayerTarget(enemy), bomb, out Vector3 flank)) tactics.BeginFlank(flank);
            }
            bool wasMoving = tactics.FlankMoving;
            if (tactics.ContinueFlank(enemy.transform.position))
            {
                if (TacticalPositioning.SafeBombRoute(state, bomb, tactics.FlankPoint))
                    GameFieldAccess.NavMeshAgent(state.Enemy).SetDestination(tactics.FlankPoint);
                else
                {
                    tactics.FlankMoving = false;
                    GameFieldAccess.NavMeshAgent(state.Enemy).ResetPath();
                }
            }
            else if (wasMoving) GameFieldAccess.NavMeshAgent(state.Enemy).ResetPath();
        });
}

[HarmonyPatch]
internal static class BowtieElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyBowtie, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyBowtie, float>("stateTimer");
    private static readonly Action<EnemyBowtie, EnemyBowtie.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyBowtie, EnemyBowtie.State>>(
            AccessTools.Method(typeof(EnemyBowtie), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyBowtie enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState == EnemyBowtie.State.PlayerNotice)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        else if (enemy.currentState == EnemyBowtie.State.YellEnd)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.CooldownRate(state));
        }
        if (runtime.PreviousState == (int)EnemyBowtie.State.YellEnd &&
            enemy.currentState is EnemyBowtie.State.Idle or EnemyBowtie.State.Leave &&
            !runtime.ExtraActionUsed)
        {
            runtime.ExtraActionUsed = true;
            UpdateState(enemy, EnemyBowtie.State.Yell);
        }
        if (enemy.currentState == EnemyBowtie.State.PlayerNotice &&
            runtime.PreviousState != (int)EnemyBowtie.State.PlayerNotice)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyBowtie), "Update")]
    private static void UpdatePostfix(EnemyBowtie __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Bowtie, Tick);
}

[HarmonyPatch]
internal static class PeeperElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyCeilingEye, float> DamageWaitTimer =
        AccessTools.FieldRefAccess<EnemyCeilingEye, float>("eyeDamageWaitTimer");
    private static readonly AccessTools.FieldRef<EnemyCeilingEye, float> DamageTimer =
        AccessTools.FieldRefAccess<EnemyCeilingEye, float>("eyeDamageTimer");
    private static readonly AccessTools.FieldRef<EnemyCeilingEye, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyCeilingEye, PlayerAvatar>("targetPlayer");
    private static readonly AccessTools.FieldRef<EnemyCeilingEye, PhotonView> PhotonViewRef =
        AccessTools.FieldRefAccess<EnemyCeilingEye, PhotonView>("photonView");
    private static readonly Action<EnemyCeilingEye, EnemyCeilingEye.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyCeilingEye, EnemyCeilingEye.State>>(
            AccessTools.Method(typeof(EnemyCeilingEye), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyCeilingEye enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState == EnemyCeilingEye.State.HasTarget)
        {
            DedicatedBehaviorSupport.Countdown(enemy, DamageWaitTimer, DedicatedBehaviorSupport.WindupRate(state));
            DedicatedBehaviorSupport.Countdown(enemy, DamageTimer, DedicatedBehaviorSupport.CooldownRate(state));
        }
        if (runtime.PreviousState == (int)EnemyCeilingEye.State.HasTarget &&
            enemy.currentState == EnemyCeilingEye.State.TargetLost && !runtime.ExtraActionUsed &&
            DedicatedBehaviorSupport.TryFindNearestPlayer(
                enemy.eyeTransform.position,
                20f,
                out PlayerAvatar next,
                PlayerTarget(enemy),
                state.Enemy))
        {
            runtime.ExtraActionUsed = true;
            PlayerTarget(enemy) = next;
            if (GameManager.Multiplayer())
            {
                PhotonViewRef(enemy).RPC("TargetPlayerRPC", RpcTarget.Others, next.photonView.ViewID);
            }
            UpdateState(enemy, EnemyCeilingEye.State.HasTarget);
        }
        if (enemy.currentState == EnemyCeilingEye.State.Idle)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyCeilingEye), "Update")]
    private static void UpdatePostfix(EnemyCeilingEye __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Peeper, Tick);
}

[HarmonyPatch]
internal static class ApexPredatorElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyDuck, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyDuck, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyDuck, float> ChaseTimer =
        AccessTools.FieldRefAccess<EnemyDuck, float>("chaseTimer");
    private static readonly AccessTools.FieldRef<EnemyDuck, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyDuck, PlayerAvatar>("playerTarget");
    private static readonly Action<EnemyDuck, EnemyDuck.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyDuck, EnemyDuck.State>>(
            AccessTools.Method(typeof(EnemyDuck), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyDuck enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState is EnemyDuck.State.Notice or EnemyDuck.State.AttackStart or EnemyDuck.State.Transform)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        if (enemy.currentState is EnemyDuck.State.ChaseNavmesh or EnemyDuck.State.ChaseTowards or EnemyDuck.State.ChaseMoveBack)
        {
            DedicatedBehaviorSupport.RetainCountup(enemy, ChaseTimer, DedicatedBehaviorSupport.Retention(state));
        }
        if (enemy.currentState == EnemyDuck.State.DeTransform &&
            runtime.PreviousState is (int)EnemyDuck.State.ChaseNavmesh or
                (int)EnemyDuck.State.ChaseTowards or (int)EnemyDuck.State.ChaseMoveBack &&
            !runtime.ExtraActionUsed && DedicatedBehaviorSupport.IsValidTarget(PlayerTarget(enemy)))
        {
            runtime.ExtraActionUsed = true;
            UpdateState(enemy, EnemyDuck.State.AttackStart);
        }
        if (enemy.currentState == EnemyDuck.State.Notice &&
            runtime.PreviousState != (int)EnemyDuck.State.Notice)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyDuck), "Update")]
    private static void UpdatePostfix(EnemyDuck __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.ApexPredator, Tick);
}

[HarmonyPatch]
internal static class ElsaElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyElsa, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyElsa, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyElsa, float> AngryTimer =
        AccessTools.FieldRefAccess<EnemyElsa, float>("angryTimer");
    private static readonly AccessTools.FieldRef<EnemyElsa, float> ChaseTimer =
        AccessTools.FieldRefAccess<EnemyElsa, float>("chaseTimer");
    private static readonly AccessTools.FieldRef<EnemyElsa, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyElsa, PlayerAvatar>("playerTarget");
    private static readonly AccessTools.FieldRef<EnemyElsa, PhotonView> PhotonViewRef =
        AccessTools.FieldRefAccess<EnemyElsa, PhotonView>("photonView");
    private static readonly Action<EnemyElsa, EnemyElsa.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyElsa, EnemyElsa.State>>(
            AccessTools.Method(typeof(EnemyElsa), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyElsa enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState == EnemyElsa.State.NoticeSmall)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        if (enemy.currentState is EnemyElsa.State.GoToPlayerSmall or EnemyElsa.State.GoToPlayerOverSmall or EnemyElsa.State.GoToPlayerUnderSmall)
        {
            DedicatedBehaviorSupport.Countup(enemy, AngryTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        if (enemy.currentState is EnemyElsa.State.GoToPlayerBig or EnemyElsa.State.GoToPlayerOverBig or EnemyElsa.State.LookUnderBig)
        {
            DedicatedBehaviorSupport.RetainCountup(enemy, ChaseTimer, DedicatedBehaviorSupport.Retention(state));
        }
        if (enemy.currentState == EnemyElsa.State.TransformBigToSmall &&
            runtime.PreviousState != (int)EnemyElsa.State.TransformBigToSmall && !runtime.ExtraActionUsed &&
            DedicatedBehaviorSupport.TryFindNearestPlayer(
                enemy.transform.position,
                15f,
                out PlayerAvatar next,
                PlayerTarget(enemy),
                state.Enemy))
        {
            runtime.ExtraActionUsed = true;
            PlayerTarget(enemy) = next;
            GameFieldAccess.SetTargetPlayer(state.Enemy, next);
            GameFieldAccess.SetTargetPlayerViewId(state.Enemy, next.photonView.ViewID);
            if (GameManager.Multiplayer())
            {
                PhotonViewRef(enemy).RPC("UpdatePlayerTargetRPC", RpcTarget.Others, next.photonView.ViewID);
            }
            ChaseTimer(enemy) = 0f;
            UpdateState(enemy, EnemyElsa.State.GoToPlayerBig);
        }
        if (enemy.currentState == EnemyElsa.State.NoticeSmall &&
            runtime.PreviousState != (int)EnemyElsa.State.NoticeSmall)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyElsa), "Update")]
    private static void UpdatePostfix(EnemyElsa __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Elsa, Tick);
}

[HarmonyPatch]
internal static class MentalistElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyFloater, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyFloater, PlayerAvatar>("targetPlayer");
    private static readonly AccessTools.FieldRef<EnemyFloater, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyFloater, float>("stateTimer");
    private static readonly Action<EnemyFloater, EnemyFloater.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyFloater, EnemyFloater.State>>(
            AccessTools.Method(typeof(EnemyFloater), "UpdateState"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyFloater enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState is EnemyFloater.State.Notice or EnemyFloater.State.ChargeAttack or EnemyFloater.State.DelayAttack)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        if (runtime.PreviousState == (int)EnemyFloater.State.Attack &&
            enemy.currentState is EnemyFloater.State.Idle or EnemyFloater.State.Leave &&
            !runtime.ExtraActionUsed)
        {
            runtime.ExtraActionUsed = true;
            UpdateState(enemy, EnemyFloater.State.DelayAttack);
        }
        if (enemy.currentState == EnemyFloater.State.Notice &&
            runtime.PreviousState != (int)EnemyFloater.State.Notice)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyFloater), "Update")]
    private static void UpdatePostfix(EnemyFloater __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Mentalist, Tick);

    [HarmonyPrefix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyFloater), "UpdateState")]
    private static void UpdateStatePrefix(EnemyFloater __instance, ref EnemyFloater.State _state)
    {
        if (!TacticalPositioning.IsActive(__instance, out EliteState state)) return;
        TacticalRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state).Tactics;
        try
        {
            if (_state is EnemyFloater.State.Idle or EnemyFloater.State.Roam or EnemyFloater.State.Leave or
                EnemyFloater.State.Stun or EnemyFloater.State.Despawn)
            {
                runtime.Repositioning = false;
                runtime.PreparationUsed = false;
            }
            if (_state != EnemyFloater.State.ChargeAttack || runtime.PreparationUsed) return;
            runtime.PreparationUsed = true;
            if (!TacticalPositioning.MentalistPosition(state, __instance, PlayerTarget(__instance), out Vector3 point)) return;
            runtime.BeginReposition(PlayerTarget(__instance), point, Time.time);
            // Redirect before the vanilla RPC publishes the charge state and starts the aura.
            _state = EnemyFloater.State.GoToPlayer;
        }
        catch (Exception exception)
        {
            runtime.Repositioning = false;
            EliteRegistry.RecordAdapterTickFailure(state, exception, "Mentalist positioning");
        }
    }

    [HarmonyPrefix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyFloater), nameof(EnemyFloater.StateGoToPlayer))]
    private static bool StateGoToPlayerPrefix(EnemyFloater __instance)
    {
        if (!TacticalPositioning.IsActive(__instance, out EliteState state)) return true;
        TacticalRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state).Tactics;
        if (!runtime.Repositioning) return true;
        try
        {
            PlayerAvatar target = PlayerTarget(__instance);
            bool visible = target == runtime.RepositionTarget && TacticalPositioning.VisiblePlayer(state.Enemy, target);
            bool forceLeave = SemiFunc.EnemyForceLeave(state.Enemy);
            bool done = Time.time >= runtime.RepositionDeadline ||
                Vector3.Distance(__instance.feetTransform.position, runtime.RepositionPoint) <= 0.65f;
            if (forceLeave || !visible || done)
            {
                runtime.Repositioning = false;
                EnemyFloater.State next = forceLeave ? EnemyFloater.State.Leave :
                    visible && Vector3.Distance(__instance.feetTransform.position, target.transform.position) <= 4f
                        ? EnemyFloater.State.ChargeAttack : EnemyFloater.State.Idle;
                UpdateState(__instance, next);
                return false;
            }
            EnemyNavMeshAgent nav = GameFieldAccess.NavMeshAgent(state.Enemy);
            nav.SetDestination(runtime.RepositionPoint);
            nav.OverrideAgent(2f, GameFieldAccess.NavDefaultAcceleration(nav), 0.2f);
            return false;
        }
        catch (Exception exception)
        {
            runtime.Repositioning = false;
            EliteRegistry.RecordAdapterTickFailure(state, exception, "Mentalist positioning movement");
            return true;
        }
    }
}

[HarmonyPatch]
internal static class HeadmanElitePatches
{
    private static readonly AccessTools.FieldRef<Enemy, float> DisableChaseTimer =
        AccessTools.FieldRefAccess<Enemy, float>("DisableChaseTimer");
    private static readonly AccessTools.FieldRef<EnemyStateChase, float> ChaseStateTimer =
        AccessTools.FieldRefAccess<EnemyStateChase, float>("StateTimer");

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, Enemy enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.CurrentState == EnemyState.Sneak)
        {
            enemy.CurrentState = EnemyState.ChaseBegin;
        }
        else if (enemy.CurrentState == EnemyState.Chase &&
                 runtime.PreviousState != (int)EnemyState.Chase && GameFieldAccess.StateChase(enemy) != null)
        {
            EnemyStateChase chase = GameFieldAccess.StateChase(enemy);
            ChaseStateTimer(chase) = Mathf.Max(ChaseStateTimer(chase), 10f);
        }
        else if (runtime.PreviousState == (int)EnemyState.Chase &&
                 enemy.CurrentState == EnemyState.ChaseSlow && !runtime.ExtraActionUsed)
        {
            PlayerAvatar? target = GameFieldAccess.TargetPlayer(enemy);
            if (DedicatedBehaviorSupport.IsValidTarget(target) &&
                Vector3.Distance(enemy.transform.position, target.transform.position) <= 30f)
            {
                runtime.ExtraActionUsed = true;
                enemy.CurrentState = EnemyState.ChaseBegin;
            }
        }
        if (enemy.CurrentState is EnemyState.Roaming or EnemyState.Investigate)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.Countdown(enemy, DisableChaseTimer, DedicatedBehaviorSupport.CooldownRate(state));
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.CurrentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(Enemy), "Update")]
    private static void UpdatePostfix(Enemy __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Headman, Tick);
}

[HarmonyPatch]
internal static class HeadgrabElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyHeadGrabber, PlayerDeathHead> HeadTarget =
        AccessTools.FieldRefAccess<EnemyHeadGrabber, PlayerDeathHead>("headTarget");
    private static readonly Action<EnemyHeadGrabber, PlayerDeathHead> UpdateHeadTarget =
        AccessTools.MethodDelegate<Action<EnemyHeadGrabber, PlayerDeathHead>>(
            AccessTools.Method(typeof(EnemyHeadGrabber), "UpdateHeadTarget"));
    private static readonly Action<EnemyHeadGrabber, EnemyHeadGrabber.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemyHeadGrabber, EnemyHeadGrabber.State>>(
            AccessTools.Method(typeof(EnemyHeadGrabber), "UpdateState"));
    private static readonly AccessTools.FieldRef<EnemyHeadGrabber, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyHeadGrabber, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyHeadGrabber, float> AttackCooldown =
        AccessTools.FieldRefAccess<EnemyHeadGrabber, float>("attackCooldown");
    private static readonly AccessTools.FieldRef<EnemyHeadGrabber, float> HeadTargetCooldown =
        AccessTools.FieldRefAccess<EnemyHeadGrabber, float>("headTargetCooldown");
    private static readonly AccessTools.FieldRef<EnemyHeadGrabber, int> AttacksInRow =
        AccessTools.FieldRefAccess<EnemyHeadGrabber, int>("attacksInRow");
    private static readonly AccessTools.FieldRef<EnemyHeadGrabber, bool> HeadTargetActive =
        AccessTools.FieldRefAccess<EnemyHeadGrabber, bool>("headTargetActive");

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyHeadGrabber enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        RecoverDroppedHead(state, enemy, runtime.Tactics);
        if (enemy.currentState == EnemyHeadGrabber.State.Notice)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        float rate = DedicatedBehaviorSupport.CooldownRate(state);
        DedicatedBehaviorSupport.Countdown(enemy, AttackCooldown, rate);
        DedicatedBehaviorSupport.Countdown(enemy, HeadTargetCooldown, rate);
        if (enemy.currentState == EnemyHeadGrabber.State.Attack && !HeadTargetActive(enemy) &&
            AttacksInRow(enemy) > 3 && StateTimer(enemy) <= Time.deltaTime * 2f &&
            !runtime.ExtraActionUsed)
        {
            // Preserve the vanilla >3 exit rule, but grant one additional combo
            // instead of continually lowering the counter and removing its cap.
            runtime.ExtraActionUsed = true;
            AttacksInRow(enemy) = 3;
        }
        if (enemy.currentState == EnemyHeadGrabber.State.Notice &&
            runtime.PreviousState != (int)EnemyHeadGrabber.State.Notice)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHeadGrabber), "Update")]
    private static void UpdatePostfix(EnemyHeadGrabber __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Headgrab, Tick);

    private static void CancelRecovery(EnemyHeadGrabber enemy, TacticalRuntimeState tactics)
    {
        bool owned = tactics.RecoveringHead && HeadTarget(enemy) == tactics.RecoveryHead && !HeadTargetActive(enemy);
        tactics.RecoveryHead = null;
        tactics.RecoveringHead = false;
        if (owned && enemy.currentState is EnemyHeadGrabber.State.GotoHead or EnemyHeadGrabber.State.GotoHeadOver or EnemyHeadGrabber.State.GrabHead)
        {
            UpdateState(enemy, EnemyHeadGrabber.State.Idle);
            HeadTarget(enemy) = null!;
        }
    }

    private static void RecoverDroppedHead(EliteState state, EnemyHeadGrabber enemy, TacticalRuntimeState tactics)
    {
        if (!TacticalPositioning.IsActive(enemy, out _) || tactics.RecoveryHead == null) return;
        if (HeadTargetActive(enemy)) { tactics.RecoveryHead = null; tactics.RecoveringHead = false; return; }
        if (!TacticalPositioning.RecoverableHead(tactics)) { CancelRecovery(enemy, tactics); return; }
        if (tactics.RecoveringHead)
        {
            if (enemy.currentState is not (EnemyHeadGrabber.State.GotoHead or EnemyHeadGrabber.State.GotoHeadOver or
                EnemyHeadGrabber.State.GrabHead or EnemyHeadGrabber.State.BackToNavmesh)) CancelRecovery(enemy, tactics);
            return;
        }
        // Wait for vanilla stun recovery and return to the navigation mesh before issuing a new pursuit.
        if (enemy.currentState is not (EnemyHeadGrabber.State.Idle or EnemyHeadGrabber.State.Roam or EnemyHeadGrabber.State.RoamFast)) return;
        if (!TacticalPositioning.TryPath(enemy.transform.position, tactics.RecoveryHead.transform.position, 10f, out _, out _))
        { CancelRecovery(enemy, tactics); return; }
        tactics.RecoveringHead = true;
        tactics.RecoveryDeadline = Time.time + 5f;
        UpdateHeadTarget(enemy, tactics.RecoveryHead);
        UpdateState(enemy, EnemyHeadGrabber.State.GotoHead);
    }

    [HarmonyPrefix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHeadGrabber), nameof(EnemyHeadGrabber.DeathHeadRelease))]
    private static void ReleaseHeadPrefix(EnemyHeadGrabber __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Headgrab, (state, enemy) =>
        {
            if (TacticalPositioning.IsActive(enemy, out _) && enemy.currentState == EnemyHeadGrabber.State.Stun &&
                HeadTargetActive(enemy) && HeadTarget(enemy) != null)
                DedicatedBehaviorSupport.Runtime(state).Tactics.RememberDroppedHead(HeadTarget(enemy), enemy.headTargetPositionReleaseTransform.position);
        });

    [HarmonyPrefix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHeadGrabber), "StateGotoHead")]
    private static bool RecoverHeadPrefix(EnemyHeadGrabber __instance)
    {
        bool runOriginal = true;
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Headgrab, (state, enemy) =>
        {
            TacticalRuntimeState tactics = DedicatedBehaviorSupport.Runtime(state).Tactics;
            if (TacticalPositioning.IsActive(enemy, out _) && tactics.RecoveringHead && !HeadTargetActive(enemy) &&
                !TacticalPositioning.RecoverableHead(tactics))
            { CancelRecovery(enemy, tactics); runOriginal = false; }
        });
        return runOriginal;
    }

    [HarmonyPrefix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHeadGrabber), "StateGotoHeadOver")]
    private static bool RecoverHeadOverPrefix(EnemyHeadGrabber __instance) => RecoverHeadPrefix(__instance);

    [HarmonyPrefix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHeadGrabber), "StateGrabHead")]
    private static bool GrabRecoveredHeadPrefix(EnemyHeadGrabber __instance) => RecoverHeadPrefix(__instance);
}

[HarmonyPatch]
internal static class HeartHuggerElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyHeartHugger, PlayerAvatar> CurrentTarget =
        AccessTools.FieldRefAccess<EnemyHeartHugger, PlayerAvatar>("currentTarget");
    private static readonly AccessTools.FieldRef<EnemyHeartHugger, List<LevelPoint>> LevelPoints =
        AccessTools.FieldRefAccess<EnemyHeartHugger, List<LevelPoint>>("levelPoints");
    private static readonly AccessTools.FieldRef<EnemyHeartHugger, bool> DoLeave =
        AccessTools.FieldRefAccess<EnemyHeartHugger, bool>("doLeave");
    private static readonly AccessTools.FieldRef<EnemyHeartHugger, bool> DoDespawn =
        AccessTools.FieldRefAccess<EnemyHeartHugger, bool>("doDespawn");
    private static readonly AccessTools.FieldRef<EnemyHeartHugger, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyHeartHugger, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyHeartHugger, int> AttacksInRow =
        AccessTools.FieldRefAccess<EnemyHeartHugger, int>("attacksInARow");
    private static readonly Action<EnemyHeartHugger, EnemyHeartHugger.State> StateSet =
        AccessTools.MethodDelegate<Action<EnemyHeartHugger, EnemyHeartHugger.State>>(
            AccessTools.Method(typeof(EnemyHeartHugger), "StateSet"));

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyHeartHugger enemy)
    {
        if (enemy.currentState is EnemyHeartHugger.State.AggroStart or EnemyHeartHugger.State.LureStart or EnemyHeartHugger.State.ChompGasp)
        {
            DedicatedBehaviorSupport.Countup(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
    }

    [HarmonyPrefix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHeartHugger), "LeaveStart")]
    private static bool LeaveStartPrefix(EnemyHeartHugger __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteRegistry.TryGet(__instance, out EliteState state))
        {
            return true;
        }
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (__instance.currentState != EnemyHeartHugger.State.Chomp || runtime.ExtraActionUsed)
        {
            return true;
        }
        runtime.ExtraActionUsed = true;
        AttacksInRow(__instance) = 0;
        StateSet(__instance, EnemyHeartHugger.State.Aggro);
        return false;
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHeartHugger), "Update")]
    private static void UpdatePostfix(EnemyHeartHugger __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.HeartHugger, Tick);

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHeartHugger), nameof(EnemyHeartHugger.OnVision))]
    private static void OnVisionPostfix(EnemyHeartHugger __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.HeartHugger, (state, enemy) =>
        {
            PlayerAvatar target = CurrentTarget(enemy);
            if (TacticalPositioning.IsActive(enemy, out _) && TacticalPositioning.VisiblePlayer(state.Enemy, target))
                TacticalPositioning.RememberSeenPlayer(DedicatedBehaviorSupport.Runtime(state).Tactics, target);
        });

    [HarmonyPrefix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHeartHugger), "SetPositionToRandomLevelPoint")]
    private static void ChoosePositionPrefix(EnemyHeartHugger __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.HeartHugger, (state, enemy) =>
        {
            if (DoLeave(enemy) || DoDespawn(enemy) || enemy.currentState is not (EnemyHeartHugger.State.Grow or EnemyHeartHugger.State.AggroGrow)) return;
            TacticalRuntimeState tactics = DedicatedBehaviorSupport.Runtime(state).Tactics;
            List<LevelPoint> candidates = LevelPoints(enemy);
            if (candidates != null && TacticalPositioning.AmbushExit(state, tactics, candidates, out LevelPoint? point))
            {
                LevelPoints(enemy) = new List<LevelPoint> { point! };
                tactics.HasSeenPlayer = false;
            }
        });
}

[HarmonyPatch]
internal static class HiddenElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyHidden, float> StateTimer =
        AccessTools.FieldRefAccess<EnemyHidden, float>("stateTimer");
    private static readonly AccessTools.FieldRef<EnemyHidden, float> MaxMoveTimer =
        AccessTools.FieldRefAccess<EnemyHidden, float>("maxMoveTimer");
    private static readonly AccessTools.FieldRef<EnemyHidden, Vector3> AgentDestination =
        AccessTools.FieldRefAccess<EnemyHidden, Vector3>("agentDestination");
    private static readonly AccessTools.FieldRef<EnemyHidden, PlayerAvatar> PlayerTarget =
        AccessTools.FieldRefAccess<EnemyHidden, PlayerAvatar>("playerTarget");

    internal static void OnSpawn(EliteState state) => DedicatedBehaviorSupport.OnSpawn(state);

    private static void Tick(EliteState state, EnemyHidden enemy)
    {
        DedicatedEnemyRuntimeState runtime = DedicatedBehaviorSupport.Runtime(state);
        if (enemy.currentState is EnemyHidden.State.PlayerNotice or EnemyHidden.State.PlayerPickup or EnemyHidden.State.PlayerReleaseWait)
        {
            DedicatedBehaviorSupport.Countdown(enemy, StateTimer, DedicatedBehaviorSupport.WindupRate(state));
        }
        if (enemy.currentState == EnemyHidden.State.PlayerMove)
        {
            if (runtime.PreviousState != (int)EnemyHidden.State.PlayerMove)
            {
                runtime.Tactics.RescueEvaded = false;
                runtime.Tactics.NextScanAt = 0f;
            }
            if (TacticalPositioning.EvadeRescuer(state, PlayerTarget(enemy), AgentDestination(enemy), runtime.Tactics, out Vector3 escape))
            {
                AgentDestination(enemy) = escape;
                GameFieldAccess.NavMeshAgent(state.Enemy).SetDestination(escape);
            }
            DedicatedBehaviorSupport.RetainCountdown(enemy, MaxMoveTimer, DedicatedBehaviorSupport.Retention(state));
            float moved = Vector3.Distance(runtime.LastPosition, enemy.transform.position);
            runtime.StuckTimer = moved < 0.05f ? runtime.StuckTimer + Time.deltaTime : 0f;
            runtime.LastPosition = enemy.transform.position;
            if (runtime.StuckTimer >= 1.5f && !runtime.ExtraActionUsed)
            {
                LevelPoint? point = SemiFunc.LevelPointGetPlayerDistance(enemy.transform.position, 20f, 80f);
                if (point != null && NavMesh.SamplePosition(point.transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    AgentDestination(enemy) = hit.position;
                    GameFieldAccess.NavMeshAgent(state.Enemy).SetDestination(hit.position);
                    runtime.ExtraActionUsed = true;
                }
                runtime.StuckTimer = 0f;
            }
        }
        else
        {
            runtime.StuckTimer = 0f;
            runtime.LastPosition = enemy.transform.position;
        }
        if (enemy.currentState == EnemyHidden.State.PlayerNotice &&
            runtime.PreviousState != (int)EnemyHidden.State.PlayerNotice)
        {
            runtime.ExtraActionUsed = false;
        }
        DedicatedBehaviorSupport.TrackState(runtime, (int)enemy.currentState);
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHidden), "Update")]
    private static void UpdatePostfix(EnemyHidden __instance) =>
        DedicatedBehaviorSupport.Run(__instance, EliteArchetype.Hidden, Tick);
}
