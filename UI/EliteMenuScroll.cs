using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

// Match RoleUI's fixed-distance wheel behavior, scoped exclusively to Elites.
[HarmonyPatch(typeof(MenuScrollBox), "Update")]
internal static class EliteMenuScroll
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        var input = AccessTools.Method(typeof(SemiFunc), nameof(SemiFunc.InputScrollY));
        var height = AccessTools.Field(typeof(MenuScrollBox), "scrollHeight");
        for (int i = 0; i + 5 < code.Count; i++)
        {
            if (!code[i].Calls(input) || code[i + 1].opcode != OpCodes.Ldarg_0 || !code[i + 2].LoadsField(height) ||
                code[i + 3].opcode != OpCodes.Ldc_R4 || !Equals(code[i + 3].operand, 0.01f) ||
                code[i + 4].opcode != OpCodes.Mul || code[i + 5].opcode != OpCodes.Div) continue;
            code.InsertRange(i + 6, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MenuScrollBox), "scrollerStartPosition")),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MenuScrollBox), "scrollerEndPosition")),
                CodeInstruction.Call(typeof(EliteMenuScroll), nameof(WheelStep))
            });
            return code;
        }
        EliteEnemyVariantsPlugin.ModLogger.LogWarning("Elites menu wheel adjustment unavailable; using standard scrolling.");
        return code;
    }

    private static float WheelStep(float nativeStep, MenuScrollBox box, float start, float end)
    {
        if (!EliteMenu.OwnsScrollBox(box)) return nativeStep;
        float wheel = SemiFunc.InputScrollY();
        float travel = Mathf.Abs(start - end);
        if (box.scrollBarBackground == null || box.scrollHandle == null) return nativeStep;
        float handle = box.scrollBarBackground.rect.height - box.scrollHandle.sizeDelta.y;
        if (float.IsNaN(wheel) || float.IsInfinity(wheel) || travel <= 0 || handle <= 0) return 0;
        return wheel / 120f * 75f * handle / travel;
    }
}
