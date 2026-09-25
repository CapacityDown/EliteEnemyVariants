using UnityEngine;
using HarmonyLib;
using Photon.Pun;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class HuntsmanElitePatches
{
    private static readonly AccessTools.FieldRef<EnemyHunter, Vector3> InvestigatePoint =
        AccessTools.FieldRefAccess<EnemyHunter, Vector3>("investigatePoint");
    private static readonly AccessTools.FieldRef<EnemyHunter, Quaternion> InvestigateAimHorizontal =
        AccessTools.FieldRefAccess<EnemyHunter, Quaternion>("investigateAimHorizontal");
    private static readonly AccessTools.FieldRef<EnemyHunter, Quaternion> InvestigateAimVertical =
        AccessTools.FieldRefAccess<EnemyHunter, Quaternion>("investigateAimVertical");

    internal static void OnSpawn(EliteState state)
    {
        state.AdapterState = new HuntsmanRuntimeState();
    }

    internal static void Tick(EliteState state)
    {
        EnemyHunter? hunter = state.Enemy.GetComponent<EnemyHunter>();
        if (hunter == null || state.Enemy.IsStunned())
        {
            return;
        }
        HuntsmanRuntimeState runtime = GetRuntime(state);
        if (hunter.currentState is not EnemyHunter.State.Aim and not EnemyHunter.State.Shoot)
        {
            runtime.AccuracyRolled = false;
            runtime.MissThisShot = false;
        }
        if (runtime.PendingInvestigate &&
            hunter.currentState is not EnemyHunter.State.Aim and not EnemyHunter.State.Shoot)
        {
            runtime.PendingInvestigate = false;
            GameFieldAccess.StateInvestigate(state.Enemy).Set(
                runtime.PendingInvestigatePoint,
                runtime.PendingInvestigatePathfindOnly);
        }
    }

    [HarmonyPostfix]
    [HarmonyAfter(StageFluxCompatibility.PluginGuid, RoleShuffleCompatibility.PluginGuid)]
    [HarmonyPatch(typeof(EnemyHunter), "AimLogic")]
    private static void AimLogicPostfix(EnemyHunter __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteRegistry.TryGet(__instance, out EliteState state))
        {
            return;
        }
        HuntsmanRuntimeState runtime = GetRuntime(state);
        Vector3 origin = __instance.gunAimTransform.position;
        Vector3 target = InvestigatePoint(__instance);
        float distance = Vector3.Distance(origin, target);
        float falloff = Mathf.InverseLerp(5f, 45f, distance);
        if (!runtime.AccuracyRolled)
        {
            runtime.AccuracyRolled = true;
            // Accuracy loss rises with distance and caps at a 30% miss chance.
            runtime.MissThisShot = Random.value < falloff * 0.3f;
            float sign = Random.value < 0.5f ? -1f : 1f;
            // Convert a small target-space offset to an angle so a miss still
            // lands close to the investigated player position at any distance.
            float lateralOffset = Mathf.Lerp(1.5f, 2.25f, falloff);
            float verticalOffset = Random.Range(-0.5f, 0.5f);
            runtime.MissYaw = sign * Mathf.Atan2(lateralOffset, Mathf.Max(1f, distance)) * Mathf.Rad2Deg;
            runtime.MissPitch = Mathf.Atan2(verticalOffset, Mathf.Max(1f, distance)) * Mathf.Rad2Deg;
            Vector3 forward = target - origin;
            forward.y = 0f;
            Vector3 targetRight = forward.sqrMagnitude > 0.01f
                ? Vector3.Cross(Vector3.up, forward.normalized)
                : __instance.gunAimTransform.right;
            runtime.MissImpactPoint = target + targetRight * (sign * lateralOffset) + Vector3.up * verticalOffset;
        }
        if (!runtime.MissThisShot)
        {
            return;
        }

        Vector3 direction = target - origin;
        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }
        direction.Normalize();
        direction = Quaternion.AngleAxis(runtime.MissYaw, Vector3.up) * direction;
        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        direction = Quaternion.AngleAxis(runtime.MissPitch, right) * direction;

        Quaternion missRotation = Quaternion.LookRotation(direction, Vector3.up);
        InvestigateAimHorizontal(__instance) = Quaternion.Euler(0f, missRotation.eulerAngles.y, 0f);
        float pitch = missRotation.eulerAngles.x;
        pitch = pitch < 180f ? Mathf.Clamp(pitch, 0f, 45f) : Mathf.Clamp(pitch, 315f, 360f);
        InvestigateAimVertical(__instance) = Quaternion.Euler(pitch, 0f, 0f);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PhotonView), nameof(PhotonView.RPC),
        new[] { typeof(string), typeof(RpcTarget), typeof(object[]) })]
    private static void PhotonViewRpcPrefix(
        PhotonView __instance,
        string methodName,
        object[] parameters)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || methodName != "ShootRPC" ||
            parameters == null || parameters.Length == 0 ||
            __instance.GetComponent<EnemyHunter>() is not EnemyHunter hunter ||
            !EliteRegistry.TryGet(hunter, out EliteState state))
        {
            return;
        }
        HuntsmanRuntimeState runtime = GetRuntime(state);
        if (runtime.MissThisShot)
        {
            parameters[0] = runtime.MissImpactPoint;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnemyHunter), "ShootRPC")]
    private static void ShootRpcPrefix(EnemyHunter __instance, ref Vector3 _hitPosition)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteRegistry.TryGet(__instance, out EliteState state))
        {
            return;
        }
        HuntsmanRuntimeState runtime = GetRuntime(state);
        if (runtime.MissThisShot)
        {
            _hitPosition = runtime.MissImpactPoint;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyDirector), nameof(EnemyDirector.SetInvestigate))]
    private static void EnemyDirectorSetInvestigatePostfix(
        Vector3 position,
        float radius,
        bool pathfindOnly)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
        {
            return;
        }
        float forcedRange = EliteEnemyVariantsPlugin.Instance.Settings.HuntsmanSoundInvestigateRange.Value;
        foreach (EliteState state in EliteRegistry.States)
        {
            if (!state.Spawned || state.Archetype != EliteArchetype.Huntsman || state.Enemy == null ||
                !GameFieldAccess.HasStateInvestigate(state.Enemy))
            {
                continue;
            }
            float distance = Vector3.Distance(position, state.Enemy.transform.position);
            if (distance > forcedRange)
            {
                continue;
            }
            EnemyHunter? hunter = state.Enemy.GetComponent<EnemyHunter>();
            if (hunter == null)
            {
                continue;
            }
            if (hunter.currentState is EnemyHunter.State.Aim or EnemyHunter.State.Shoot)
            {
                HuntsmanRuntimeState runtime = GetRuntime(state);
                runtime.PendingInvestigate = true;
                runtime.PendingInvestigatePoint = position;
                runtime.PendingInvestigatePathfindOnly = pathfindOnly;
                continue;
            }
            EnemyStateInvestigate investigate = GameFieldAccess.StateInvestigate(state.Enemy);
            bool vanillaAlreadyReached = distance / Mathf.Max(0.01f, investigate.rangeMultiplier) < radius;
            if (!vanillaAlreadyReached)
            {
                investigate.Set(position, pathfindOnly);
            }
        }
    }

    private static HuntsmanRuntimeState GetRuntime(EliteState state)
    {
        if (state.AdapterState is not HuntsmanRuntimeState runtime)
        {
            runtime = new HuntsmanRuntimeState();
            state.AdapterState = runtime;
        }
        return runtime;
    }
}
