using HarmonyLib;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

// Use the native Sunstone scroll step; preserve keyboard/gamepad navigation.
// Only this menu's scroll box is adjusted, never Roles, Events or vanilla pages.
[HarmonyPatch(typeof(MenuScrollBox), "Update")]
internal static class EliteMenuScroll
{
    [HarmonyPrefix]
    private static void Prefix(MenuScrollBox __instance, float ___scrollHeight,
        float ___scrollerStartPosition, float ___scrollerEndPosition)
    {
        if (!EliteMenu.OwnsScrollBox(__instance) || __instance.scrollBarBackground == null ||
            __instance.scrollHandle == null) return;
        float travel = Mathf.Abs(___scrollerStartPosition - ___scrollerEndPosition);
        float handle = __instance.scrollBarBackground.rect.height - __instance.scrollHandle.sizeDelta.y;
        __instance.scrollWheelStep = EliteMenuPolicy.WheelStep(___scrollHeight, travel, handle);
    }
}
