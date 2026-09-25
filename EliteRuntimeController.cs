using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal sealed class EliteRuntimeController : MonoBehaviour
{
    private EliteEnemyConfig _config = null!;

    internal void Initialize(EliteEnemyConfig config)
    {
        _config = config;
    }

    internal void Shutdown()
    {
        EliteRegistry.ClearStage();
    }

    private void Update()
    {
        TestSpawnCommands.Tick();
        if (!_config.Enabled.Value || !SemiFunc.IsMasterClientOrSingleplayer())
        {
            return;
        }

        IReadOnlyList<EliteState> states = EliteRegistry.States;
        foreach (EliteState state in states)
        {
            if (state.Enemy == null)
            {
                continue;
            }
            if (state.Spawned)
            {
                if (!state.AdapterTickFaulted && EnemyAdapterRuntime.HasRuntimeTick(state.Archetype))
                {
                    try
                    {
                        EnemyAdapterRuntime.Tick(state);
                        EliteRegistry.RecordAdapterTickSuccess(state);
                    }
                    catch (Exception exception)
                    {
                        EliteRegistry.RecordAdapterTickFailure(state, exception, "runtime Tick");
                    }
                }
                TickPulse(state);
            }
        }
    }

    private void TickPulse(EliteState state)
    {
        if (!_config.EnableVanillaPulse.Value || !state.IsEnhanced || state.Enemy == null ||
            !GameFieldAccess.HasHealth(state.Enemy))
        {
            return;
        }
        EnemyHealth health = GameFieldAccess.Health(state.Enemy);
        EnemyParent parent = GameFieldAccess.Parent(state.Enemy);
        if (health == null || GameFieldAccess.IsDead(health) || parent == null ||
            !GameFieldAccess.ParentSpawned(parent))
        {
            return;
        }

        float blockedUntil = Mathf.Max(
            state.LastHurtAt + _config.DelayAfterHurtSeconds.Value,
            state.LastRealHealAt + _config.DelayAfterRealHealSeconds.Value);
        if (Time.time < blockedUntil)
        {
            state.NextPulseAt = Mathf.Max(state.NextPulseAt, blockedUntil);
            state.NextPulseCycleAt = Mathf.Max(state.NextPulseCycleAt, blockedUntil);
            return;
        }
        if (!state.EncounterPulseTriggered && IsPlayerWithinRange(state.Enemy, 12f))
        {
            state.EncounterPulseTriggered = true;
            state.EncounterPulseRemaining = 3;
            state.NextPulseAt = Time.time + EncounterJitter(state);
        }
        if (Time.time < state.NextPulseAt)
        {
            return;
        }

        SendHealFlash(state.Enemy);
        if (state.EncounterPulseRemaining > 0)
        {
            state.EncounterPulseRemaining--;
            if (state.EncounterPulseRemaining > 0)
            {
                state.NextPulseAt = Time.time + 0.18f;
                return;
            }

            state.PulsePart = 0;
            state.NextPulseCycleAt = Time.time + _config.PulseCycleSeconds.Value;
            state.NextPulseAt = state.NextPulseCycleAt;
            return;
        }

        const int pulseCount = 2;
        float gap = _config.PulseGapSeconds.Value;
        float cycle = _config.PulseCycleSeconds.Value;
        state.PulsePart++;
        if (state.PulsePart < pulseCount)
        {
            state.NextPulseAt = Time.time + gap;
            return;
        }

        state.PulsePart = 0;
        while (state.NextPulseCycleAt <= Time.time)
        {
            state.NextPulseCycleAt += cycle;
        }
        state.NextPulseAt = state.NextPulseCycleAt;
        state.NextPulseCycleAt += cycle;
    }

    private static void SendHealFlash(Enemy enemy)
    {
        if (GameManager.Multiplayer())
        {
            GameFieldAccess.PhotonView(enemy).RPC(nameof(EnemyHealth.HealRPC), RpcTarget.All);
        }
        else
        {
            GameFieldAccess.Health(enemy).HealRPC();
        }
    }

    private static bool IsPlayerWithinRange(Enemy enemy, float range)
    {
        float rangeSquared = range * range;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (player == null || GameFieldAccess.PlayerDisabled(player))
            {
                continue;
            }
            Vector3 target = player.PlayerVisionTarget?.VisionTransform?.position ?? player.transform.position;
            if ((target - enemy.transform.position).sqrMagnitude <= rangeSquared)
            {
                return true;
            }
        }
        return false;
    }

    private static float EncounterJitter(EliteState state)
    {
        PhotonView view = GameFieldAccess.PhotonView(state.Enemy);
        int key = view != null && view.ViewID != 0
            ? view.ViewID
            : state.Enemy.GetInstanceID();
        return (unchecked((uint)key) * 2654435761u % 251u) / 1000f;
    }
}
