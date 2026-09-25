using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

/// <summary>
/// Runtime-safe access to fields that are publicized by the compile-time game
/// references but remain non-public in the shipped Assembly-CSharp.dll.
/// Direct IL field access to these members throws FieldAccessException.
/// </summary>
internal static class GameFieldAccess
{
    private static readonly AccessTools.FieldRef<Enemy, PhotonView> EnemyPhotonView =
        AccessTools.FieldRefAccess<Enemy, PhotonView>("PhotonView");
    private static readonly AccessTools.FieldRef<Enemy, EnemyParent> EnemyParent =
        AccessTools.FieldRefAccess<Enemy, EnemyParent>("EnemyParent");
    private static readonly AccessTools.FieldRef<Enemy, LayerMask> EnemyVisionMask =
        AccessTools.FieldRefAccess<Enemy, LayerMask>("VisionMask");
    private static readonly AccessTools.FieldRef<Enemy, EnemyVision> EnemyVision =
        AccessTools.FieldRefAccess<Enemy, EnemyVision>("Vision");
    private static readonly AccessTools.FieldRef<Enemy, EnemyOnScreen> EnemyOnScreen =
        AccessTools.FieldRefAccess<Enemy, EnemyOnScreen>("OnScreen");
    private static readonly AccessTools.FieldRef<Enemy, EnemyRigidbody> EnemyRigidbody =
        AccessTools.FieldRefAccess<Enemy, EnemyRigidbody>("Rigidbody");
    private static readonly AccessTools.FieldRef<Enemy, EnemyNavMeshAgent> EnemyNavMeshAgent =
        AccessTools.FieldRefAccess<Enemy, EnemyNavMeshAgent>("NavMeshAgent");
    private static readonly AccessTools.FieldRef<Enemy, EnemyStateInvestigate> EnemyStateInvestigate =
        AccessTools.FieldRefAccess<Enemy, EnemyStateInvestigate>("StateInvestigate");
    private static readonly AccessTools.FieldRef<Enemy, EnemyStateChase> EnemyStateChase =
        AccessTools.FieldRefAccess<Enemy, EnemyStateChase>("StateChase");
    private static readonly AccessTools.FieldRef<Enemy, bool> EnemyHasStateInvestigate =
        AccessTools.FieldRefAccess<Enemy, bool>("HasStateInvestigate");
    private static readonly AccessTools.FieldRef<Enemy, EnemyJump> EnemyJump =
        AccessTools.FieldRefAccess<Enemy, EnemyJump>("Jump");
    private static readonly AccessTools.FieldRef<Enemy, EnemyHealth> EnemyHealth =
        AccessTools.FieldRefAccess<Enemy, EnemyHealth>("Health");
    private static readonly AccessTools.FieldRef<Enemy, bool> EnemyHasHealth =
        AccessTools.FieldRefAccess<Enemy, bool>("HasHealth");
    private static readonly AccessTools.FieldRef<Enemy, PlayerAvatar> EnemyTargetPlayerAvatar =
        AccessTools.FieldRefAccess<Enemy, PlayerAvatar>("TargetPlayerAvatar");
    private static readonly AccessTools.FieldRef<Enemy, int> EnemyTargetPlayerViewId =
        AccessTools.FieldRefAccess<Enemy, int>("TargetPlayerViewID");
    private static readonly AccessTools.FieldRef<EnemyOnScreen, bool> OnScreenAnyRef =
        AccessTools.FieldRefAccess<EnemyOnScreen, bool>("OnScreenAny");

    private static readonly AccessTools.FieldRef<EnemyHealth, int> HealthCurrentRef =
        AccessTools.FieldRefAccess<EnemyHealth, int>("healthCurrent");
    private static readonly AccessTools.FieldRef<EnemyHealth, bool> HealthDeadRef =
        AccessTools.FieldRefAccess<EnemyHealth, bool>("dead");

    private static readonly AccessTools.FieldRef<PlayerAvatar, bool> PlayerDisabledRef =
        AccessTools.FieldRefAccess<PlayerAvatar, bool>("isDisabled");
    private static readonly AccessTools.FieldRef<PlayerAvatar, PlayerTumble> PlayerTumbleRef =
        AccessTools.FieldRefAccess<PlayerAvatar, PlayerTumble>("tumble");
    private static readonly AccessTools.FieldRef<PlayerTumble, PhysGrabObject> TumblePhysGrabObjectRef =
        AccessTools.FieldRefAccess<PlayerTumble, PhysGrabObject>("physGrabObject");

    private static readonly AccessTools.FieldRef<ValuableObject, PhysGrabObject> ValuablePhysGrabObjectRef =
        AccessTools.FieldRefAccess<ValuableObject, PhysGrabObject>("physGrabObject");
    private static readonly AccessTools.FieldRef<ValuableObject, float> ValuableDollarValueRef =
        AccessTools.FieldRefAccess<ValuableObject, float>("dollarValueCurrent");
    private static readonly AccessTools.FieldRef<PhysGrabObject, PhysGrabObjectImpactDetector> PhysGrabImpactDetectorRef =
        AccessTools.FieldRefAccess<PhysGrabObject, PhysGrabObjectImpactDetector>("impactDetector");
    private static readonly AccessTools.FieldRef<PhysGrabObject, Vector3> PhysGrabVelocityRef =
        AccessTools.FieldRefAccess<PhysGrabObject, Vector3>("rbVelocity");
    private static readonly AccessTools.FieldRef<PhysGrabObjectImpactDetector, bool> ImpactIndestructibleRef =
        AccessTools.FieldRefAccess<PhysGrabObjectImpactDetector, bool>("isIndestructible");

    private static readonly AccessTools.FieldRef<EnemyParent, bool> ParentSetupDoneRef =
        AccessTools.FieldRefAccess<EnemyParent, bool>("SetupDone");
    private static readonly AccessTools.FieldRef<EnemyParent, bool> ParentSpawnedRef =
        AccessTools.FieldRefAccess<EnemyParent, bool>("Spawned");
    private static readonly AccessTools.FieldRef<EnemyParent, LevelPoint> ParentFirstSpawnPointRef =
        AccessTools.FieldRefAccess<EnemyParent, LevelPoint>("firstSpawnPoint");
    private static readonly AccessTools.FieldRef<EnemyParent, bool> ParentFirstSpawnPointUsedRef =
        AccessTools.FieldRefAccess<EnemyParent, bool>("firstSpawnPointUsed");

    private static readonly AccessTools.FieldRef<EnemyDirector, float> DirectorSpawnIdlePauseTimerRef =
        AccessTools.FieldRefAccess<EnemyDirector, float>("spawnIdlePauseTimer");

    private static readonly AccessTools.FieldRef<EnemyNavMeshAgent, float> NavDefaultSpeedRef =
        AccessTools.FieldRefAccess<EnemyNavMeshAgent, float>("DefaultSpeed");
    private static readonly AccessTools.FieldRef<EnemyNavMeshAgent, float> NavDefaultAccelerationRef =
        AccessTools.FieldRefAccess<EnemyNavMeshAgent, float>("DefaultAcceleration");
    private static readonly AccessTools.FieldRef<EnemyRigidbody, float> RigidbodyNotMovingTimerRef =
        AccessTools.FieldRefAccess<EnemyRigidbody, float>("notMovingTimer");
    private static readonly AccessTools.FieldRef<EnemyJump, bool> JumpingRef =
        AccessTools.FieldRefAccess<EnemyJump, bool>("jumping");

    private static readonly AccessTools.FieldRef<EnemyBeamer, Enemy> BeamerEnemyRef =
        AccessTools.FieldRefAccess<EnemyBeamer, Enemy>("enemy");
    private static readonly AccessTools.FieldRef<EnemyBeamer, PhotonView> BeamerPhotonViewRef =
        AccessTools.FieldRefAccess<EnemyBeamer, PhotonView>("photonView");
    private static readonly AccessTools.FieldRef<EnemyBeamer, PlayerAvatar> BeamerPlayerTargetRef =
        AccessTools.FieldRefAccess<EnemyBeamer, PlayerAvatar>("playerTarget");

    private static readonly AccessTools.FieldRef<EnemyBang, Enemy> BangEnemyRef =
        AccessTools.FieldRefAccess<EnemyBang, Enemy>("enemy");
    private static readonly AccessTools.FieldRef<EnemyBang, PhotonView> BangPhotonViewRef =
        AccessTools.FieldRefAccess<EnemyBang, PhotonView>("photonView");
    private static readonly AccessTools.FieldRef<EnemyBang, float> BangFuseLerpRef =
        AccessTools.FieldRefAccess<EnemyBang, float>("fuseLerp");
    private static readonly AccessTools.FieldRef<EnemyBang, bool> BangFuseActiveRef =
        AccessTools.FieldRefAccess<EnemyBang, bool>("fuseActive");

    private static readonly AccessTools.FieldRef<EnemyGnome, float> GnomeAttackCooldownRef =
        AccessTools.FieldRefAccess<EnemyGnome, float>("attackCooldown");
    private static readonly AccessTools.FieldRef<EnemyGnomeDirector, Vector3> GnomeAttackPositionRef =
        AccessTools.FieldRefAccess<EnemyGnomeDirector, Vector3>("attackPosition");

    internal static PhotonView PhotonView(Enemy enemy) => EnemyPhotonView(enemy);
    internal static EnemyParent Parent(Enemy enemy) => EnemyParent(enemy);
    internal static LayerMask VisionMask(Enemy enemy) => EnemyVisionMask(enemy);
    internal static EnemyVision Vision(Enemy enemy) => EnemyVision(enemy);
    internal static EnemyOnScreen OnScreen(Enemy enemy) => EnemyOnScreen(enemy);
    internal static EnemyRigidbody Rigidbody(Enemy enemy) => EnemyRigidbody(enemy);
    internal static EnemyNavMeshAgent NavMeshAgent(Enemy enemy) => EnemyNavMeshAgent(enemy);
    internal static EnemyStateInvestigate StateInvestigate(Enemy enemy) => EnemyStateInvestigate(enemy);
    internal static EnemyStateChase StateChase(Enemy enemy) => EnemyStateChase(enemy);
    internal static bool HasStateInvestigate(Enemy enemy) => EnemyHasStateInvestigate(enemy);
    internal static EnemyJump Jump(Enemy enemy) => EnemyJump(enemy);
    internal static EnemyHealth Health(Enemy enemy) => EnemyHealth(enemy);
    internal static bool HasHealth(Enemy enemy) => EnemyHasHealth(enemy);
    internal static PlayerAvatar TargetPlayer(Enemy enemy) => EnemyTargetPlayerAvatar(enemy);
    internal static int TargetPlayerViewId(Enemy enemy) => EnemyTargetPlayerViewId(enemy);
    internal static void SetTargetPlayer(Enemy enemy, PlayerAvatar player) => EnemyTargetPlayerAvatar(enemy) = player;
    internal static void SetTargetPlayerViewId(Enemy enemy, int viewId) => EnemyTargetPlayerViewId(enemy) = viewId;
    internal static bool OnScreenAny(EnemyOnScreen onScreen) => OnScreenAnyRef(onScreen);

    internal static int HealthCurrent(EnemyHealth health) => HealthCurrentRef(health);
    internal static bool IsDead(EnemyHealth health) => HealthDeadRef(health);

    internal static bool PlayerDisabled(PlayerAvatar player) => PlayerDisabledRef(player);
    internal static PlayerTumble? Tumble(PlayerAvatar player) => PlayerTumbleRef(player);
    internal static PhysGrabObject? TumblePhysGrabObject(PlayerTumble tumble) => TumblePhysGrabObjectRef(tumble);

    internal static PhysGrabObject? ValuablePhysGrabObject(ValuableObject valuable) => ValuablePhysGrabObjectRef(valuable);
    internal static float ValuableDollarValue(ValuableObject valuable) => ValuableDollarValueRef(valuable);
    internal static PhysGrabObjectImpactDetector? ImpactDetector(PhysGrabObject physGrab) => PhysGrabImpactDetectorRef(physGrab);
    internal static Vector3 PhysGrabVelocity(PhysGrabObject physGrab) => PhysGrabVelocityRef(physGrab);
    internal static bool IsIndestructible(PhysGrabObjectImpactDetector impact) => ImpactIndestructibleRef(impact);

    internal static bool ParentSpawned(EnemyParent parent) => ParentSpawnedRef(parent);
    internal static void SetParentSetupDone(EnemyParent parent, bool value) => ParentSetupDoneRef(parent) = value;
    internal static void SetParentFirstSpawnPoint(EnemyParent parent, LevelPoint point) => ParentFirstSpawnPointRef(parent) = point;
    internal static void SetParentFirstSpawnPointUsed(EnemyParent parent, bool value) => ParentFirstSpawnPointUsedRef(parent) = value;

    internal static void EndEnemySpawnIdlePause(EnemyDirector director) => DirectorSpawnIdlePauseTimerRef(director) = 0f;

    internal static float NavDefaultSpeed(EnemyNavMeshAgent nav) => NavDefaultSpeedRef(nav);
    internal static float NavDefaultAcceleration(EnemyNavMeshAgent nav) => NavDefaultAccelerationRef(nav);
    internal static float NotMovingTimer(EnemyRigidbody rigidbody) => RigidbodyNotMovingTimerRef(rigidbody);
    internal static bool IsJumping(EnemyJump jump) => JumpingRef(jump);

    internal static Enemy BeamerEnemy(EnemyBeamer beamer) => BeamerEnemyRef(beamer);
    internal static PhotonView BeamerPhotonView(EnemyBeamer beamer) => BeamerPhotonViewRef(beamer);
    internal static PlayerAvatar BeamerTarget(EnemyBeamer beamer) => BeamerPlayerTargetRef(beamer);
    internal static void SetBeamerTarget(EnemyBeamer beamer, PlayerAvatar target) => BeamerPlayerTargetRef(beamer) = target;

    internal static Enemy BangEnemy(EnemyBang bang) => BangEnemyRef(bang);
    internal static PhotonView BangPhotonView(EnemyBang bang) => BangPhotonViewRef(bang);
    internal static float BangFuseLerp(EnemyBang bang) => BangFuseLerpRef(bang);
    internal static void SetBangFuseLerp(EnemyBang bang, float value) => BangFuseLerpRef(bang) = value;
    internal static bool BangFuseActive(EnemyBang bang) => BangFuseActiveRef(bang);

    internal static float GnomeAttackCooldown(EnemyGnome gnome) => GnomeAttackCooldownRef(gnome);
    internal static void SetGnomeAttackCooldown(EnemyGnome gnome, float value) => GnomeAttackCooldownRef(gnome) = value;
    internal static Vector3 GnomeAttackPosition(EnemyGnomeDirector director) => GnomeAttackPositionRef(director);
}
