using System;
using HarmonyLib;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class GnomeElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyGnome, float> AttackAngle =
        AccessTools.FieldRefAccess<EnemyGnome, float>("attackAngle");
    private static readonly AccessTools.FieldRef<EnemyGnome, int> DirectorIndex =
        AccessTools.FieldRefAccess<EnemyGnome, int>("directorIndex");
    private static readonly AccessTools.FieldRef<EnemyGnome, float> OverlapCheckTimer =
        AccessTools.FieldRefAccess<EnemyGnome, float>("overlapCheckTimer");
    private static readonly AccessTools.FieldRef<EnemyGnome, float> OverlapCheckCooldown =
        AccessTools.FieldRefAccess<EnemyGnome, float>("overlapCheckCooldown");
    private static readonly AccessTools.FieldRef<EnemyGnome, bool> OverlapCheckPrevious =
        AccessTools.FieldRefAccess<EnemyGnome, bool>("overlapCheckPrevious");
    private static readonly AccessTools.FieldRef<EnemyGnomeDirector, bool> DirectorStateImpulse =
        AccessTools.FieldRefAccess<EnemyGnomeDirector, bool>("stateImpulse");
    private static readonly AccessTools.FieldRef<EnemyGnomeDirector, PlayerAvatar> DirectorPlayerTarget =
        AccessTools.FieldRefAccess<EnemyGnomeDirector, PlayerAvatar>("playerTarget");
    private static readonly AccessTools.FieldRef<EnemyGnomeDirector, PhysGrabObject> DirectorValuableTarget =
        AccessTools.FieldRefAccess<EnemyGnomeDirector, PhysGrabObject>("valuableTarget");
    private static readonly Action<EnemyGnome, EnemyGnome.State> UpdateGnomeState =
        AccessTools.MethodDelegate<Action<EnemyGnome, EnemyGnome.State>>(
            AccessTools.Method(typeof(EnemyGnome), "UpdateState"));
    private static readonly Action<EnemyGnomeDirector, EnemyGnomeDirector.State> UpdateDirectorState =
        AccessTools.MethodDelegate<Action<EnemyGnomeDirector, EnemyGnomeDirector.State>>(
            AccessTools.Method(typeof(EnemyGnomeDirector), "UpdateState"));

    internal static void OnSpawn(EliteState state)
    {
        EnemyGnome? gnome = state.Enemy.GetComponent<EnemyGnome>();
        if (gnome == null || GameFieldAccess.Jump(state.Enemy) == null)
        {
            return;
        }
        GnomeRuntimeState runtime = GetRuntime(state);
        if (runtime.StatsApplied)
        {
            return;
        }

        float multiplier = EliteEnemyVariantsPlugin.Instance.Settings.GnomeJumpMultiplier.Value;
        EnemyJump jump = GameFieldAccess.Jump(state.Enemy);
        jump.surfaceJumpForceUp *= multiplier;
        jump.surfaceJumpForceSide *= multiplier;
        jump.stuckJumpForceUp *= multiplier;
        jump.stuckJumpForceSide *= multiplier;
        jump.gapJumpForceUp *= multiplier;
        jump.gapJumpForceForward *= multiplier;
        runtime.StatsApplied = true;
    }

    internal static void Tick(EliteState state)
    {
    }

    internal static void Cleanup(EliteState state)
    {
        // Jump multipliers are applied once per enemy object and retained across
        // vanilla despawn/respawn. The entire state is discarded at level change.
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyGnome), "StateNoticeDelay")]
    private static bool StateNoticeDelayPrefix(EnemyGnome __instance)
    {
        if (!EliteEnemyVariantsPlugin.Instance.Settings.GnomeZeroNoticeDelay.Value ||
            !EliteRegistry.TryGet(__instance, out _))
        {
            return true;
        }
        UpdateGnomeState(__instance, EnemyGnome.State.Notice);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyGnome), "AttackPositionLogic")]
    private static bool AttackPositionLogicPrefix(EnemyGnome __instance, ref Vector3 __result)
    {
        if (!EliteRegistry.TryGet(__instance, out EliteState state) || EnemyGnomeDirector.instance == null)
        {
            return true;
        }
        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        float speed = config.GnomeAttackOrbitSpeed.Value;
        float radius = config.GnomeAttackRadius.Value;
        float angle = AttackAngle(__instance);
        angle += (DirectorIndex(__instance) % 2 == 0 ? 0f : Mathf.PI);
        __result = GameFieldAccess.GnomeAttackPosition(EnemyGnomeDirector.instance) +
                   new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        AttackAngle(__instance) += Time.deltaTime * speed;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyGnome), "StateAttackDone")]
    private static void StateAttackDonePostfix(EnemyGnome __instance)
    {
        if (EliteEnemyVariantsPlugin.Instance.Settings.GnomeZeroAttackCooldown.Value &&
            EliteRegistry.TryGet(__instance, out _))
        {
            GameFieldAccess.SetGnomeAttackCooldown(__instance, 0f);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyGnomeDirector), "CanAttack")]
    private static bool CanAttackPrefix(EnemyGnomeDirector __instance, EnemyGnome _gnome, ref bool __result)
    {
        if (!EliteRegistry.TryGet(_gnome, out EliteState state))
        {
            return true;
        }
        if (GameFieldAccess.GnomeAttackCooldown(_gnome) > 0f ||
            GameFieldAccess.IsJumping(GameFieldAccess.Jump(_gnome.enemy)))
        {
            __result = false;
            return false;
        }

        EliteEnemyConfig config = EliteEnemyVariantsPlugin.Instance.Settings;
        float radius = config.GnomeAttackRadius.Value;
        if (__instance.currentState == EnemyGnomeDirector.State.AttackPlayer)
        {
            int active = 0;
            foreach (EnemyGnome member in __instance.gnomes)
            {
                if (member != null && member != _gnome &&
                    member.currentState is EnemyGnome.State.AttackMove or EnemyGnome.State.Attack)
                {
                    active++;
                }
            }
            if (active >= config.GnomeConcurrentAttackers.Value)
            {
                __result = false;
                return false;
            }
            __result = Vector3.Distance(
                GameFieldAccess.Rigidbody(_gnome.enemy).transform.position,
                GameFieldAccess.GnomeAttackPosition(__instance)) <= radius;
            return false;
        }

        PhysGrabObject valuable = DirectorValuableTarget(__instance);
        if (valuable == null)
        {
            __result = false;
            return false;
        }
        OverlapCheckCooldown(_gnome) = 1f;
        if (OverlapCheckTimer(_gnome) <= 0f)
        {
            OverlapCheckTimer(_gnome) = 0.5f;
            OverlapCheckPrevious(_gnome) = false;
            foreach (Collider collider in Physics.OverlapSphere(
                         GameFieldAccess.Rigidbody(_gnome.enemy).transform.position,
                         radius,
                         LayerMask.GetMask("PhysGrabObject")))
            {
                ValuableObject? value = collider.GetComponentInParent<ValuableObject>();
                if (value != null && GameFieldAccess.ValuablePhysGrabObject(value) == valuable)
                {
                    OverlapCheckPrevious(_gnome) = true;
                    break;
                }
            }
        }
        __result = OverlapCheckPrevious(_gnome);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyGnomeDirector), "StateAttackSet")]
    private static bool StateAttackSetPrefix(EnemyGnomeDirector __instance)
    {
        if (!TryGetDirectorState(__instance, out _))
        {
            return true;
        }
        if (!DirectorStateImpulse(__instance))
        {
            return false;
        }
        DirectorStateImpulse(__instance) = false;
        DirectorValuableTarget(__instance) = null!;
        PlayerAvatar player = DirectorPlayerTarget(__instance);
        if (player == null)
        {
            UpdateDirectorState(__instance, EnemyGnomeDirector.State.Idle);
            return false;
        }

        float bestValue = float.MinValue;
        float radius = EliteEnemyVariantsPlugin.Instance.Settings.GnomeValuablePriorityRadius.Value;
        foreach (Collider collider in Physics.OverlapSphere(player.transform.position, radius, LayerMask.GetMask("PhysGrabObject")))
        {
            ValuableObject? value = collider.GetComponentInParent<ValuableObject>();
            PhysGrabObject? physGrab = value != null ? GameFieldAccess.ValuablePhysGrabObject(value) : null;
            PhysGrabObjectImpactDetector? impact = physGrab != null ? GameFieldAccess.ImpactDetector(physGrab) : null;
            if (value == null || physGrab == null || impact == null ||
                GameFieldAccess.IsIndestructible(impact) || GameFieldAccess.ValuableDollarValue(value) <= bestValue)
            {
                continue;
            }
            bestValue = GameFieldAccess.ValuableDollarValue(value);
            DirectorValuableTarget(__instance) = physGrab;
        }
        UpdateDirectorState(__instance,
            DirectorValuableTarget(__instance) != null
                ? EnemyGnomeDirector.State.AttackValuable
                : EnemyGnomeDirector.State.AttackPlayer);
        return false;
    }

    private static bool TryGetDirectorState(EnemyGnomeDirector director, out EliteState state)
    {
        foreach (EnemyGnome gnome in director.gnomes)
        {
            if (gnome != null && EliteRegistry.TryGet(gnome, out state))
            {
                return true;
            }
        }
        state = null!;
        return false;
    }

    private static GnomeRuntimeState GetRuntime(EliteState state)
    {
        if (state.AdapterState is not GnomeRuntimeState runtime)
        {
            runtime = new GnomeRuntimeState();
            state.AdapterState = runtime;
        }
        return runtime;
    }
}
