using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mono.Cecil;
using REPOJP.EliteEnemyVariants;
using UnityEngine;

static class Program
{
    private static readonly Type PatchType = typeof(EnemyOrbRewardPatches);
    private static int _checks;
    private static object? Call(string method, params object?[] arguments) =>
        PatchType.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments);
    private static void Check(bool result, string name)
    {
        if (!result) throw new InvalidOperationException(name);
        _checks++;
    }
    private static (GameObject Orb, ValuableObject Value, EnemyParent Parent) Orb(bool enhanced = true, bool enemyOrb = true)
    {
        GameObject orb = new();
        ValuableObject value = new();
        orb.Components[typeof(ValuableObject)] = value;
        if (enemyOrb) orb.Components[typeof(EnemyValuable)] = new EnemyValuable();
        EnemyParent parent = new();
        parent.Components[typeof(Enemy)] = new Enemy { Enhanced = enhanced };
        return (orb, value, parent);
    }
    private static void Initialize(ValuableObject value)
    {
        value.DollarValueSetLogic();
        Call("DollarValueSetLogicPostfix", value);
    }

    public static void Main(string[] args)
    {
        // HarmonyX 2.7 targets the game's Mono runtime; use its Cecil emitter on .NET 9.
        Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", "cecil");
        foreach (float amount in new[] { 0f, 100f, 1100f, 5500f, 18000f })
        {
            var drop = Orb();
            drop.Value.RolledValue = amount;
            Check(ReferenceEquals(drop.Orb, Call("RegisterSpawnedOrb", drop.Orb, drop.Parent)), "factory result preserved");
            Call("DollarValueSetLogicPostfix", drop.Value);
            Check(!drop.Value.dollarValueSet, "waits for vanilla value initialization");
            Initialize(drop.Value);
            Check(drop.Value.dollarValueOriginal == amount * 1.5f && drop.Value.dollarValueCurrent == amount * 1.5f,
                "exact 1.5x for both initial and current reward");
            Call("RegisterSpawnedOrb", drop.Orb, drop.Parent);
            Initialize(drop.Value);
            Check(drop.Value.dollarValueCurrent == amount * 1.5f, "duplicate registration and initialization are idempotent");
            drop.Value.dollarValueCurrent *= 0.5f;
            Initialize(drop.Value);
            Check(drop.Value.dollarValueCurrent == amount * 0.75f, "subsequent value loss is preserved");
        }
        foreach (var drop in new[] { Orb(enhanced: false), Orb(enemyOrb: false) })
        {
            Call("RegisterSpawnedOrb", drop.Orb, drop.Parent);
            Initialize(drop.Value);
            Check(drop.Value.dollarValueCurrent == 1000f, "ordinary enemy or non-orb valuable unchanged");
        }
        var bonus = Orb();
        Initialize(bonus.Value);
        Check(bonus.Value.dollarValueCurrent == 1000f, "separately spawned reward is unchanged");
        var disabled = Orb();
        EliteEnemyVariantsPlugin.Instance.Settings.Enabled.Value = false;
        Call("RegisterSpawnedOrb", disabled.Orb, disabled.Parent);
        EliteEnemyVariantsPlugin.Instance.Settings.Enabled.Value = true;
        Initialize(disabled.Value);
        Check(disabled.Value.dollarValueCurrent == 1000f, "disabled mod does not mark drops");
        var client = Orb();
        SemiFunc.Host = false;
        Call("RegisterSpawnedOrb", client.Orb, client.Parent);
        Initialize(client.Value);
        SemiFunc.Host = true;
        Initialize(client.Value);
        Check(client.Value.dollarValueCurrent == 1000f, "client does not mark drops");
        var hostLoss = Orb();
        Call("RegisterSpawnedOrb", hostLoss.Orb, hostLoss.Parent);
        SemiFunc.Host = false;
        Initialize(hostLoss.Value);
        Check(hostLoss.Value.dollarValueCurrent == 1000f, "non-host cannot change marked value");
        SemiFunc.Host = true;
        Initialize(hostLoss.Value);
        Check(hostLoss.Value.dollarValueCurrent == 1500f, "host can finish pending reward");

        if (args.Length != 1) throw new ArgumentException("Pass the installed game's Assembly-CSharp.dll path.");
        using ModuleDefinition game = ModuleDefinition.ReadModule(args[0]);
        MethodDefinition despawn = game.Types.Single(t => t.Name == "EnemyParent").Methods.Single(m => m.Name == "Despawn");
        Dictionary<short, OpCode> codes = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(c => c.Value);
        MethodInfo local = typeof(UnityEngine.Object).GetMethods().Single(m => m.Name == "Instantiate").MakeGenericMethod(typeof(GameObject));
        MethodInfo network = typeof(Photon.Pun.PhotonNetwork).GetMethod("InstantiateRoomObject")!;
        List<CodeInstruction> original = new();
        foreach (Mono.Cecil.Cil.Instruction instruction in despawn.Body.Instructions)
        {
            object? operand = instruction.Operand;
            if (operand is MethodReference method && method.Name == "Instantiate" && method.DeclaringType.FullName == "UnityEngine.Object")
            {
                Check(method is GenericInstanceMethod generic && generic.GenericArguments.Single().FullName == "UnityEngine.GameObject" &&
                    method.Parameters.Count == 3 && method.Parameters[1].ParameterType.FullName == "UnityEngine.Vector3" &&
                    method.Parameters[2].ParameterType.FullName == "UnityEngine.Quaternion", "installed local factory signature");
                operand = local;
            }
            else if (operand is MethodReference photon && photon.Name == "InstantiateRoomObject")
            {
                Check(photon.DeclaringType.FullName == "Photon.Pun.PhotonNetwork" && photon.ReturnType.FullName == "UnityEngine.GameObject" &&
                    photon.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[]
                    { "System.String", "UnityEngine.Vector3", "UnityEngine.Quaternion", "System.Byte", "System.Object[]" }), "installed network factory signature");
                operand = network;
            }
            original.Add(new CodeInstruction(codes[instruction.OpCode.Value], operand));
        }
        List<CodeInstruction> patched = ((IEnumerable<CodeInstruction>)Call("DespawnTranspiler", original)!).ToList();
        Check(patched.Count == original.Count + 4, "installed game gets exactly two reward registrations");
        Check(patched.Where(original.Contains).SequenceEqual(original), "all original death checks and instructions preserved");
        foreach (MethodInfo factory in new[] { local, network })
        {
            int index = patched.FindIndex(i => Equals(i.operand, factory));
            Check(index >= 0 && patched[index + 1].opcode == OpCodes.Ldarg_0 &&
                patched[index + 2].operand is MethodInfo register && register.Name == "RegisterSpawnedOrb" &&
                patched[index + 3].opcode == OpCodes.Pop, "factory result consumed before original pop");
        }
        foreach (var unsupported in new[]
        {
            original.Where(i => !Equals(i.operand, network)).ToList(),
            original.Concat(new[] { new CodeInstruction(OpCodes.Call, local), new CodeInstruction(OpCodes.Pop) }).ToList()
        })
        {
            var unchanged = (IEnumerable<CodeInstruction>)Call("DespawnTranspiler", unsupported)!;
            Check(unchanged.SequenceEqual(unsupported), "unrecognized spawn path is left unchanged");
        }
        Check(EliteEnemyVariantsPlugin.ModLogger.Warnings == 1, "unsupported path is reported only once");

        TypeDefinition valuable = game.Types.Single(t => t.Name == "ValuableObject");
        foreach (string field in new[] { "dollarValueSet", "dollarValueOriginal", "dollarValueCurrent" })
            Check(valuable.Fields.Any(f => f.Name == field), "installed valuable field: " + field);
        MethodDefinition coroutine = valuable.NestedTypes.Single(t => t.Name.StartsWith("<DollarValueSet>"))
            .Methods.Single(m => m.Name == "MoveNext");
        var body = coroutine.Body.Instructions;
        int initAt = body.ToList().FindLastIndex(i => i.Operand is MethodReference m && m.Name == "DollarValueSetLogic");
        int syncAt = body.ToList().FindIndex(i => Equals(i.Operand, "DollarValueSetRPC"));
        int haulAt = body.ToList().FindIndex(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Stfld && i.Operand is FieldReference f && f.Name == "haulGoalMax");
        Check(initAt >= 0 && syncAt > initAt && haulAt > syncAt, "host value initialization precedes vanilla synchronization and haul accounting");
        Console.WriteLine($"PASS: {_checks} checks; reward logic uses game stubs, drop and synchronization contracts use the installed game assembly.");
    }
}
