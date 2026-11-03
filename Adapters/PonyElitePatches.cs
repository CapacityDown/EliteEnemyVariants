using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace REPOJP.EliteEnemyVariants;

// All decision patches run on the host. Native state setters retain the tongue
// handshake, player ownership, telegraphs, damage and spectator/client animation.
[HarmonyPatch]
internal static partial class PonyElitePatches
{
    private sealed class Runtime
    {
        internal readonly PonyPolicy Policy = new(Time.time, UnityEngine.Random.value);
        internal bool WasOnCeiling;
        internal bool Patrol;
        internal float PatrolEndsAt;
        internal bool Intercept;
        internal PlayerAvatar? InterceptTarget;
        internal Vector3 InterceptPoint;
        internal float InterceptEndsAt;
        internal float NextInterceptScan;
        internal float NextPathCheck;
        internal float NextLandingCheck;
        internal float LandingPauseUntil;
        internal bool LandingPending;
        internal float NextCoverScan;
        internal bool FlankLocked;
        internal Vector3 FlankPoint;
        internal Vector3 FlankTargetPoint;
        internal PlayerAvatar? FlankTarget;
        internal float NextCaptureScan;
        internal readonly PonyThrowCache Throw = new();
        internal readonly System.Collections.Generic.List<Collider> ThrowColliders = new();
        internal readonly Collider[] ThrowOverlaps = new Collider[64];
        internal readonly RaycastHit[] ThrowHits = new RaycastHit[64];
    }

    private delegate bool BlockingObject(EnemySnatcher pony, Vector3 origin, Vector3 target, out PhysGrabObject item);
    private delegate bool Landing(EnemySnatcher pony, PlayerAvatar player, out Vector3 point);
    private static T Bind<T>(string name) where T : Delegate =>
        AccessTools.MethodDelegate<T>(AccessTools.Method(typeof(EnemySnatcher), name));
    private static readonly Func<EnemySnatcher, bool> CeilingActive = Bind<Func<EnemySnatcher, bool>>("get_CeilingActive");
    private static readonly Func<EnemySnatcher, bool> CeilingAttached = Bind<Func<EnemySnatcher, bool>>("get_CeilingAttached");
    private static readonly Func<EnemySnatcher, bool> CeilingStayRequired = Bind<Func<EnemySnatcher, bool>>("CeilingStayRequired");
    private static readonly Func<EnemySnatcher, bool> CeilingJumpReady = Bind<Func<EnemySnatcher, bool>>("CeilingJumpReady");
    private static readonly Action<EnemySnatcher, bool> CeilingJumpTry = Bind<Action<EnemySnatcher, bool>>("CeilingJumpTry");
    private static readonly Action<EnemySnatcher> CeilingDrop = Bind<Action<EnemySnatcher>>("CeilingDrop");
    private static readonly Action<EnemySnatcher> DropVoice = Bind<Action<EnemySnatcher>>("CeilingScareVoicePlay");
    private static readonly Func<EnemySnatcher, Vector3, bool> CeilingPathClear = Bind<Func<EnemySnatcher, Vector3, bool>>("CeilingWanderPathClear");
    private static readonly Landing ScareLanding = Bind<Landing>("CeilingScareLanding");
    private static readonly Func<EnemySnatcher, PlayerAvatar, bool> Visible = Bind<Func<EnemySnatcher, PlayerAvatar, bool>>("PlayerVisible");
    private static readonly Func<EnemySnatcher, PlayerAvatar, bool> PlayerCooldown = Bind<Func<EnemySnatcher, PlayerAvatar, bool>>("PlayerAttackCooldownActive");
    private static readonly Func<EnemySnatcher, bool> AnyPlayerCooldown = Bind<Func<EnemySnatcher, bool>>("PlayerAttackCooldownAnyActive");
    private static readonly Action<EnemySnatcher, PlayerAvatar> SetTarget = Bind<Action<EnemySnatcher, PlayerAvatar>>("SetPlayerTarget");
    private static readonly Func<EnemySnatcher, Transform> TongueOrigin = Bind<Func<EnemySnatcher, Transform>>("TongueOriginTransform");
    private static readonly BlockingObject FindBlockingObject = Bind<BlockingObject>("TryGetTongueBlockingPhysObject");
    private static readonly Func<EnemySnatcher, PhysGrabObject, EnemySnatcher.State, bool> GrabObject =
        Bind<Func<EnemySnatcher, PhysGrabObject, EnemySnatcher.State, bool>>("SetStateGrabPhysObject");
    private static readonly Func<EnemySnatcher, bool> LookUnder = Bind<Func<EnemySnatcher, bool>>("LookUnderNavigationActive");

    private static readonly AccessTools.FieldRef<EnemySnatcher, PlayerAvatar> Target = AccessTools.FieldRefAccess<EnemySnatcher, PlayerAvatar>("playerTarget");
    private static readonly AccessTools.FieldRef<EnemySnatcher, bool> Impulse = AccessTools.FieldRefAccess<EnemySnatcher, bool>("stateImpulse");
    private static readonly AccessTools.FieldRef<EnemySnatcher, Vector3> Destination = AccessTools.FieldRefAccess<EnemySnatcher, Vector3>("agentDestination");
    private static readonly AccessTools.FieldRef<EnemySnatcher, Vector3> FlankStart = AccessTools.FieldRefAccess<EnemySnatcher, Vector3>("tongueRepositionStartPosition");
    private static readonly AccessTools.FieldRef<EnemySnatcher, Vector3> FlankPlayerPosition = AccessTools.FieldRefAccess<EnemySnatcher, Vector3>("tongueRepositionPlayerPosition");
    private static readonly AccessTools.FieldRef<EnemySnatcher, float> FlankCacheTime = AccessTools.FieldRefAccess<EnemySnatcher, float>("tongueRepositionDestinationTimer");
    private static readonly AccessTools.FieldRef<EnemySnatcher, float> AttackPause = AccessTools.FieldRefAccess<EnemySnatcher, float>("attackPauseTimer");
    private static readonly AccessTools.FieldRef<EnemySnatcher, float> AttackCooldown = AccessTools.FieldRefAccess<EnemySnatcher, float>("mainAttackCooldownTimer");
    private static readonly AccessTools.FieldRef<EnemySnatcher, float> Recovery = AccessTools.FieldRefAccess<EnemySnatcher, float>("tongueAttackRecoveryTimer");
    private static readonly AccessTools.FieldRef<EnemySnatcher, float> CeilingCooldown = AccessTools.FieldRefAccess<EnemySnatcher, float>("ceilingCooldownTimer");
    private static readonly AccessTools.FieldRef<PhysGrabber, PhysGrabObject> HeldObject = AccessTools.FieldRefAccess<PhysGrabber, PhysGrabObject>("grabbedPhysGrabObject");
    private static readonly AccessTools.FieldRef<PlayerAvatar, PhysGrabber> Grabber = AccessTools.FieldRefAccess<PlayerAvatar, PhysGrabber>("physGrabber");
    private static readonly AccessTools.FieldRef<PlayerAvatar, Vector3> Velocity = AccessTools.FieldRefAccess<PlayerAvatar, Vector3>("rbVelocity");
    private static readonly AccessTools.FieldRef<PlayerAvatar, Vector3> LastNavmesh = AccessTools.FieldRefAccess<PlayerAvatar, Vector3>("LastNavmeshPosition");
    private static readonly AccessTools.FieldRef<Enemy, float> Teleported = AccessTools.FieldRefAccess<Enemy, float>("TeleportedTimer");
    private static readonly AccessTools.FieldRef<EnemyRigidbody, bool> Grabbed = AccessTools.FieldRefAccess<EnemyRigidbody, bool>("grabbed");
    private static readonly AccessTools.FieldRef<EnemyRigidbody, bool> Frozen = AccessTools.FieldRefAccess<EnemyRigidbody, bool>("frozen");
    private static readonly AccessTools.FieldRef<EnemyNavMeshAgent, NavMeshAgent> Agent = AccessTools.FieldRefAccess<EnemyNavMeshAgent, NavMeshAgent>("Agent");
    private static readonly AccessTools.FieldRef<PhysGrabObject, bool> IsPlayer = AccessTools.FieldRefAccess<PhysGrabObject, bool>("isPlayer");
    private static readonly AccessTools.FieldRef<PhysGrabObject, bool> IsEnemy = AccessTools.FieldRefAccess<PhysGrabObject, bool>("isEnemy");
    private static readonly FieldInfo TongueCommand = AccessTools.Field(typeof(EnemySnatcher), "tongueCommandState");
    private static readonly FieldInfo TongueTarget = AccessTools.Field(typeof(EnemySnatcher), "tongueTargetType");
    private static readonly Type RetryType = AccessTools.Inner(typeof(EnemySnatcher), "TongueRetry");
    private static readonly FieldInfo RetryFirst = AccessTools.Field(RetryType, "firstOffset");
    private static readonly FieldInfo RetryLast = AccessTools.Field(RetryType, "lastOffset");

    internal static void OnSpawn(EliteState state) => state.AdapterState = new Runtime();

    private static bool Try(EnemySnatcher pony, out EliteState state, out Runtime runtime)
    {
        state = null!; runtime = null!;
        if (!SemiFunc.IsMasterClientOrSingleplayer() || EliteEnemyVariantsPlugin.Instance == null ||
            !EliteEnemyVariantsPlugin.Instance.Settings.Enabled.Value ||
            !EliteRegistry.TryGet(pony, out state) || state.Archetype != EliteArchetype.Pony ||
            !state.Spawned || state.AdapterTickFaulted || state.AdapterState is not Runtime found) return false;
        runtime = found;
        return true;
    }

    private static bool Ready(EliteState state) => state.Enemy.isActiveAndEnabled &&
        !state.Enemy.IsStunned() && state.Enemy.FreezeTimer <= 0f && Teleported(state.Enemy) <= 0f &&
        GameFieldAccess.Rigidbody(state.Enemy) != null && !Grabbed(GameFieldAccess.Rigidbody(state.Enemy)) &&
        !Frozen(GameFieldAccess.Rigidbody(state.Enemy)) &&
        !GameFieldAccess.PhysicsBody(GameFieldAccess.Rigidbody(state.Enemy)).isKinematic;

    private static bool TongueFree(EnemySnatcher pony) =>
        Convert.ToInt32(TongueCommand.GetValue(pony)) == 0 && Convert.ToInt32(TongueTarget.GetValue(pony)) == 0;

    private static bool TargetVisible(EnemySnatcher pony, PlayerAvatar? player) =>
        DedicatedBehaviorSupport.CanUseAsUnseenTarget(player) && Visible(pony, player!);

    private static PhysGrabObject? Held(PlayerAvatar player)
    {
        PhysGrabber grabber = Grabber(player);
        return grabber != null && grabber.grabbed ? HeldObject(grabber) : null;
    }

    private static bool Armed(PlayerAvatar player)
    {
        PhysGrabObject? held = Held(player);
        return held != null && held.gameObject.activeInHierarchy &&
            (held.GetComponentInChildren<ItemMelee>(true) != null || held.GetComponentInChildren<ItemGun>(true) != null);
    }

    private static void Fault(EliteState state, Exception exception) =>
        EliteRegistry.RecordAdapterTickFailure(state, exception, "Pony decision");

    // Selection opportunities only: never redirect a tongue, capture, kick or throw.
    private static bool PreferArmed(EnemySnatcher pony, EliteState state, PlayerAvatar? ignored)
    {
        if (!Ready(state) || CeilingActive(pony) || !TongueFree(pony) ||
            pony.currentState is not (EnemySnatcher.State.Notice or EnemySnatcher.State.Retarget or
                EnemySnatcher.State.Chase or EnemySnatcher.State.Search or EnemySnatcher.State.Roam or
                EnemySnatcher.State.Idle or EnemySnatcher.State.Frustrated)) return false;
        PlayerAvatar? best = null;
        float nearest = float.PositiveInfinity;
        foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
        {
            if (player == null || player == ignored || !TargetVisible(pony, player) ||
                !PonyPolicy.CanPreferArmed(true, true, Armed(player), PlayerCooldown(pony, player))) continue;
            float distance = (player.transform.position - state.Enemy.transform.position).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance; best = player;
        }
        if (best == null) return false;
        if (best != Target(pony)) SetTarget(pony, best);
        return Target(pony) == best;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(EnemySnatcher), "StateNotice")]
    private static void NoticePrefix(EnemySnatcher __instance)
    {
        if (!Try(__instance, out EliteState state, out _) || !Impulse(__instance)) return;
        try { PreferArmed(__instance, state, null); }
        catch (Exception e) { Fault(state, e); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(EnemySnatcher), "FindClosestVisiblePlayerTarget")]
    private static void SelectionPostfix(EnemySnatcher __instance, PlayerAvatar _ignoredPlayer, ref bool __result)
    {
        if (!Try(__instance, out EliteState state, out _)) return;
        try
        {
            // This method is also called while completing an attack. A live tongue
            // blocks our override even when vanilla is considering its next target.
            if (PreferArmed(__instance, state, _ignoredPlayer)) __result = true;
        }
        catch (Exception e) { Fault(state, e); }
    }

    private static bool Nav(EliteState state, out EnemyNavMeshAgent nav, out NavMeshAgent agent)
    {
        nav = GameFieldAccess.NavMeshAgent(state.Enemy);
        agent = nav != null ? Agent(nav) : null!;
        return agent != null && agent.enabled && agent.isOnNavMesh && !agent.isOnOffMeshLink;
    }

    private static bool ClearTongueLine(Vector3 origin, Vector3 target, PlayerAvatar player)
    {
        Vector3 delta = target - origin;
        if (delta.sqrMagnitude < 0.01f) return false;
        if (!Physics.Linecast(origin, target, out RaycastHit hit, SemiFunc.LayerMaskGetShouldHits(), QueryTriggerInteraction.Ignore)) return true;
        return hit.collider != null && hit.collider.GetComponentInParent<PlayerAvatar>() == player;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(EnemySnatcher), "TongueRepositionDestinationPick")]
    private static bool RepositionPrefix(EnemySnatcher __instance, object _retry, ref bool __result)
    {
        if (!Try(__instance, out EliteState state, out Runtime runtime)) return true;
        try
        {
            PlayerAvatar player = Target(__instance);
            if (!Ready(state) || CeilingActive(__instance) || LookUnder(__instance) ||
                !TargetVisible(__instance, player) || !Nav(state, out _, out NavMeshAgent agent)) return true;
            Vector3 center = LastNavmesh(player);
            Vector3 body = GameFieldAccess.Rigidbody(state.Enemy).transform.position;
            Vector3 offset = Vector3.ProjectOnPlane(body - center, Vector3.up);
            if (offset.sqrMagnitude < 0.25f) return true;
            Vector3 first = (Vector3)RetryFirst.GetValue(_retry);
            Vector3 last = (Vector3)RetryLast.GetValue(_retry);
            Vector3 sight = player.PlayerVisionTarget?.VisionTransform?.position ?? player.transform.position + Vector3.up;
            Transform mouth = TongueOrigin(__instance);
            if (mouth == null) return true;
            Vector3 mouthOffset = mouth.position - body;
            // Hold the chosen angle while walking there. Re-rotating from the
            // moving body on every retry would make the destination spiral away.
            if (PonyPolicy.CanReuseFlank(runtime.FlankLocked, runtime.FlankTarget == player,
                (runtime.FlankTargetPoint - center).sqrMagnitude,
                Vector3.Distance(runtime.FlankPoint, FlankStart(__instance))) &&
                !agent.Raycast(runtime.FlankPoint, out _) &&
                ClearTongueLine(runtime.FlankPoint + mouthOffset, sight, player))
            {
                Destination(__instance) = runtime.FlankPoint;
                FlankPlayerPosition(__instance) = center;
                FlankCacheTime(__instance) = 0.5f;
                __result = true;
                return false;
            }
            runtime.FlankLocked = false;
            // Finite candidate set, retaining the original retry count and deadline.
            for (int i = 0; i < 8; i++)
            {
                float angle = (20f + 15f * (i / 2)) * (i % 2 == 0 ? 1f : -1f);
                Vector3 candidate = center + Quaternion.Euler(0f, angle, 0f) * offset;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit sample, 0.35f, agent.areaMask)) continue;
                candidate = sample.position;
                Vector3 newOffset = Vector3.ProjectOnPlane(candidate - center, Vector3.up);
                if (Vector3.Distance(candidate, FlankStart(__instance)) > PonyPolicy.FlankDistance ||
                    Math.Abs(candidate.y - agent.transform.position.y) > 0.5f ||
                    newOffset.magnitude > __instance.tongueAttackRange ||
                    Math.Abs(newOffset.magnitude - offset.magnitude) > 0.35f ||
                    Vector3.Distance(newOffset, first) < __instance.tongueRepositionMinimumDistance ||
                    Vector3.Distance(newOffset, last) < __instance.tongueRepositionMinimumDistance ||
                    Vector3.Angle(newOffset, first) < 10f || Vector3.Angle(newOffset, last) < 10f ||
                    agent.Raycast(candidate, out _) ||
                    !ClearTongueLine(candidate + mouthOffset, sight, player)) continue;
                Destination(__instance) = candidate;
                FlankPlayerPosition(__instance) = center;
                FlankCacheTime(__instance) = 0.5f;
                runtime.FlankLocked = true;
                runtime.FlankPoint = candidate;
                runtime.FlankTargetPoint = center;
                runtime.FlankTarget = player;
                __result = true;
                return false;
            }
        }
        catch (Exception e) { Fault(state, e); }
        return true;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(EnemySnatcher), "TongueAttackCheck")]
    private static bool CoverPrefix(EnemySnatcher __instance, ref bool __result)
    {
        if (!Try(__instance, out EliteState state, out Runtime runtime)) return true;
        try
        {
            PlayerAvatar player = Target(__instance);
            if (player == null || Time.time < runtime.NextCoverScan) return true;
            runtime.NextCoverScan = Time.time + 0.25f;
            if (!PonyPolicy.CanClearCover(runtime.Policy.CoverUsed, TargetVisible(__instance, player),
                PlayerCooldown(__instance, player), Held(player) != null,
                Ready(state) && __instance.currentState == EnemySnatcher.State.Chase && TongueFree(__instance) &&
                AttackPause(__instance) <= 0f && Recovery(__instance) <= 0f && AttackCooldown(__instance) <= 0f)) return true;
            Transform mouth = TongueOrigin(__instance);
            Vector3 sight = player.PlayerVisionTarget?.VisionTransform?.position ?? player.transform.position + Vector3.up;
            if (mouth == null || !FindBlockingObject(__instance, mouth.position, sight, out PhysGrabObject item) ||
                item == null || IsPlayer(item) || IsEnemy(item) || !item.gameObject.activeInHierarchy ||
                !GrabObject(__instance, item, EnemySnatcher.State.GrabPhysObject)) return true;
            runtime.Policy.CoverUsed = true;
            __result = true;
            return false;
        }
        catch (Exception e) { Fault(state, e); return true; }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(EnemySnatcher), "CeilingStateGround")]
    private static bool PatrolPrefix(EnemySnatcher __instance)
    {
        if (!Try(__instance, out EliteState state, out Runtime runtime)) return true;
        try
        {
            if (__instance.currentState != EnemySnatcher.State.Roam || Target(__instance) != null) return true;
            // Replace only the calm patrol lottery, not chase/search transitions.
            // Never clear an active player's safety cooldown through a new jump.
            if (!Ready(state) || CeilingActive(__instance) || !TongueFree(__instance) ||
                AttackPause(__instance) > 0f || CeilingCooldown(__instance) > 0f || AnyPlayerCooldown(__instance) ||
                !CeilingJumpReady(__instance) || Time.time < runtime.Policy.NextPatrolAt ||
                Time.time < runtime.Policy.GroundReadyAt) return false;
            if (!runtime.Policy.TryPatrol(Time.time, UnityEngine.Random.value, UnityEngine.Random.value)) return false;
            CeilingJumpTry(__instance, false);
            if (CeilingActive(__instance))
            {
                runtime.Patrol = true;
                runtime.PatrolEndsAt = 0f;
            }
            return false;
        }
        catch (Exception e) { Fault(state, e); return true; }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(EnemySnatcher), "CeilingStateWatchdog")]
    private static bool PatrolStayPrefix(EnemySnatcher __instance, ref bool __result)
    {
        if (!Try(__instance, out EliteState state, out Runtime runtime)) return true;
        try
        {
            // Extend only the ordinary stay deadline while a native capture is
            // finishing. Stuns, lost support and navigation failures still win.
            bool capture = CeilingAttached(__instance) && CeilingCaptureInProgress(__instance);
            bool protectCapture = runtime.Policy.ObserveCeilingCapture(Time.time, capture);
            if (protectCapture && Ready(state))
            {
                runtime.Patrol = false;
                __result = false;
                return false;
            }
            if (!runtime.Patrol || !CeilingAttached(__instance)) return true;
            if (Target(__instance) != null) { runtime.Patrol = false; return true; }
            if (runtime.PatrolEndsAt <= 0f) runtime.PatrolEndsAt = Time.time + PonyPolicy.PatrolDuration(UnityEngine.Random.value);
            if (Time.time < runtime.PatrolEndsAt) { __result = false; return false; }
            CeilingDrop(__instance);
            __result = true;
            return false;
        }
        catch (Exception e) { Fault(state, e); return true; }
    }

    private static bool InterceptPoint(EnemySnatcher pony, EliteState state, PlayerAvatar player,
        NavMeshAgent agent, out Vector3 point)
    {
        point = default;
        Vector3 velocity = Vector3.ProjectOnPlane(Velocity(player), Vector3.up);
        if (velocity.sqrMagnitude < 0.25f) return false;
        Rigidbody body = GameFieldAccess.PhysicsBody(GameFieldAccess.Rigidbody(state.Enemy));
        float height = Math.Max(0f, body.worldCenterOfMass.y - LastNavmesh(player).y);
        float fallTime = Mathf.Sqrt(2f * height / Math.Max(0.1f, -Physics.gravity.y));
        // Aim two metres ahead at landing, not at takeoff. Native landing checks
        // still reject sprints, unsafe geometry and targets looking another way.
        Vector3 desired = LastNavmesh(player) + velocity.normalized * PonyPolicy.InterceptDistance(velocity.magnitude, fallTime);
        if (!NavMesh.SamplePosition(desired, out NavMeshHit sample, 0.3f, agent.areaMask) ||
            Math.Abs(sample.position.y - LastNavmesh(player).y) > 0.5f ||
            agent.Raycast(sample.position, out _) || !CeilingPathClear(pony, sample.position) ||
            Vector3.ProjectOnPlane(sample.position - agent.transform.position, Vector3.up).magnitude > 6f) return false;
        point = sample.position;
        return true;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(EnemySnatcher), "StateChase")]
    private static bool InterceptPrefix(EnemySnatcher __instance)
    {
        if (!Try(__instance, out EliteState state, out Runtime runtime)) return true;
        try
        {
            PlayerAvatar player = Target(__instance);
            // A reachable ceiling capture takes priority over the optional drop.
            // The native setter still owns tells, collision, disarming and retries.
            if (TryCeilingCapture(__instance, state, runtime, player))
            { runtime.Intercept = false; return false; }
            if (!Ready(state) || Impulse(__instance) || !CeilingAttached(__instance) ||
                CeilingStayRequired(__instance) || !TargetVisible(__instance, player) ||
                !TongueFree(__instance) || PlayerCooldown(__instance, player) ||
                AttackPause(__instance) > 0f || !Nav(state, out EnemyNavMeshAgent nav, out NavMeshAgent agent))
            { runtime.Intercept = false; return true; }
            if (!runtime.Intercept)
            {
                if (runtime.Policy.InterceptUsed || Time.time < runtime.NextInterceptScan) return true;
                runtime.NextInterceptScan = Time.time + 0.5f;
                if (!InterceptPoint(__instance, state, player, agent, out Vector3 point)) return true;
                runtime.Intercept = true;
                runtime.Policy.InterceptUsed = true;
                runtime.InterceptTarget = player;
                runtime.InterceptPoint = point;
                runtime.InterceptEndsAt = Time.time + PonyPolicy.InterceptDuration;
                runtime.NextPathCheck = Time.time + 0.25f;
                runtime.NextLandingCheck = 0f;
            }
            if (runtime.InterceptTarget != player || Time.time >= runtime.InterceptEndsAt)
            { runtime.Intercept = false; return true; }
            if (Time.time >= runtime.NextPathCheck)
            {
                runtime.NextPathCheck = Time.time + 0.25f;
                if (!InterceptPoint(__instance, state, player, agent, out Vector3 point))
                { runtime.Intercept = false; return true; }
                runtime.InterceptPoint = point;
            }
            Vector3 body = GameFieldAccess.Rigidbody(state.Enemy).transform.position;
            if (Vector3.ProjectOnPlane(body - runtime.InterceptPoint, Vector3.up).magnitude < 0.4f)
            {
                nav.ResetPath(); nav.Stop(0.2f);
                // Keep native floor support, fall trajectory and body-clearance tests.
                if (Time.time < runtime.NextLandingCheck) return false;
                runtime.NextLandingCheck = Time.time + 0.15f;
                if (ScareLanding(__instance, player, out _))
                {
                    Rigidbody physics = GameFieldAccess.PhysicsBody(GameFieldAccess.Rigidbody(state.Enemy));
                    physics.AddForce(-Vector3.ProjectOnPlane(physics.linearVelocity, Vector3.up), ForceMode.VelocityChange);
                    CeilingDrop(__instance);
                    if (!CeilingAttached(__instance))
                    {
                        DropVoice(__instance);
                        runtime.LandingPending = true;
                        runtime.Intercept = false;
                    }
                }
                return false;
            }
            nav.SetDestination(runtime.InterceptPoint);
            nav.OverrideAgent(__instance.chaseSpeed, __instance.chaseAcceleration, 0.25f);
            GameFieldAccess.Rigidbody(state.Enemy).OverrideFollowPosition(0.1f, Math.Max(0.1f, __instance.chaseRigidbodySpeed));
            return false;
        }
        catch (Exception e) { runtime.Intercept = false; Fault(state, e); return true; }
    }

    [HarmonyPostfix, HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemySnatcher), "Update")]
    private static void UpdatePostfix(EnemySnatcher __instance)
    {
        if (!Try(__instance, out EliteState state, out Runtime runtime)) return;
        try
        {
            bool ceiling = CeilingActive(__instance);
            if (runtime.WasOnCeiling && !ceiling)
            {
                runtime.Policy.Landed(Time.time);
                runtime.Patrol = false;
                runtime.Intercept = false;
                if (runtime.LandingPending)
                {
                    runtime.LandingPauseUntil = Time.time + PonyPolicy.LandingPause;
                    runtime.LandingPending = false;
                }
            }
            runtime.WasOnCeiling = ceiling;
            if (runtime.LandingPending || Time.time < runtime.LandingPauseUntil)
                AttackPause(__instance) = Math.Max(AttackPause(__instance), PonyPolicy.LandingPause);
            bool calm = Target(__instance) == null &&
                (__instance.currentState == EnemySnatcher.State.Idle || __instance.currentState == EnemySnatcher.State.Roam);
            runtime.Policy.ObserveCalm(Time.time, calm);
            if (__instance.currentState != EnemySnatcher.State.TongueReposition) runtime.FlankLocked = false;
            // Do not reset a prior prefix failure on every successful idle Update.
        }
        catch (Exception e) { Fault(state, e); }
    }
}
