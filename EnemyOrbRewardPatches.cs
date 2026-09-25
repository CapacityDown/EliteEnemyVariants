using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class EnemyOrbRewardPatches
{
    internal const float ValueMultiplier = 1.5f;

    private sealed class RewardStamp
    {
        internal bool Applied;
    }

    // Weak keys retain neither destroyed orbs nor objects from previous levels.
    private static readonly ConditionalWeakTable<ValuableObject, RewardStamp> Rewards = new();
    private static readonly AccessTools.FieldRef<ValuableObject, bool> ValueSet =
        AccessTools.FieldRefAccess<ValuableObject, bool>("dollarValueSet");
    private static readonly AccessTools.FieldRef<ValuableObject, float> OriginalValue =
        AccessTools.FieldRefAccess<ValuableObject, float>("dollarValueOriginal");
    private static readonly AccessTools.FieldRef<ValuableObject, float> CurrentValue =
        AccessTools.FieldRefAccess<ValuableObject, float>("dollarValueCurrent");
    private static bool _reportedUnsupportedSpawn;

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(EnemyParent), nameof(EnemyParent.Despawn))]
    private static IEnumerable<CodeInstruction> DespawnTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> source = new(instructions);
        List<int> spawnCalls = new();
        int localSpawns = 0;
        int networkSpawns = 0;
        for (int index = 0; index + 1 < source.Count; index++)
        {
            if (source[index].opcode != OpCodes.Call ||
                source[index].operand is not MethodInfo method ||
                method.ReturnType != typeof(GameObject) || source[index + 1].opcode != OpCodes.Pop)
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            bool localSpawn = method.DeclaringType == typeof(UnityEngine.Object) &&
                method.Name == nameof(UnityEngine.Object.Instantiate) && method.IsGenericMethod &&
                parameters.Length == 3 && parameters[0].ParameterType == typeof(GameObject) &&
                parameters[1].ParameterType == typeof(Vector3) && parameters[2].ParameterType == typeof(Quaternion);
            bool networkSpawn = method.DeclaringType == typeof(PhotonNetwork) &&
                method.Name == nameof(PhotonNetwork.InstantiateRoomObject) && parameters.Length == 5 &&
                parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(Vector3) &&
                parameters[2].ParameterType == typeof(Quaternion) && parameters[3].ParameterType == typeof(byte) &&
                parameters[4].ParameterType == typeof(object[]);
            if (!localSpawn && !networkSpawn)
            {
                continue;
            }

            spawnCalls.Add(index);
            localSpawns += localSpawn ? 1 : 0;
            networkSpawns += networkSpawn ? 1 : 0;
        }

        // Require both known drop paths. An incompatible game/mod keeps its original drops.
        if (localSpawns != 1 || networkSpawns != 1)
        {
            if (!_reportedUnsupportedSpawn)
            {
                _reportedUnsupportedSpawn = true;
                EliteEnemyVariantsPlugin.ModLogger.LogWarning(
                    "Enemy orb reward bonus unavailable: the enemy drop creation path was not recognized.");
            }
            return source;
        }

        MethodInfo register = AccessTools.Method(typeof(EnemyOrbRewardPatches), nameof(RegisterSpawnedOrb));
        for (int index = spawnCalls.Count - 1; index >= 0; index--)
        {
            // Consume the exact factory result and return it unchanged to the original pop.
            // Other mods' separately spawned rewards never pass through this call.
            source.InsertRange(spawnCalls[index] + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, register)
            });
        }
        return source;
    }

    private static GameObject? RegisterSpawnedOrb(GameObject? orb, EnemyParent source)
    {
        if (orb == null || source == null || !SemiFunc.IsMasterClientOrSingleplayer() ||
            !EliteEnemyVariantsPlugin.Instance.Settings.Enabled.Value ||
            !EliteRegistry.IsEnhanced(source.GetComponentInChildren<Enemy>(true)))
        {
            return orb;
        }

        // Despawn reaches the factory only for an eligible death drop. Its earlier
        // DespawnRPC clears active behavior but deliberately retains the Enhanced tier.
        ValuableObject? valuable = orb.GetComponentInChildren<ValuableObject>(true);
        if (valuable != null && orb.GetComponentInChildren<EnemyValuable>(true) != null)
        {
            Rewards.GetValue(valuable, _ => new RewardStamp());
        }
        return orb;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.DollarValueSetLogic))]
    private static void DollarValueSetLogicPostfix(ValuableObject __instance)
    {
        if (__instance == null || !SemiFunc.IsMasterClientOrSingleplayer() || !ValueSet(__instance) ||
            !Rewards.TryGetValue(__instance, out RewardStamp? reward) || reward == null || reward.Applied)
        {
            return;
        }

        reward.Applied = true;
        OriginalValue(__instance) *= ValueMultiplier;
        CurrentValue(__instance) *= ValueMultiplier;
        // The vanilla coroutine next sends DollarValueSetRPC and adds this value to
        // haulGoalMax, so vanilla participants and the host receive the same reward.
    }
}
