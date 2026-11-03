using System;
using HarmonyLib;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal static partial class PonyElitePatches
{
    private static readonly Func<EnemySnatcher, Vector3> PlayerTargetPosition =
        Bind<Func<EnemySnatcher, Vector3>>("PlayerTargetPosition");
    private static readonly Func<EnemySnatcher, Vector3> GrabPosition =
        Bind<Func<EnemySnatcher, Vector3>>("TongueGrabPosition");
    private static readonly Func<EnemySnatcher, Vector3, float, bool> AttackInRange =
        Bind<Func<EnemySnatcher, Vector3, float, bool>>("TongueAttackWithinRange");
    private static readonly Func<EnemySnatcher, int, bool> GrabPlayer =
        Bind<Func<EnemySnatcher, int, bool>>("SetStateGrab");
    private static readonly Func<EnemySnatcher, bool> PlayerFlingReady =
        Bind<Func<EnemySnatcher, bool>>("TonguePlayerFlingReady");
    private static readonly AccessTools.FieldRef<EnemySnatcher, EnemySnatcher.State> TellNextState =
        AccessTools.FieldRefAccess<EnemySnatcher, EnemySnatcher.State>("tongueAttackTellNextState");
    private static readonly AccessTools.FieldRef<EnemySnatcher, int> TongueSequence =
        AccessTools.FieldRefAccess<EnemySnatcher, int>("tongueAppliedSequence");
    private static readonly AccessTools.FieldRef<PlayerTumble, Rigidbody> TumbleBody =
        AccessTools.FieldRefAccess<PlayerTumble, Rigidbody>("rb");

    private static bool CeilingCaptureInProgress(EnemySnatcher pony) => pony.currentState switch
    {
        EnemySnatcher.State.TongueAttackTell => TellNextState(pony) == EnemySnatcher.State.Grab,
        EnemySnatcher.State.Grab => true,
        EnemySnatcher.State.TongueRelease => Convert.ToInt32(TongueTarget.GetValue(pony)) == 1,
        _ => false
    };

    private static bool TryCeilingCapture(EnemySnatcher pony, EliteState state, Runtime runtime, PlayerAvatar player)
    {
        if (player == null || Impulse(pony) || Time.time < runtime.NextCaptureScan ||
            !CeilingAttached(pony) || !Ready(state) ||
            !PonyPolicy.CanCapture(true, true, TargetVisible(pony, player),
                TongueFree(pony), PlayerCooldown(pony, player), AttackPause(pony), Recovery(pony), AttackCooldown(pony)))
            return false;
        runtime.NextCaptureScan = Time.time + PonyPolicy.CaptureScanInterval;
        Vector3 target = PlayerTargetPosition(pony);
        Transform mouth = TongueOrigin(pony);
        if (mouth == null || !AttackInRange(pony, target, pony.tongueAttackRange) ||
            !ClearTongueLine(mouth.position, target, player)) return false;
        // Never call the immediate grab/RPC: this entry retains disarming, the
        // normal tell, miss handling, actual tongue contact and capture timeout.
        return GrabPlayer(pony, UnityEngine.Random.Range(0, 3));
    }

    [HarmonyPostfix, HarmonyPatch(typeof(EnemySnatcher), "TongueFlingDirection")]
    private static void CeilingFlingPostfix(EnemySnatcher __instance, ref Vector3 __result)
    {
        if (!Try(__instance, out EliteState state, out Runtime runtime)) return;
        try
        {
            // No change to ground attacks or thrown objects. The native player
            // impulse, damage, one-throw-per-sequence guard and RPC stay intact.
            if (!CeilingAttached(__instance) || !PlayerFlingReady(__instance)) return;
            int sequence = TongueSequence(__instance);
            Vector3 preferred = __result;
            // Cache zero too: a blocked release must not suddenly gain an
            // untelegraphed throw when native code prepares the impulse again.
            __result = runtime.Throw.Resolve(sequence, () =>
            {
                PlayerAvatar player = Target(__instance);
                PlayerTumble? tumble = GameFieldAccess.Tumble(player);
                PhysGrabObject? physical = tumble != null ? GameFieldAccess.TumblePhysGrabObject(tumble) : null;
                if (tumble == null || physical == null) return Vector3.zero;
                Rigidbody body = TumbleBody(tumble);
                if (body == null) return Vector3.zero;
                Vector3 anchor = GrabPosition(__instance) + body.worldCenterOfMass - PlayerTargetPosition(__instance);
                float radius = PlayerThrowRadius(runtime, physical, body);
                return PonyCeilingThrow.Choose(preferred, __instance.transform.forward,
                    direction => ThrowCorridorClear(__instance, runtime, player, body, anchor, radius, direction));
            });
        }
        catch (Exception e) { Fault(state, e); }
    }

    private static float PlayerThrowRadius(Runtime runtime, PhysGrabObject physical, Rigidbody body)
    {
        runtime.ThrowColliders.Clear();
        physical.GetComponentsInChildren(false, runtime.ThrowColliders);
        float radius = 0.25f;
        foreach (Collider collider in runtime.ThrowColliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger ||
                !collider.gameObject.activeInHierarchy || collider.attachedRigidbody != body) continue;
            radius = Math.Max(radius, (collider.bounds.center - body.worldCenterOfMass).magnitude + collider.bounds.extents.magnitude);
        }
        return radius + 0.05f;
    }

    private static bool ThrowCorridorClear(EnemySnatcher pony, Runtime runtime, PlayerAvatar player,
        Rigidbody body, Vector3 anchor, float radius, Vector3 direction)
    {
        Vector3 pullback = anchor - direction * Math.Max(0f, pony.tonguePhysObjectThrowPullback);
        Vector3 end = anchor + direction * Math.Max(PonyCeilingThrow.ClearanceDistance, pony.tonguePhysObjectThrowSwingDistance);
        // Check both wind-up and initial launch, not just a ray at release. Do
        // not ignore the Pony or other players, only the captured player's body.
        return ThrowSegmentClear(runtime, player, body, body.worldCenterOfMass, anchor, radius) &&
            ThrowSegmentClear(runtime, player, body, anchor, pullback, radius) &&
            ThrowSegmentClear(runtime, player, body, pullback, end, radius);
    }

    private static bool ThrowSegmentClear(Runtime runtime, PlayerAvatar player, Rigidbody body, Vector3 start, Vector3 end, float radius)
    {
        if (!PonyCeilingThrow.Finite(start) || !PonyCeilingThrow.Finite(end) ||
            float.IsNaN(radius) || float.IsInfinity(radius)) return false;
        int mask = SemiFunc.LayerMaskGetShouldHits();
        bool Obstacle(Collider collider) => collider != null &&
            collider.attachedRigidbody != body && collider.GetComponentInParent<PlayerAvatar>() != player;
        int overlaps = Physics.OverlapSphereNonAlloc(start, radius, runtime.ThrowOverlaps, mask, QueryTriggerInteraction.Ignore);
        if (overlaps >= runtime.ThrowOverlaps.Length) return false;
        for (int i = 0; i < overlaps; i++) if (Obstacle(runtime.ThrowOverlaps[i])) return false;
        Vector3 delta = end - start;
        if (delta.sqrMagnitude < 0.0001f) return true;
        int hits = Physics.SphereCastNonAlloc(start, radius, delta.normalized, runtime.ThrowHits,
            delta.magnitude, mask, QueryTriggerInteraction.Ignore);
        if (hits >= runtime.ThrowHits.Length) return false;
        for (int i = 0; i < hits; i++) if (Obstacle(runtime.ThrowHits[i].collider)) return false;
        return true;
    }
}
