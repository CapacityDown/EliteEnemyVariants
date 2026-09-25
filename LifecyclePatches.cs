using HarmonyLib;

namespace REPOJP.EliteEnemyVariants;

[HarmonyPatch]
internal static class LifecyclePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    private static void LevelGeneratorGenerateDonePostfix()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            EliteRegistry.BeginStage();
            EliteRegistry.BuildInitialSelectionPlan();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.ChangeLevel))]
    private static void RunManagerChangeLevelPrefix()
    {
        EliteRegistry.ClearStage();
    }
}
