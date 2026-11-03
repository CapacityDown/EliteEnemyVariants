using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REPOJP.EliteEnemyVariants;

// Retain the native expression so either Roles or Elites can patch first.
// Each helper changes only its own page; keyboard/gamepad input is untouched.
[HarmonyPatch(typeof(MenuScrollBox), "Update")]
internal static class EliteMenuScroll
{
    private static readonly Func<float> ReadWheelUnitsPerNotch = AccessTools.MethodDelegate<Func<float>>(
        AccessTools.PropertyGetter(typeof(InputSystem), "scrollWheelDeltaPerTick"));

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = new(instructions);
        var wheelStep = AccessTools.Field(typeof(MenuScrollBox), "scrollWheelStep");
        var height = AccessTools.Field(typeof(MenuScrollBox), "scrollHeight");
        int matches = 0;
        for (int i = 0; i + 7 < code.Count; i++)
        {
            if (code[i].opcode != OpCodes.Ldarg_0 || !code[i + 1].LoadsField(wheelStep) ||
                code[i + 2].opcode != OpCodes.Mul || code[i + 3].opcode != OpCodes.Ldarg_0 ||
                !code[i + 4].LoadsField(height) || code[i + 5].opcode != OpCodes.Ldc_R4 ||
                !Equals(code[i + 5].operand, 0.01f) || code[i + 6].opcode != OpCodes.Mul ||
                code[i + 7].opcode != OpCodes.Div) continue;
            code.InsertRange(i + 8, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MenuScrollBox), "scrollerStartPosition")),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MenuScrollBox), "scrollerEndPosition")),
                CodeInstruction.Call(typeof(EliteMenuScroll), nameof(WheelStep))
            });
            matches++;
            i += 13;
        }
        if (matches != 1)
            throw new InvalidOperationException($"Expected one MenuScrollBox wheel calculation, found {matches}.");
        return code;
    }

    internal static float WheelStep(float nativeStep, MenuScrollBox box, float start, float end)
    {
        if (!EliteMenu.OwnsScrollBox(box)) return nativeStep;
        if (box.scrollBarBackground == null || box.scrollHandle == null) return 0;
        return EliteMenuPolicy.WheelStep(SemiFunc.InputScrollY(), ReadWheelUnitsPerNotch(),
            Mathf.Abs(start - end), box.scrollBarBackground.rect.height - box.scrollHandle.sizeDelta.y);
    }
}
