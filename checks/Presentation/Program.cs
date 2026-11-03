using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using Mono.Cecil;
using REPOJP.EliteEnemyVariants;
using UnityEngine.InputSystem;

int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
bool Close(float a, float b) => Math.Abs(a - b) < 0.01f;
var expected = new[]
{
    (EliteMenuRowKind.Body, 25f, 18f, false, false, false),
    (EliteMenuRowKind.EnemyHeading, 64f, 20f, true, false, false),
    (EliteMenuRowKind.Setting, 56f, 20f, true, true, false),
    (EliteMenuRowKind.Action, 40f, 18f, true, true, false),
    (EliteMenuRowKind.Message, 80f, 18f, false, false, true),
    (EliteMenuRowKind.Space, 10f, 18f, false, false, false)
};
foreach (var (kind, height, fontSize, bold, control, wrap) in expected)
{
    Check(EliteMenuLayout.Height(kind) == height, "Roles row height " + kind);
    Check(EliteMenuLayout.FontSize(kind) == fontSize, "Roles font size " + kind);
    Check(EliteMenuLayout.Bold(kind) == bold, "Roles font weight " + kind);
    Check(EliteMenuLayout.Control(kind) == control, "Roles interactive sizing " + kind);
    Check(EliteMenuLayout.Wrap(kind) == wrap, "Roles text wrapping " + kind);
}
Check(EliteMenuLayout.BodyLineStep == 32.5f, "body line includes both margins and spacing");

using var game = AssemblyDefinition.ReadAssembly(args[0]);
using var input = AssemblyDefinition.ReadAssembly(Path.Combine(Path.GetDirectoryName(args[0])!, "Unity.InputSystem.dll"));
var getter = input.MainModule.Types.Single(t => t.FullName == "UnityEngine.InputSystem.InputSystem")
    .Methods.Single(m => m.Name == "get_scrollWheelDeltaPerTick");
Check(getter.IsStatic && getter.ReturnType.FullName == "System.Single", "native runtime tick getter");
var callback = input.MainModule.Types.Single(t => t.FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule")
    .Methods.Single(m => m.Name == "OnScrollCallback");
Check(callback.Body.Instructions.Any(i => i.Operand is MethodReference m && m.FullName == getter.FullName),
    "same normalization as native Unity UI");
var update = game.MainModule.Types.Single(t => t.Name == "MenuScrollBox").Methods.Single(m => m.Name == "Update");
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
    .GroupBy(o => o.Value).ToDictionary(g => g.Key, g => g.First());
object? Operand(object? value) => value switch
{
    MethodReference m when m.DeclaringType.Name == "SemiFunc" && m.Name == "InputScrollY" => typeof(SemiFunc).GetMethod(m.Name),
    MethodReference m when m.DeclaringType.Name == "Mathf" && m.Name == "Sign" => typeof(UnityEngine.Mathf).GetMethod(m.Name),
    FieldReference f when f.DeclaringType.Name == "MenuScrollBox" => (object?)typeof(MenuScrollBox).GetField(f.Name) ?? value,
    _ => value
};
var original = update.Body.Instructions.Select(i => new CodeInstruction(opcodes[i.OpCode.Value], Operand(i.Operand))).ToList();
var eliteHelper = typeof(EliteMenuScroll).GetMethod("WheelStep", BindingFlags.Static | BindingFlags.NonPublic)!;
var pipelines = new List<(string Name, Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>> Patch, bool Roles)>
{ ("EEV alone", EliteMenuScroll.Transpiler, false) };
#if ROLE_REFERENCE
pipelines.Add(("Roles then EEV", x => EliteMenuScroll.Transpiler(REPOJP.StageRoles.RoleGuideScrollPatch.Transpiler(x)), true));
pipelines.Add(("EEV then Roles", x => REPOJP.StageRoles.RoleGuideScrollPatch.Transpiler(EliteMenuScroll.Transpiler(x)), true));
#endif
foreach (var (name, patch, roles) in pipelines)
{
    var code = patch(original).ToList();
    int helperCount = roles ? 2 : 1;
    Check(code.Count == original.Count + helperCount * 6, name + " inserts only helper calls");
    Check(code.Count(i => i.Calls(eliteHelper)) == 1, name + " exactly one Elites helper");
    Check(code.Where(original.Contains).SequenceEqual(original), name + " native keyboard, drag, clamps and animation retained");
    int eliteIndex = code.FindIndex(i => i.Calls(eliteHelper));
    int div = eliteIndex;
    while (div >= 0 && code[div].opcode != OpCodes.Div) div--;
    Check(div >= 8, name + " native wheel division found");
    var method = new DynamicMethod("PatchedWheel", typeof(float), new[] { typeof(MenuScrollBox) }, typeof(Program).Module, true);
    var il = method.GetILGenerator();
    il.Emit(OpCodes.Call, typeof(SemiFunc).GetMethod("InputScrollY")!);
    foreach (var instruction in code.GetRange(div - 8, 9 + helperCount * 6))
    {
        switch (instruction.operand)
        {
            case null: il.Emit(instruction.opcode); break;
            case float number: il.Emit(instruction.opcode, number); break;
            case FieldInfo field: il.Emit(instruction.opcode, field); break;
            case MethodInfo call: il.Emit(instruction.opcode, call); break;
            default: throw new Exception("Unexpected native wheel operand");
        }
    }
    il.Emit(OpCodes.Ret);
    var wheelStep = method.CreateDelegate<Func<MenuScrollBox, float>>();
    foreach (var box in new[] { EliteMenu.Box, REPOJP.StageRoles.RoleMenu.Box, new MenuScrollBox() })
    foreach (float units in new[] { 1f, 120f, 10f })
    foreach (float notches in new[] { -5f, -1f, -0.5f, -0.01f, 0.01f, 0.5f, 1f, 5f })
    foreach (float travel in new[] { 100f, 4000f, 50000f })
    foreach (float staleHeight in new[] { 200f, 4000f, 100000f })
    {
        InputSystem.scrollWheelDeltaPerTick = units; SemiFunc.Scroll = units * notches;
        box.scrollerStartPosition = travel; box.scrollHeight = staleHeight;
        float result = wheelStep(box);
        bool own = ReferenceEquals(box, EliteMenu.Box) || roles && ReferenceEquals(box, REPOJP.StageRoles.RoleMenu.Box);
        float target = own ? notches * 97.5f * 960f / travel : Math.Sign(notches) * 15f / (staleHeight * 0.01f);
        Check(Close(result, target), name + (own ? " exact three-line movement" : " unrelated page unchanged"));
        Check(box.scrollWheelStep == 15f, name + " no persistent native step mutation");
    }
    EliteMenu.Open = false; SemiFunc.Scroll = 1;
    Check(Close(wheelStep(EliteMenu.Box), 15f / (EliteMenu.Box.scrollHeight * 0.01f)), name + " closed menu does not own box");
    EliteMenu.Open = true;
}
foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    Check(EliteMenuPolicy.WheelStep(invalid, 1, 100, 20) == 0, "invalid wheel rejected");
Check(EliteMenuPolicy.WheelStep(float.MaxValue, float.Epsilon, 1, 1) == 0, "overflow rejected");
foreach (var malformed in new[] { new List<CodeInstruction>(), original.Concat(original).ToList() })
{
    bool rejected = false;
    try { _ = EliteMenuScroll.Transpiler(malformed).ToList(); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "unexpected native contract fails patch rather than affecting another expression");
}

// Compare the shared format with the actual reference source when available.
// RoleShuffle is a test-only input, never a required plugin or build dependency.
if (args.Length > 1 && Directory.Exists(args[1]))
{
    string roles = File.ReadAllText(Path.Combine(args[1], "RoleMenu.cs"));
    string settings = File.ReadAllText(Path.Combine(args[1], "RoleMenuSettings.cs"));
    Check(Regex.IsMatch(roles, @"GuideLineHeight\s*=\s*25f"), "reference body height");
    Check(Regex.IsMatch(roles, @"RowPadding\s*=\s*3f") && Regex.IsMatch(roles, @"RowSpacing\s*=\s*1\.5f"), "reference row margins");
    Check(Regex.IsMatch(roles, @"20f,\s*FontStyles.Bold,\s*64f,\s*wrap: false"), "reference guide headings");
    Check(settings.Contains("20, FontStyles.Bold, emblem.HasValue ? 56 : 40"), "reference setting rows");
    Check(roles.Contains("\"> \" + label, 18, FontStyles.Bold, 40"), "reference tool buttons");
    Check(roles.Contains("_utilityMessage, 18, FontStyles.Normal, 80, true"), "reference feedback block");
    string root = Path.GetFullPath(Path.Combine(args[1], "..", "EliteEnemyVariants"));
    string Normalize(string s) => Regex.Replace(s.Replace("REPOJP.StageRoles", "REPOJP.EliteEnemyVariants").Replace("RoleMenuButtonVisual", "EliteMenuButtonVisual"), @"\s+", "");
    Check(Normalize(File.ReadAllText(Path.Combine(args[1], "RoleMenuButtonVisual.cs"))) ==
        Normalize(File.ReadAllText(Path.Combine(root, "UI/EliteMenuButtonVisual.cs"))), "reference control colors, borders and hover states");
}
Console.WriteLine($"PASS: {checks} presentation checks across {pipelines.Count} patch orders; actual game IL with stand-in UI objects, not an in-game visual test.");
