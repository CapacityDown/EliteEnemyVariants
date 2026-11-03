using Mono.Cecil;
using Mono.Cecil.Cil;
using REPOJP.EliteEnemyVariants;
using UnityEngine;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks++; }
for (int i = 0; i <= 1000; i++)
{
    float roll = i / 1000f;
    PonyPolicy policy = new(100, roll);
    Check(policy.NextPatrolAt >= 110 && policy.NextPatrolAt <= 115, "10-15s patrol cadence");
    Check(PonyPolicy.PatrolDuration(roll) >= 20 && PonyPolicy.PatrolDuration(roll) <= 30, "20-30s patrol stay");
    Check(!policy.TryPatrol(109.9f, 0, 0), "cannot roll every frame");
    Check(policy.TryPatrol(115, roll, roll) == (roll < 0.5f), "50% independent action lottery");
    Check(!policy.TryPatrol(115, 0, 0), "success and failure both consume lottery opportunity");
    policy.Landed(120);
    Check(policy.GroundReadyAt == 140 && !policy.TryPatrol(139.99f, 0, 0), "20s after ground return");
    Check(policy.TryPatrol(140, 0, 0), "patrol permitted after cooldown");
}
foreach (float invalid in new[] { float.NaN, -1f, float.PositiveInfinity, float.NegativeInfinity })
{
    PonyPolicy policy = new(0, invalid);
    Check(policy.NextPatrolAt >= 10 && policy.NextPatrolAt <= 15, "bounded schedule input");
    Check(!policy.TryPatrol(20, invalid, 0), "invalid probability cannot enable patrol");
}
PonyPolicy pursuit = new(0, 0) { CoverUsed = true, InterceptUsed = true };
pursuit.ObserveCalm(10, false);
Check(pursuit.CoverUsed && pursuit.InterceptUsed, "target switch does not replenish tactics");
pursuit.ObserveCalm(11, true); pursuit.ObserveCalm(13.9f, true);
Check(pursuit.CoverUsed && pursuit.InterceptUsed, "short calm does not replenish tactics");
pursuit.ObserveCalm(14, false); pursuit.ObserveCalm(15, true); pursuit.ObserveCalm(17.9f, true);
Check(pursuit.CoverUsed && pursuit.InterceptUsed, "interrupted reset restarts quiet interval");
pursuit.ObserveCalm(18, true);
Check(!pursuit.CoverUsed && !pursuit.InterceptUsed, "genuine new pursuit permits tactics again");
Check(!new PonyPolicy(50, 0).CoverUsed && !new PonyPolicy(50, 0).InterceptUsed, "respawn creates clean encounter state");
for (int mask = 0; mask < 32; mask++)
{
    bool A(int index) => (mask & (1 << index)) != 0;
    Check(PonyPolicy.CanClearCover(A(0), A(1), A(2), A(3), A(4)) == (mask == 18),
        "cover requires unused, visible, no protection, no held item and ready");
}
for (int mask = 0; mask < 16; mask++)
{
    bool A(int index) => (mask & (1 << index)) != 0;
    Check(PonyPolicy.CanPreferArmed(A(0), A(1), A(2), A(3)) == (mask == 7),
        "armed target respects vision, role eligibility and player protection");
}
Check(PonyPolicy.FlankDistance == 2.5f && PonyPolicy.InterceptLead == 2 &&
    PonyPolicy.InterceptDuration == 3 && PonyPolicy.LandingPause >= 1, "bounded tactical constants");
Check(PonyPolicy.InterceptDistance(2, 0.75f) == 3.5f, "lead accounts for travel during fall");
Check(PonyPolicy.InterceptDistance(20, 2) == 3.75f, "prediction is capped rather than tracking sprint infinitely");
Check(PonyPolicy.InterceptDistance(0, 1) == 2, "base landing gap is retained");
Check(PonyPolicy.CanReuseFlank(true, true, 0, 2.5f), "hold a valid flank point while approaching");
Check(!PonyPolicy.CanReuseFlank(false, true, 0, 1), "new reposition needs a destination");
Check(!PonyPolicy.CanReuseFlank(true, false, 0, 1), "target switch invalidates destination");
Check(!PonyPolicy.CanReuseFlank(true, true, 0.25f, 1), "moving target invalidates destination");
Check(!PonyPolicy.CanReuseFlank(true, true, 0, 2.51f), "cached path still respects travel cap");
Check(!PonyPolicy.CanReuseFlank(true, true, float.NaN, 1), "invalid cached target is rejected");
Check(!PonyPolicy.CanReuseFlank(true, true, 0, float.NaN), "invalid cached path is rejected");

for (int mask = 0; mask < 32; mask++)
{
    bool A(int index) => (mask & (1 << index)) != 0;
    Check(PonyPolicy.CanCapture(A(0), A(1), A(2), A(3), A(4), 0, 0, 0) == (mask == 15),
        "capture requires ceiling, ready, visible, free tongue and unprotected player");
}
foreach (float blocked in new[] { 0.01f, 1f, float.PositiveInfinity, float.NaN })
{
    Check(!PonyPolicy.CanCapture(true, true, true, true, false, blocked, 0, 0), "attack pause retained");
    Check(!PonyPolicy.CanCapture(true, true, true, true, false, 0, blocked, 0), "tongue recovery retained");
    Check(!PonyPolicy.CanCapture(true, true, true, true, false, 0, 0, blocked), "main cooldown retained");
}
PonyPolicy capture = new(0, 0);
Check(!capture.ObserveCeilingCapture(5, false), "no capture does not extend stay");
Check(capture.ObserveCeilingCapture(10, true), "native capture receives fixed finishing window");
Check(capture.ObserveCeilingCapture(19.99f, true), "window can cover native tell/grab/windup");
Check(!capture.ObserveCeilingCapture(20, true), "ten second hard deadline");
Check(!capture.ObserveCeilingCapture(30, true), "continued stuck capture cannot renew deadline");
Check(!capture.ObserveCeilingCapture(31, false) && capture.ObserveCeilingCapture(32, true), "new capture can start a new window");
Check(new PonyPolicy(0, 0).ObserveCeilingCapture(0, true), "respawn starts with no stale deadline");

Vector3 preferred = new(1, 0.5f, 0);
Vector3 chosen = PonyCeilingThrow.Choose(preferred, Vector3.forward, _ => true);
Check(MathF.Abs(chosen.sqrMagnitude - 1) < 0.0001f && chosen.y < 0 && chosen.x > 0.9f, "shallow downward unit vector preserves force magnitude");
int attempts = 0;
chosen = PonyCeilingThrow.Choose(preferred, Vector3.forward, direction => { attempts++; return direction.z > 0.5f; });
Check(attempts <= 8 && chosen.z > 0.5f, "blocked preferred direction selects another corridor");
chosen = PonyCeilingThrow.Choose(preferred, Vector3.forward, direction => direction.y == 0);
Check(chosen.y == 0 && chosen.sqrMagnitude > 0.99f, "low ceiling can use horizontal windup");
attempts = 0;
chosen = PonyCeilingThrow.Choose(preferred, Vector3.forward, _ => { attempts++; return false; });
Check(attempts == 16 && chosen.sqrMagnitude == 0, "bounded search releases without extra force when all corridors are blocked");
foreach (var input in new[] { Vector3.zero, Vector3.up, Vector3.down })
    Check(PonyCeilingThrow.Choose(input, Vector3.up, _ => true).y <= 0, "vertical/zero target offsets never fling upward");
foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    Check(PonyCeilingThrow.Choose(new(bad, 0, 1), Vector3.forward, _ => true).sqrMagnitude == 0, "invalid direction safely releases");
for (int i = 0; i < 360; i++)
{
    Vector3 input = new(MathF.Sin(i), 3, MathF.Cos(i));
    var candidates = new List<Vector3>();
    PonyCeilingThrow.Choose(input, Vector3.forward, d => { candidates.Add(d); return false; });
    Check(candidates.Count == 16 && candidates.All(d => d.y <= 0 && MathF.Abs(d.sqrMagnitude - 1) < 0.0001f), "all candidates bounded, non-upward and normalized");
}
PonyThrowCache throwCache = new();
int choices = 0;
Vector3 Pick() { choices++; return choices == 1 ? Vector3.zero : Vector3.forward; }
Check(throwCache.Resolve(-1, Pick).sqrMagnitude == 0 && choices == 0, "invalid tongue sequence cannot throw");
Check(throwCache.Resolve(7, Pick).sqrMagnitude == 0 && choices == 1, "blocked capture caches zero");
Check(throwCache.Resolve(7, Pick).sqrMagnitude == 0 && choices == 1, "native final-release retry cannot create an untelegraphed fling");
Check(throwCache.Resolve(8, Pick).sqrMagnitude == 1 && choices == 2, "next capture recomputes geometry");
Check(throwCache.Resolve(8, Pick).sqrMagnitude == 1 && choices == 2, "same capture retains direction");
Check(new PonyThrowCache().Resolve(8, Pick).sqrMagnitude == 1 && choices == 3, "respawn resets cached capture");

if (args.Length is < 2 or > 3) throw new ArgumentException("Pass Assembly-CSharp.dll, EliteEnemyVariants.dll, and optionally the previous EEV DLL for a Pony-only change audit.");
using var game = ModuleDefinition.ReadModule(args[0]);
using var mod = ModuleDefinition.ReadModule(args[1]);
TypeDefinition pony = game.Types.Single(t => t.Name == "EnemySnatcher");
TypeDefinition adapter = mod.Types.Single(t => t.Name == "PonyElitePatches");
var ctor = adapter.Methods.Single(m => m.Name == ".cctor");
int bindings = 0;
for (int i = 0; i < ctor.Body.Instructions.Count; i++)
{
    var instruction = ctor.Body.Instructions[i];
    if (instruction.Operand is not GenericInstanceMethod bind || bind.Name != "Bind") continue;
    string methodName = (string)ctor.Body.Instructions[i - 1].Operand;
    MethodDefinition target = pony.Methods.Single(m => m.Name == methodName);
    TypeReference signature = bind.GenericArguments[0];
    string result;
    List<string> parameters;
    if (signature is GenericInstanceType generic)
    {
        parameters = generic.GenericArguments.Select(p => p.FullName).ToList();
        result = "System.Void";
        if (generic.Name.StartsWith("Func")) { result = parameters[^1]; parameters.RemoveAt(parameters.Count - 1); }
    }
    else
    {
        var invoke = adapter.NestedTypes.Single(t => t.FullName == signature.FullName).Methods.Single(m => m.Name == "Invoke");
        result = invoke.ReturnType.FullName;
        parameters = invoke.Parameters.Select(p => p.ParameterType.FullName).ToList();
    }
    Check(parameters[0] == "EnemySnatcher", "open delegate receiver " + methodName);
    parameters.RemoveAt(0);
    Check(target.ReturnType.FullName == result && target.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters),
        "native decision delegate " + methodName);
    bindings++;
}
Check(bindings >= 17, "all Pony method bindings validated");
var retry = pony.NestedTypes.Single(t => t.Name == "TongueRetry");
foreach (string field in new[] { "firstOffset", "lastOffset" })
    Check(retry.Fields.Single(f => f.Name == field).FieldType.FullName == "UnityEngine.Vector3", "native retry offsets " + field);
foreach (string field in new[] { "tongueTargetType", "tongueCommandState" })
{
    var enumType = pony.NestedTypes.Single(t => t.FullName == pony.Fields.Single(f => f.Name == field).FieldType.FullName);
    Check((int)enumType.Fields.Single(f => f.Name == (field == "tongueTargetType" ? "None" : "Released")).Constant == 0,
        "native tongue-free sentinel " + field);
}
var patches = adapter.Methods.Where(m => m.CustomAttributes.Any(a => a.AttributeType.Name is "HarmonyPrefix" or "HarmonyPostfix")).ToArray();
Check(patches.Length == 9, "all nine decision/direction hooks present");
foreach (var patch in patches)
    Check(patch.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "Try" && m.DeclaringType.Name == adapter.Name),
        "host/spawn/enhancement gate " + patch.Name);
var gateCalls = adapter.Methods.Single(m => m.Name == "Try").Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
Check(gateCalls.Any(m => m.Name == "IsMasterClientOrSingleplayer"), "mandatory host gate");
Check(gateCalls.Any(m => m.Name == "TryGet" && m.DeclaringType.Name == "EliteRegistry"), "only selected enhanced instances");
var boundNames = ctor.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr).Select(i => (string)i.Operand).ToArray();
foreach (string forbidden in new[] { "TongueAttackCooldownsReset", "TongueCommandPlayer", "SetStateGrabPhysObjectNow", "SetStateTongueAttackTellSend", "PlayerHurt" })
    Check(!boundNames.Contains(forbidden), "no bypass of native telegraph/ownership/recovery: " + forbidden);
Check(boundNames.Contains("SetStateGrabPhysObject") && boundNames.Contains("CeilingScareLanding"), "native tell and landing validation reused");
Check(boundNames.Contains("SetStateGrab") && boundNames.Contains("TongueAttackWithinRange") && boundNames.Contains("TonguePlayerFlingReady"),
    "ceiling capture reuses native tell, 3D reach and confirmed ownership");
Check(!boundNames.Contains("SetStateGrabNow") && !boundNames.Contains("TumbleForce"), "no immediate capture or additional player impulse");
var targetEnum = pony.NestedTypes.Single(t => t.Name == "TongueTargetType");
Check((int)targetEnum.Fields.Single(f => f.Name == "Player").Constant == 1, "native player target sentinel");
string[] Calls(TypeDefinition type, string method) => type.Methods.Single(m => m.Name == method).Body.Instructions
    .Select(i => i.Operand).OfType<MethodReference>().Select(m => m.Name).ToArray();
Check(Calls(adapter, "InterceptPrefix").Contains("TryCeilingCapture"), "capture considered before interception");
Check(Calls(adapter, "PatrolStayPrefix").Contains("ObserveCeilingCapture") && Calls(adapter, "PatrolStayPrefix").Contains("Ready"),
    "bounded ceiling extension preserves interrupt gate");
Check(Calls(adapter, "ThrowSegmentClear").Contains("SphereCastNonAlloc") && Calls(adapter, "ThrowSegmentClear").Contains("OverlapSphereNonAlloc"),
    "throw corridor checks swept volume and start overlap");
Check(Calls(pony, "PlayerReleaseImpulsePrepare").Contains("TongueFlingDirection"), "native player throw consumes patched direction");
Check(Calls(pony, "SetStateGrab").Contains("SetStateTongueAttackTellPlayer"), "native entry retains warning");
Check(Calls(pony, "PlayerReleaseImpulseApply").Count(n => n == "TumbleForce") == 1, "native throw has single impulse path");
Check(pony.Methods.Single(m => m.Name == "PlayerReleaseImpulseApply").Body.Instructions.Any(i =>
    i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.Name == "tonguePlayerThrowSequence"), "native sequence deduplication retained");
Check(Calls(pony, "StateTongueRelease").Contains("PlayerAttackCompleteState"), "native recovery and player protection retained");
Check(!adapter.Methods.SelectMany(m => m.HasBody ? m.Body.Instructions : Enumerable.Empty<Instruction>()).Any(i =>
    i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.DeclaringType.Name == "EnemySnatcher"),
    "no permanent vanilla tuning/stat mutation");
Check((int)mod.Types.Single(t => t.Name == "EliteArchetype").Fields.Single(f => f.Name == "Pony").Constant == 29,
    "append-only Pony identity preserves older bits");
if (args.Length == 3)
{
    using var previous = ModuleDefinition.ReadModule(args[2]);
    var currentTypes = mod.GetTypes().ToDictionary(t => t.FullName);
    int preserved = 0;
    foreach (var type in previous.GetTypes())
    {
        var root = type; while (root.DeclaringType != null) root = root.DeclaringType;
        if (root.Name is "PonyElitePatches" or "PonyPolicy" or "PonyCeilingThrow" or "PonyThrowCache" or "EliteGuideCatalog" || root.Name.StartsWith('<')) continue;
        foreach (var method in type.Methods.Where(m => m.HasBody))
        {
            var current = currentTypes[type.FullName].Methods.Single(m => m.FullName == method.FullName);
            string Code(MethodDefinition m) => string.Join("\n", m.Body.Instructions.Select(i => i.OpCode.Code + ":" + i.Operand));
            Check(Code(method) == Code(current), "unchanged non-Pony behavior/UI: " + method.FullName);
            preserved++;
        }
    }
    Console.WriteLine($"PASS: {preserved} existing non-Pony method bodies unchanged.");
}
Console.WriteLine($"PASS: {checks} Pony policy/contract checks; not an in-game multiplayer test.");
