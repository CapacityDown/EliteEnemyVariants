using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace REPOJP.EliteEnemyVariants;

internal sealed partial class TacticalRuntimeState
{
    // Each enemy owns one instance; DedicatedBehaviorSupport replaces it on a real spawn.
    internal bool FlankAttempted;
    internal bool FlankMoving;
    internal Vector3 FlankPoint;
    internal float FlankDeadline;
    internal PlayerDeathHead? RecoveryHead;
    internal int RecoveryHeadId;
    internal bool RecoveringHead;
    internal Vector3 DroppedHeadPosition;
    internal float RecoveryDeadline;
    internal bool HasSeenPlayer;
    internal Vector3 SeenPosition;
    internal Vector3 SeenDirection;
    internal float SeenAt;
    internal bool SweepUsed;
    internal bool Sweeping;
    internal Vector3 SweepPoint;

    internal void ResetFlank() { FlankAttempted = false; FlankMoving = false; }
    internal void BeginFlank(Vector3 point)
    {
        FlankMoving = true;
        FlankPoint = point;
        FlankDeadline = Time.time + 2f;
    }
    internal bool ContinueFlank(Vector3 origin)
    {
        if (Time.time >= FlankDeadline || Vector3.Distance(origin, FlankPoint) <= 0.8f) FlankMoving = false;
        return FlankMoving;
    }
    internal bool RememberDroppedHead(PlayerDeathHead head, Vector3 point)
    {
        if (RecoveryHeadId == head.GetInstanceID()) return false;
        RecoveryHeadId = head.GetInstanceID();
        RecoveryHead = head;
        DroppedHeadPosition = point;
        RecoveringHead = false;
        RecoveryDeadline = Time.time + 30f;
        return true;
    }
}

internal static partial class TacticalPositioning
{
    private static readonly AccessTools.FieldRef<PlayerDeathHead, PhysGrabObject> DeathHeadPhysics =
        AccessTools.FieldRefAccess<PlayerDeathHead, PhysGrabObject>("physGrabObject");
    private static readonly AccessTools.FieldRef<PlayerDeathHead, bool> DeathHeadSpectated =
        AccessTools.FieldRefAccess<PlayerDeathHead, bool>("overrideSpectated");

    internal static bool RecoverableHead(TacticalRuntimeState runtime)
    {
        PlayerDeathHead? head = runtime.RecoveryHead;
        if (head == null || !head.gameObject.activeInHierarchy || head.playerAvatar == null ||
            !GameFieldAccess.PlayerDisabled(head.playerAvatar) || Time.time >= runtime.RecoveryDeadline ||
            DeathHeadSpectated(head) || Vector3.Distance(head.transform.position, runtime.DroppedHeadPosition) > 4f) return false;
        PhysGrabObject? phys = DeathHeadPhysics(head);
        return phys != null && phys.playerGrabbing.Count == 0;
    }

    internal static bool ThrowingFlank(EliteState state, PlayerAvatar? target, out Vector3 point)
    {
        point = default;
        if (!IsActive(state.Enemy, out _) || !DedicatedBehaviorSupport.CanUseAsUnseenTarget(target)) return false;
        Vector3 origin = state.Enemy.transform.position;
        Vector3 aim = target!.transform.position;
        if (ClearLine(origin + Vector3.up, aim + Vector3.up)) return false;
        Vector3 forward = aim - origin;
        forward.y = 0f;
        if (forward.magnitude > 12f || forward.magnitude < 1f) return false;
        forward = forward.normalized;
        Vector3 side = new(forward.z, 0f, -forward.x);
        float best = float.PositiveInfinity;
        foreach (float sign in new[] { -1f, 1f })
        foreach (float lateral in new[] { 2f, 3.5f })
        {
            Vector3 desired = origin + side * (sign * lateral) + forward * 1.5f;
            if (!TryPath(origin, desired, 7f, out Vector3 candidate, out NavMeshPath path) ||
                Vector3.Distance(candidate, origin) < 1f || Vector3.Distance(candidate, aim) < 3f ||
                Vector3.Distance(candidate, aim) > 8f || !ClearLine(candidate + Vector3.up, aim + Vector3.up)) continue;
            float score = PathLength(path) + Mathf.Abs(Vector3.Distance(candidate, aim) - 4f);
            if (score >= best) continue;
            best = score;
            point = candidate;
        }
        return !float.IsPositiveInfinity(best);
    }

    internal static bool BombPincer(EliteState state, PlayerAvatar? target, Vector3 bomb, out Vector3 point)
    {
        point = default;
        if (!IsActive(state.Enemy, out _) || !VisiblePlayer(state.Enemy, target)) return false;
        Vector3 origin = state.Enemy.transform.position;
        Vector3 center = target!.transform.position;
        Vector3 bombSide = bomb - center;
        bombSide.y = 0f;
        if (bombSide.sqrMagnitude < 1f || Vector3.Distance(origin, bomb) < 6f) return false;
        float currentSide = Vector3.Dot((origin - center).normalized, bombSide.normalized);
        float best = float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            Vector3 desired = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 5f;
            desired.y = origin.y;
            if (!TryPath(origin, desired, 9f, out Vector3 candidate, out NavMeshPath path) ||
                Vector3.Distance(candidate, origin) < 1f || Vector3.Distance(candidate, center) < 4f ||
                Vector3.Distance(candidate, center) > 7f || PathClearance(path, bomb, 10f) < 6f ||
                !ClearLine(candidate + Vector3.up, center + Vector3.up)) continue;
            float side = Vector3.Dot((candidate - center).normalized, bombSide.normalized);
            if (side > -0.2f || side > currentSide - 0.15f) continue;
            float score = -side * 5f - PathLength(path) * 0.25f;
            if (score <= best) continue;
            best = score;
            point = candidate;
        }
        return !float.IsNegativeInfinity(best);
    }

    internal static bool SafeBombRoute(EliteState state, Vector3 bomb, Vector3 destination) =>
        TryPath(state.Enemy.transform.position, destination, 9f, out _, out NavMeshPath path) &&
        PathClearance(path, bomb, 10f) >= 6f;

    internal static void RememberSeenPlayer(TacticalRuntimeState runtime, PlayerAvatar target)
    {
        runtime.HasSeenPlayer = true;
        runtime.SeenPosition = target.transform.position;
        Vector3 direction = PlayerVelocity(target);
        direction.y = 0f;
        runtime.SeenDirection = direction.sqrMagnitude >= 0.25f ? direction.normalized : Vector3.zero;
        runtime.SeenAt = Time.time;
    }

    internal static bool AmbushExit(EliteState state, TacticalRuntimeState runtime, List<LevelPoint> candidates,
        out LevelPoint? chosen)
    {
        chosen = null;
        if (!IsActive(state.Enemy, out _) || !runtime.HasSeenPlayer || Time.time - runtime.SeenAt > 12f) return false;
        float best = float.NegativeInfinity;
        int examined = 0;
        // Only rank the vanilla, obstruction-purged candidates. Do not bypass emergence or lure states.
        foreach (LevelPoint candidate in candidates)
        {
            if (candidate == null || ++examined > 48) continue;
            bool exit = candidate.ModuleConnect;
            if (!exit && candidate.ConnectedPoints != null)
                foreach (LevelPoint adjacent in candidate.ConnectedPoints)
                    if (adjacent != null && candidate.Room != null && adjacent.Room != null && candidate.Room != adjacent.Room)
                    { exit = true; break; }
            if (!exit) continue;
            Vector3 position = candidate.transform.position;
            float distance = Vector3.Distance(position, runtime.SeenPosition);
            if (distance < 6f || distance > 20f ||
                !TryPath(runtime.SeenPosition, position, 25f, out _, out _)) continue;
            bool occupied = false;
            foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
                if (DedicatedBehaviorSupport.IsValidTarget(player) && Vector3.Distance(position, player.transform.position) < 4f)
                { occupied = true; break; }
            if (occupied) continue;
            float score = Vector3.Dot((position - runtime.SeenPosition).normalized, runtime.SeenDirection) * 8f - distance * 0.2f;
            if (score <= best) continue;
            best = score;
            chosen = candidate;
        }
        return chosen != null;
    }

    internal static bool SweepCorner(EliteState state, Vector3 lastSeen, out Vector3 point)
    {
        point = default;
        if (!IsActive(state.Enemy, out _)) return false;
        float best = float.PositiveInfinity;
        // Probe around the remembered point, never the current position of an unseen player.
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI / 6f;
            Vector3 desired = lastSeen + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3f;
            if (!TryPath(state.Enemy.transform.position, desired, 6f, out Vector3 candidate, out NavMeshPath path) ||
                Vector3.Distance(candidate, lastSeen) < 1.5f || Vector3.Distance(candidate, lastSeen) > 4f ||
                ClearLine(lastSeen + Vector3.up, candidate + Vector3.up)) continue;
            float length = PathLength(path);
            if (length >= best) continue;
            best = length;
            point = candidate;
        }
        return !float.IsPositiveInfinity(best);
    }
}
