using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length < 4) throw new ArgumentException("Pass game Managed directory, EEV DLL, MenuLib DLL, REPOConfig DLL, and optionally previous EEV DLL.");
int checks = 0, fieldRefs = 0, patches = 0, delegates = 0;
void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks++; }
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(args[0]);
foreach (string path in args.Skip(1)) resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
var reader = new ReaderParameters { AssemblyResolver = resolver };
using var game = ModuleDefinition.ReadModule(Path.Combine(args[0], "Assembly-CSharp.dll"), reader);
using var mod = ModuleDefinition.ReadModule(args[1], reader);
using var menu = ModuleDefinition.ReadModule(args[2], reader);
using var config = ModuleDefinition.ReadModule(args[3], reader);
var gameTypes = game.GetTypes().ToDictionary(t => t.FullName);
TypeDefinition ModType(string name) => mod.Types.Single(t => t.Name == name);
bool GameScope(TypeReference type) => type.Scope is AssemblyNameReference a &&
    (a.Name.StartsWith("Unity") || a.Name.StartsWith("Photon") || a.Name is "Assembly-CSharp" or "Assembly-CSharp-firstpass");
FieldDefinition? Field(TypeDefinition type, string name)
{
    for (TypeDefinition? current = type; current != null; current = current.BaseType?.Resolve())
    { var field = current.Fields.FirstOrDefault(f => f.Name == name); if (field != null) return field; }
    return null;
}
string Signature(MethodDefinition m) => m.Name + ":" + m.ReturnType.FullName + "(" + string.Join(",", m.Parameters.Select(p => p.ParameterType.FullName)) + ")";

// Resolve actual binary calls, including dependency calls into Unity and the game.
foreach (var assembly in new[] { mod, menu, config })
foreach (var reference in assembly.GetMemberReferences().Where(m => GameScope(m.DeclaringType) || m.DeclaringType.Scope.Name == "MenuLib"))
{
    if (reference.DeclaringType is ArrayType) continue; // CLR synthetic array accessors.
    IMemberDefinition? found = reference switch { MethodReference m => m.Resolve(), FieldReference f => f.Resolve(), _ => null };
    Check(found != null, assembly.Name + " unresolved: " + reference.FullName);
    if (assembly == mod && found is FieldDefinition field) Check(field.IsPublic, "EEV direct field access: " + reference.FullName);
    if (assembly == mod && found is MethodDefinition method) Check(method.IsPublic, "EEV direct method access: " + reference.FullName);
}

foreach (TypeDefinition type in mod.GetTypes())
foreach (MethodDefinition method in type.Methods.Where(m => m.HasBody))
{
    var code = method.Body.Instructions;
    for (int i = 0; i < code.Count; i++)
    {
        if (code[i].Operand is GenericInstanceMethod fieldRef && fieldRef.Name == "FieldRefAccess" && fieldRef.GenericArguments.Count == 2)
        {
            string name = (string)code[i - 1].Operand;
            if (!gameTypes.TryGetValue(fieldRef.GenericArguments[0].FullName, out var owner)) continue;
            var field = Field(owner, name);
            Check(field != null && field.FieldType.FullName == fieldRef.GenericArguments[1].FullName,
                "runtime field contract: " + owner.Name + "." + name);
            fieldRefs++;
        }
        if (code[i].Operand is not MethodReference call || call.DeclaringType.Name != "AccessTools" || call.Name != "Method") continue;
        // Statically named game methods use typeof(T), name, null, null.
        if (i < 5 || code[i - 5].Operand is not TypeReference target ||
            code[i - 3].Operand is not string methodName || !gameTypes.TryGetValue(target.FullName, out var targetType)) continue;
        var matches = targetType.Methods.Where(m => m.Name == methodName).ToArray();
        Check(matches.Length == 1, "reflection target: " + target.FullName + "." + methodName);
        var nextCall = code.Skip(i + 1).Take(6).Select(x => x.Operand).OfType<GenericInstanceMethod>()
            .FirstOrDefault(m => m.Name == "MethodDelegate");
        if (nextCall == null) continue;
        var action = (GenericInstanceType)nextCall.GenericArguments[0];
        var expected = action.GenericArguments.Select(t => t.FullName).ToList();
        string result = "System.Void";
        if (action.Name.StartsWith("Func")) { result = expected[^1]; expected.RemoveAt(expected.Count - 1); }
        if (!matches[0].IsStatic) { Check(expected[0] == target.FullName, "delegate receiver: " + target.Name); expected.RemoveAt(0); }
        Check(matches[0].ReturnType.FullName == result && expected.SequenceEqual(matches[0].Parameters.Select(p => p.ParameterType.FullName)),
            "delegate signature: " + target.Name + "." + methodName);
        delegates++;
    }

    var attributes = type.CustomAttributes.Concat(method.CustomAttributes).Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch").ToArray();
    if (attributes.Length == 0 || !method.CustomAttributes.Any(a => a.AttributeType.Name is "HarmonyPrefix" or "HarmonyPostfix" or "HarmonyTranspiler")) continue;
    var values = attributes.SelectMany(a => a.ConstructorArguments).ToArray();
    var patchType = values.Select(a => a.Value).OfType<TypeReference>().FirstOrDefault();
    var patchName = values.Select(a => a.Value).OfType<string>().FirstOrDefault();
    if (patchType == null || patchName == null || !gameTypes.TryGetValue(patchType.FullName, out var patchOwner)) continue;
    var targets = patchOwner.Methods.Where(m => m.Name == patchName).ToArray();
    var parameterTypes = values.FirstOrDefault(a => a.Type.FullName == "System.Type[]").Value as CustomAttributeArgument[];
    if (parameterTypes != null) targets = targets.Where(m => m.Parameters.Select(p => p.ParameterType.FullName)
        .SequenceEqual(parameterTypes.Select(a => ((TypeReference)a.Value).FullName))).ToArray();
    Check(targets.Length == 1, "patch target: " + patchType.Name + "." + patchName);
    if (!method.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyTranspiler"))
    foreach (var parameter in method.Parameters)
    {
        string parameterType = parameter.ParameterType is ByReferenceType byRef ? byRef.ElementType.FullName : parameter.ParameterType.FullName;
        if (parameter.Name.StartsWith("___"))
        {
            var field = Field(patchOwner, parameter.Name[3..]);
            Check(field?.FieldType.FullName == parameterType, "injected patch field: " + parameter.Name);
        }
        else if (!parameter.Name.StartsWith("__"))
            Check(targets[0].Parameters.Any(p => p.Name == parameter.Name && (p.ParameterType.FullName == parameterType ||
                (parameterType == "System.Object" && !p.ParameterType.IsValueType && !p.ParameterType.IsByReference))),
                "patch parameter: " + patchName + "." + parameter.Name);
    }
    patches++;
}
Check(fieldRefs > 150, "all enemy field refs were inspected");
Check(delegates >= 20, "all enemy method delegates were inspected");
Check(patches >= 65, "common and dedicated patches were inspected");
Check(mod.Assembly.Name.Version == new Version(5, 0, 0, 0), "assembly version");
Check((string)ModType("EliteEnemyVariantsPlugin").Fields.Single(f => f.Name == "PluginVersion").Constant == "5.0.0", "plugin version");
Check((int)ModType("EliteEnemyVariantsCompatibilityApi").Fields.Single(f => f.Name == "ApiVersion").Constant == 1, "public compatibility API unchanged");
Check(!mod.AssemblyReferences.Any(r => r.Name is "RoleShuffle" or "StageRoles" or "StagePhysicsEvents"), "no required cross-mod assembly");
Check(!mod.GetTypes().SelectMany(t => t.Methods).Any(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "PunRPC")), "no custom client RPC");
Check(gameTypes.ContainsKey("EnemySnatcher"), "Sunstone game assembly");
Check(mod.GetTypeReferences().Any(t => t.Name == "EnemySnatcher"), "Pony integrated with native Sunstone AI");
var scroll = gameTypes["MenuScrollBox"].Methods.Single(m => m.Name == "Update");
Check(scroll.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "scrollWheelStep"), "native wheel-step integration");
var menuClick = menu.GetMemberReferences().OfType<MethodReference>().Where(m => m.Name == "MenuEffectClick").ToArray();
Check(menuClick.Length > 0 && menuClick.All(m => m.Parameters.Count == 6), "MenuLib uses Sunstone click signature");
string PluginGuid(ModuleDefinition assembly) => (string)assembly.Types.SelectMany(t => t.CustomAttributes)
    .Single(a => a.AttributeType.Name == "BepInPlugin").ConstructorArguments[0].Value;
Check(PluginGuid(menu) == "nickklmao.menulib", "moved MenuLib retains runtime dependency identity");
Check(PluginGuid(config) == "nickklmao.repoconfig", "moved REPOConfig retains runtime dependency identity");
var rugrat = ModType("RugratElitePatches").Methods.Single(m => m.Name == "StateGetValuablePostfix");
Check(rugrat.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "volumeType"), "Rugrat respects native pickup size limit");
Check(rugrat.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "currentState"), "Rugrat waits for native pickup selection");

if (args.Length >= 5)
{
    using var previous = ModuleDefinition.ReadModule(args[4]);
    foreach (string name in new[] { "EliteArchetype", "EliteEnemyConfigMigration", "EliteEnemyVariantsPlugin", "EliteEnemyVariantsCompatibilityApi" })
    {
        var before = previous.Types.Single(t => t.Name == name);
        foreach (var field in before.Fields.Where(f => f.HasConstant && f.Name != "PluginVersion"))
            Check(Equals(field.Constant, ModType(name).Fields.Single(f => f.Name == field.Name).Constant), "preserved constant: " + name + "." + field.Name);
    }
    foreach (string name in new[] { "EliteEnemyConfig", "EliteEnemyConfigMigration", "EliteSelector", "SelectionRollSource" })
    foreach (var before in previous.Types.Single(t => t.Name == name).Methods.Where(m => m.HasBody))
    {
        var after = ModType(name).Methods.Single(m => Signature(m) == Signature(before));
        string Body(MethodDefinition method)
        {
            var instructions = method.Body.Instructions.ToList();
            // The only permitted existing-settings change is the new Pony binding.
            // Normalize labels so appending that binding cannot mask other edits.
            if (name == "EliteEnemyConfig" && method.Name == ".ctor")
            {
                for (int i = instructions.Count - 1; i >= 3; i--)
                {
                    if (instructions[i].Operand is not MethodReference call || call.Name != "AddExpandedChance" ||
                        instructions[i - 1].OpCode != OpCodes.Ldc_I4_S || Convert.ToInt32(instructions[i - 1].Operand) != 29 ||
                        instructions[i - 2].OpCode != OpCodes.Ldarg_1 || instructions[i - 3].OpCode != OpCodes.Ldarg_0) continue;
                    instructions.RemoveRange(i - 3, 4);
                }
            }
            string Operand(object? operand) => operand switch
            {
                Instruction target => "label:" + instructions.IndexOf(target),
                Instruction[] targets => string.Join(",", targets.Select(t => "label:" + instructions.IndexOf(t))),
                _ => operand?.ToString() ?? ""
            };
            return string.Join("\n", instructions.Select(i => i.OpCode.Name + " " + Operand(i.Operand)));
        }
        Check(Body(before) == Body(after), "preserved settings/selection: " + name + "." + before.Name);
    }
    var apiBefore = previous.Types.Single(t => t.Name == "EliteEnemyVariantsCompatibilityApi");
    Check(apiBefore.Methods.Where(m => m.IsPublic).Select(Signature).SequenceEqual(ModType("EliteEnemyVariantsCompatibilityApi").Methods.Where(m => m.IsPublic).Select(Signature)), "public API method contracts retained");
}
Console.WriteLine($"PASS: {checks} compatibility checks; {fieldRefs} runtime fields, {delegates} delegates, {patches} Harmony targets; binary/static validation, not an in-game play test.");
