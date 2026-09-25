using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class GambitElitePatches
{
    private static readonly Action<EnemySpinny, EnemySpinny.State> UpdateState =
        AccessTools.MethodDelegate<Action<EnemySpinny, EnemySpinny.State>>(
            AccessTools.Method(typeof(EnemySpinny), "UpdateState"));
    private static readonly Action<EnemySpinny, bool> JumpIfStuck =
        AccessTools.MethodDelegate<Action<EnemySpinny, bool>>(
            AccessTools.Method(typeof(EnemySpinny), "JumpIfStuck"));
    private static readonly AccessTools.FieldRef<EnemySpinny, bool> ReachedPoint =
        AccessTools.FieldRefAccess<EnemySpinny, bool>("reachedPoint");

    internal static void OnSpawn(EliteState state)
    {
        EnemySpinny? gambit = state.Enemy.GetComponent<EnemySpinny>();
        if (gambit != null)
        {
            gambit.spinDurationSeconds = EliteEnemyVariantsPlugin.Instance.Settings.GambitRouletteDurationSeconds.Value;
        }
        state.AdapterState = new GambitRuntimeState { LastPosition = state.Enemy.transform.position };
    }

    internal static void Tick(EliteState state)
    {
        EnemySpinny? gambit = state.Enemy.GetComponent<EnemySpinny>();
        if (gambit == null)
        {
            return;
        }
        GambitRuntimeState runtime = GetRuntime(state);
        bool carryingPhase = gambit.currentState is EnemySpinny.State.Roulette or
            EnemySpinny.State.RouletteEndPause or EnemySpinny.State.RouletteEnd or
            EnemySpinny.State.RouletteEffect ||
            gambit.currentState == EnemySpinny.State.WaitForRoulette && ReachedPoint(gambit);
        if (!gambit.RouletteGoingOn())
        {
            runtime.EscapeActive = false;
            runtime.StuckTimer = 0f;
            runtime.EscapeAttempt = 0;
            runtime.LastPosition = gambit.transform.position;
            return;
        }
        // Do not move until the player has reached the mouth lock point. Once the
        // lock is stable, keep escaping throughout the complete roulette sequence.
        if (!carryingPhase)
        {
            return;
        }
        if (gambit.playerTarget == null || GameFieldAccess.PlayerDisabled(gambit.playerTarget))
        {
            UpdateState(gambit, EnemySpinny.State.CloseMouth);
            return;
        }

        if (!runtime.EscapeActive)
        {
            runtime.EscapeActive = TryChooseEscapePoint(
                gambit,
                runtime.EscapeAttempt++,
                out runtime.EscapePoint);
            runtime.LastPosition = gambit.transform.position;
        }
        if (!runtime.EscapeActive)
        {
            return;
        }

        EnemyNavMeshAgent nav = GameFieldAccess.NavMeshAgent(state.Enemy);
        nav.Stop(0f);
        nav.SetDestination(runtime.EscapePoint);
        nav.OverrideAgent(
            Mathf.Max(6f, GameFieldAccess.NavDefaultSpeed(nav)),
            Mathf.Max(20f, GameFieldAccess.NavDefaultAcceleration(nav)),
            0.2f);
        GameFieldAccess.Rigidbody(state.Enemy).OverrideFollowPosition(0.2f, 10f, 1f);

        float moved = Vector3.Distance(runtime.LastPosition, gambit.transform.position);
        runtime.StuckTimer = moved < 0.05f ? runtime.StuckTimer + Time.deltaTime : 0f;
        runtime.LastPosition = gambit.transform.position;
        if (runtime.StuckTimer >= 2f)
        {
            JumpIfStuck(gambit, true);
        }

        bool reached = Vector3.Distance(gambit.transform.position, runtime.EscapePoint) <= 1.25f;
        bool stalled = runtime.StuckTimer >= 2f;
        if (!reached && !stalled)
        {
            return;
        }

        // Reaching a point or becoming stuck starts another route instead of
        // ending the carry movement for the rest of the roulette.
        nav.ResetPath();
        nav.Stop(0f);
        runtime.EscapeActive = false;
        runtime.StuckTimer = 0f;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyStateStunned), nameof(EnemyStateStunned.Set))]
    private static bool EnemyStateStunnedSetPrefix(EnemyStateStunned __instance)
    {
        EnemySpinny? gambit = __instance.GetComponent<EnemySpinny>();
        if (gambit == null ||
            !EliteEnemyVariantsPlugin.Instance.Settings.GambitRouletteStunImmune.Value ||
            !gambit.RouletteGoingOn())
        {
            return true;
        }
        return !EliteRegistry.TryGet(gambit, out _);
    }

    private static bool TryChooseEscapePoint(EnemySpinny gambit, int index, out Vector3 result)
    {
        Vector3 away = gambit.transform.position - gambit.playerTarget.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = gambit.transform.forward;
        }
        away.Normalize();
        float sign = index % 2 == 0 ? 1f : -1f;
        float baseTurn = 25f + (index % 5) * 28f;
        NavMeshPath path = new();
        float bestLength = 0f;
        Vector3 best = default;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float yaw = sign * baseTurn + attempt * 36f;
            Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * away;
            Vector3 candidate = gambit.transform.position +
                                direction * (10f + attempt % 3 * 3f);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(gambit.transform.position, hit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2)
            {
                continue;
            }
            float length = 0f;
            for (int corner = 1; corner < path.corners.Length; corner++)
            {
                length += Vector3.Distance(path.corners[corner - 1], path.corners[corner]);
            }
            if (length > bestLength)
            {
                bestLength = length;
                best = hit.position;
            }
        }
        if (bestLength >= 2f)
        {
            result = best;
            return true;
        }
        result = default;
        return false;
    }

    private static GambitRuntimeState GetRuntime(EliteState state)
    {
        if (state.AdapterState is not GambitRuntimeState runtime)
        {
            runtime = new GambitRuntimeState { LastPosition = state.Enemy.transform.position };
            state.AdapterState = runtime;
        }
        return runtime;
    }
}
