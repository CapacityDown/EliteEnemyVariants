using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace REPOJP.EliteEnemyVariants;

internal sealed partial class TacticalRuntimeState
{
    internal float NextScanAt;
    internal bool HasLootDestination;
    internal Vector3 LootDestination;
    internal bool RescueEvaded;
    internal bool PreparationUsed;
    internal bool Repositioning;
    internal Vector3 RepositionPoint;
    internal float RepositionDeadline;
    internal PlayerAvatar? RepositionTarget;

    internal void BeginReposition(PlayerAvatar target, Vector3 point, float now)
    {
        PreparationUsed = true;
        Repositioning = true;
        RepositionTarget = target;
        RepositionPoint = point;
        RepositionDeadline = now + 2f;
    }
}

internal static partial class TacticalPositioning
{
    private static readonly AccessTools.FieldRef<PlayerAvatar, Vector3> PlayerVelocity =
        AccessTools.FieldRefAccess<PlayerAvatar, Vector3>("rbVelocity");
    private static readonly AccessTools.FieldRef<EnemyRigidbody, Rigidbody> PhysicsBody =
        AccessTools.FieldRefAccess<EnemyRigidbody, Rigidbody>("rb");

    internal static bool IsActive(Component instance, out EliteState state) =>
        EliteRegistry.TryGet(instance, out state) && SemiFunc.IsMasterClientOrSingleplayer() &&
        EliteEnemyVariantsPlugin.Instance.Settings.Enabled.Value && state.Spawned && !state.AdapterTickFaulted;

    private readonly struct Loot
    {
        internal readonly Vector3 Position;
        internal readonly float Value;
        internal Loot(Vector3 position, float value) { Position = position; Value = value; }
    }

    private static List<Loot> FindLoot(Vector3 origin, float radius)
    {
        List<Loot> result = new();
        HashSet<int> seen = new();
        foreach (Collider collider in Physics.OverlapSphere(origin, radius, LayerMask.GetMask("PhysGrabObject")))
        {
            ValuableObject? valuable = collider.GetComponentInParent<ValuableObject>();
            if (valuable == null || !valuable.gameObject.activeInHierarchy || !seen.Add(valuable.GetInstanceID())) continue;
            PhysGrabObject? phys = GameFieldAccess.ValuablePhysGrabObject(valuable);
            PhysGrabObjectImpactDetector? impact = phys != null ? GameFieldAccess.ImpactDetector(phys) : null;
            float value = GameFieldAccess.ValuableDollarValue(valuable);
            if (phys == null || impact == null || GameFieldAccess.IsIndestructible(impact) || value <= 0f ||
                Mathf.Abs(phys.transform.position.y - origin.y) > 3f) continue;
            result.Add(new Loot(phys.transform.position, value));
        }
        result.Sort((a, b) => (a.Position - origin).sqrMagnitude.CompareTo((b.Position - origin).sqrMagnitude));
        if (result.Count > 24) result.RemoveRange(24, result.Count - 24);
        return result;
    }

    internal static bool ClearLine(Vector3 from, Vector3 to, int mask = 0)
    {
        Vector3 delta = to - from;
        // Loot and players at the endpoint are not obstructions to themselves.
        if (mask == 0) mask = LayerMask.GetMask("Default", "PhysGrabObjectHinge", "StaticGrabObject");
        return delta.sqrMagnitude < 0.01f || !Physics.Raycast(from, delta.normalized, delta.magnitude,
            mask, QueryTriggerInteraction.Ignore);
    }

    internal static bool VisiblePlayer(Enemy enemy, PlayerAvatar? player) =>
        DedicatedBehaviorSupport.CanUseAsUnseenTarget(player) && ClearLine(enemy.CenterTransform.position,
            player!.PlayerVisionTarget?.VisionTransform?.position ?? player.transform.position + Vector3.up,
            SemiFunc.LayerMaskGetVisionObstruct() & ~LayerMask.GetMask("Player"));

    internal static bool TryPath(Vector3 from, Vector3 desired, float maxLength, out Vector3 point, out NavMeshPath path)
    {
        point = default;
        path = new NavMeshPath();
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, 3f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(desired, out NavMeshHit end, 2f, NavMesh.AllAreas) ||
            Mathf.Abs(end.position.y - desired.y) > 2f ||
            !Physics.Raycast(end.position + Vector3.up * 0.25f, Vector3.down, 1f, LayerMask.GetMask("Default")) ||
            !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete || PathLength(path) > maxLength) return false;
        point = end.position;
        return true;
    }

    private static float PathLength(NavMeshPath path)
    {
        float length = 0f;
        Vector3[] corners = path.corners;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }

    internal static bool AnimalDestination(EliteState state, PlayerAvatar? target, TacticalRuntimeState runtime, out Vector3 point)
    {
        point = default;
        if (!IsActive(state.Enemy, out _) || !DedicatedBehaviorSupport.IsValidTarget(target)) return false;
        if (Time.time >= runtime.NextScanAt)
        {
            runtime.NextScanAt = Time.time + 1f;
            runtime.HasLootDestination = false;
            Vector3 origin = state.Enemy.transform.position;
            List<Loot> loot = FindLoot(target!.transform.position, 12f);
            float bestScore = float.NegativeInfinity;
            foreach (Loot item in loot)
            {
                Vector3 approach = item.Position - origin;
                approach.y = 0f;
                if (approach.sqrMagnitude < 0.25f) approach = state.Enemy.transform.forward;
                Vector3 desired = item.Position + approach.normalized * 1.5f;
                desired.y = origin.y;
                if (!ClearLine(origin + Vector3.up, item.Position + Vector3.up * 0.25f) ||
                    !TryPath(origin, desired, 20f, out Vector3 candidate, out NavMeshPath path) ||
                    Vector3.Distance(candidate, origin) < 1f) continue;
                float score = -PathLength(path) * 0.15f;
                foreach (Loot neighbour in loot)
                {
                    if (Vector3.Distance(item.Position, neighbour.Position) <= 3.5f)
                        score += 3f + Mathf.Min(neighbour.Value / 2000f, 3f);
                }
                if (score <= bestScore) continue;
                bestScore = score;
                runtime.HasLootDestination = true;
                runtime.LootDestination = candidate;
            }
        }
        point = runtime.LootDestination;
        return runtime.HasLootDestination;
    }

    internal static bool EvadeRescuer(EliteState state, PlayerAvatar? captive, Vector3 destination,
        TacticalRuntimeState runtime, out Vector3 point)
    {
        point = default;
        return DedicatedBehaviorSupport.IsValidTarget(captive) &&
            EvadePursuer(state, destination, runtime, out point, captive);
    }

    internal static bool EvadePursuer(EliteState state, Vector3 destination,
        TacticalRuntimeState runtime, out Vector3 point, PlayerAvatar? excluded = null)
    {
        point = default;
        if (!IsActive(state.Enemy, out _) || runtime.RescueEvaded || Time.time < runtime.NextScanAt) return false;
        runtime.NextScanAt = Time.time + 0.5f;
        Vector3 origin = state.Enemy.transform.position;
        NavMeshPath currentPath = GameFieldAccess.NavMeshAgent(state.Enemy).CalculatePath(destination);
        if (currentPath.status != NavMeshPathStatus.PathComplete || currentPath.corners.Length < 2) return false;
        List<Vector3> rescuers = new();
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (player == excluded || !VisiblePlayer(state.Enemy, player)) continue;
            float distance = Vector3.Distance(origin, player.transform.position);
            if (distance < 2f || distance > 8f ||
                Vector3.Dot((currentPath.corners[1] - origin).normalized, (player.transform.position - origin).normalized) < 0.2f ||
                PathClearance(currentPath, player.transform.position, 10f) > 2.5f) continue;
            rescuers.Add(player.transform.position);
        }
        if (rescuers.Count == 0) return false;
        List<LevelPoint> points = SemiFunc.LevelPointGetWithinDistance(origin, 6f, 18f);
        if (points == null) return false;
        float best = float.NegativeInfinity;
        int examined = 0;
        foreach (LevelPoint levelPoint in points)
        {
            if (levelPoint == null) continue;
            if (++examined > 32) break;
            if (!TryPath(origin, levelPoint.transform.position, 25f, out Vector3 candidate, out NavMeshPath path)) continue;
            float clearance = float.PositiveInfinity;
            float endDistance = float.PositiveInfinity;
            foreach (Vector3 rescuer in rescuers)
            {
                clearance = Mathf.Min(clearance, PathClearance(path, rescuer, 10f));
                endDistance = Mathf.Min(endDistance, Vector3.Distance(candidate, rescuer));
            }
            if (clearance < 2.5f || endDistance < 6f) continue;
            float score = clearance * 2f + endDistance - PathLength(path) * 0.25f;
            if (score <= best) continue;
            best = score;
            point = candidate;
        }
        if (float.IsNegativeInfinity(best)) return false;
        runtime.RescueEvaded = true;
        return true;
    }

    private static float PathClearance(NavMeshPath path, Vector3 obstacle, float limit)
    {
        float clearance = float.PositiveInfinity;
        float travelled = 0f;
        Vector3[] corners = path.corners;
        for (int i = 1; i < corners.Length && travelled < limit; i++)
        {
            Vector3 start = corners[i - 1];
            Vector3 delta = Vector3.ClampMagnitude(corners[i] - start, limit - travelled);
            float projection = delta.sqrMagnitude > 0.001f ? Mathf.Clamp01(Vector3.Dot(obstacle - start, delta) / delta.sqrMagnitude) : 0f;
            clearance = Mathf.Min(clearance, Vector3.Distance(obstacle, start + delta * projection));
            travelled += delta.magnitude;
        }
        return clearance;
    }

    internal static bool MentalistPosition(EliteState state, EnemyFloater floater, PlayerAvatar? target, out Vector3 point)
    {
        point = default;
        if (!IsActive(state.Enemy, out _) || !VisiblePlayer(state.Enemy, target)) return false;
        Vector3 origin = floater.feetTransform.position;
        List<Vector3> players = new();
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
            if (VisiblePlayer(state.Enemy, player) && Vector3.Distance(origin, player.transform.position) <= 8f)
                players.Add(player.transform.position);
        List<Loot> loot = FindLoot(origin, 8f);
        List<Vector3> candidates = new();
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            candidates.Add(origin + direction * 1.5f);
            candidates.Add(origin + direction * 3f);
        }
        float initial = MentalistScore(origin, players, loot);
        float best = initial + 0.75f;
        foreach (Vector3 desired in candidates)
        {
            if (!TryPath(origin, desired, 5f, out Vector3 candidate, out NavMeshPath path) ||
                Vector3.Distance(candidate, origin) < 0.75f || Vector3.Distance(candidate, target!.transform.position) > 3.5f ||
                !ClearLine(candidate + Vector3.up, target.transform.position + Vector3.up)) continue;
            float score = MentalistScore(candidate, players, loot) - PathLength(path) * 0.1f;
            if (score <= best) continue;
            best = score;
            point = candidate;
        }
        return best > initial + 0.75f;
    }

    private static float MentalistScore(Vector3 point, List<Vector3> players, List<Loot> loot)
    {
        float score = 0f;
        foreach (Vector3 player in players)
            if (Vector3.Distance(point, player) <= 3.5f && ClearLine(point + Vector3.up, player + Vector3.up)) score += 5f;
        foreach (Loot item in loot)
            if (Vector3.Distance(point, item.Position) <= 3.5f && ClearLine(point + Vector3.up, item.Position + Vector3.up * 0.25f))
                score += 1f + Mathf.Min(item.Value / 4000f, 1f);
        return score;
    }

    internal static bool LoomPosition(EliteState state, PlayerAvatar target, out Vector3 point)
    {
        point = default;
        if (!IsActive(state.Enemy, out _) || !DedicatedBehaviorSupport.IsValidTarget(target)) return false;
        Vector3 targetPosition = target.transform.position;
        Vector3 direction = PlayerVelocity(target);
        direction.y = 0f;
        bool moving = direction.sqrMagnitude >= 0.25f;
        direction = moving ? direction.normalized : Vector3.zero;
        List<LevelPoint> points = SemiFunc.LevelPointGetWithinDistance(targetPosition, 8f, 28f);
        if (points == null) return false;
        float best = float.NegativeInfinity;
        int examined = 0;
        foreach (LevelPoint levelPoint in points)
        {
            if (levelPoint == null) continue;
            if (++examined > 48) break;
            if (!TryPath(targetPosition, levelPoint.transform.position, 45f, out Vector3 candidate, out _) ||
                Vector3.Distance(candidate, targetPosition) < 8f || Vector3.Distance(candidate, targetPosition) > 30f ||
                SemiFunc.EnemyPhysObjectBoundingBoxCheck(state.Enemy.transform.position, candidate,
                    PhysicsBody(GameFieldAccess.Rigidbody(state.Enemy)))) continue;
            bool tooClose = false;
            foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
                if (DedicatedBehaviorSupport.IsValidTarget(player) && Vector3.Distance(candidate, player.transform.position) < 8f)
                { tooClose = true; break; }
            if (tooClose) continue;
            Vector3 offset = candidate - targetPosition;
            offset.y = 0f;
            float ahead = moving ? Vector3.Dot(direction, offset.normalized) : 0f;
            float score = ahead * 12f - Mathf.Abs(offset.magnitude - 16f) * 0.2f;
            if (score <= best) continue;
            best = score;
            point = candidate;
        }
        return !float.IsNegativeInfinity(best);
    }
}
