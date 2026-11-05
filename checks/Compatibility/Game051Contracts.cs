using Mono.Cecil;
using Mono.Cecil.Cil;

// Binary regression checks for the native 0.5.1 fixes. Do not replace those
// systems with EEV patches: vanilla clients must keep the game's own lifecycle.
internal static class Game051Contracts
{
    internal static void Verify(ModuleDefinition game, ModuleDefinition mod, Action<bool, string> check)
    {
        TypeDefinition Native(string name) => game.Types.Single(t => t.Name == name);
        MethodDefinition Method(string type, string name) => Native(type).Methods.Single(m => m.Name == name);
        bool Calls(string type, string method, string owner, string called) => Method(type, method).Body.Instructions
            .Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == owner && m.Name == called);

        check(Calls("EnemySnatcher", "FixedUpdate", "EnemySnatcher", "MountingRiderCollisionTick"),
            "0.5.1 Pony updates rider collision exclusions in its native physics tick");
        check(Calls("EnemySnatcher", "MountingRiderCollisionTick", "EnemySnatcher", "MountingRiderCollisionRestore") &&
              Calls("EnemySnatcher", "MountingRiderCollisionTick", "EnemySnatcher", "MountingRiderCollisionPairsCache"),
            "0.5.1 Pony restores/rebuilds collision pairs when the rider changes");
        check(Calls("EnemySnatcher", "MountingRiderCollisionTick", "Physics", "IgnoreCollision") &&
              Calls("EnemySnatcher", "MountingRiderCollisionRestore", "Physics", "IgnoreCollision"),
            "0.5.1 Pony owns collision exclusion and restoration");
        foreach (string name in new[] { "RiderDismount", "FullReset" })
            check(Calls("EnemySnatcher", name, "EnemySnatcher", "MountingRiderCollisionRestore"),
                "0.5.1 Pony releases rider collision state on " + name);
        check(Calls("EnemySnatcher", "OnDisable", "EnemySnatcher", "FullReset"),
            "0.5.1 Pony disable/pooled respawn clears collision state");
        var anchor = Method("EnemySnatcher", "MountingVisualAnchorSetup").Body.Instructions;
        check(anchor.Any(i => i.Operand is FieldReference f && f.Name == "seatTransform") &&
              anchor.Any(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.Name == "visualAnchor") &&
              !anchor.Any(i => i.Operand is FieldReference f && f.Name == "hipBone"),
            "0.5.1 Pony uses the seat anchor rather than the old hip anchor");

        var setup = Method("PlayerTumble", "SetupRPC");
        check(setup.Parameters.Select(p => p.ParameterType.FullName)
                  .SequenceEqual(new[] { "System.Int32", "Photon.Pun.PhotonMessageInfo" }),
            "0.5.1 tumble setup receives the player view ID");
        check(Calls("PlayerTumble", "SetupRPC", "PhotonView", "Find") &&
              Calls("PlayerTumble", "SetupRPC", "SemiFunc", "MasterOnlyRPC"),
            "0.5.1 tumble setup retains native identity and authority validation");
        foreach (string name in new[] { "TumbleForceRPC", "TumbleTorqueRPC" })
            check(Calls("PlayerTumble", name, "SemiFunc", "MasterAndOwnerOnlyRPC") &&
                  Method("PlayerTumble", name).Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "playerAvatar"),
                "0.5.1 tumble keeps native player/ownership validation on " + name);

        var block = Method("InputManager", "GamepadTextInputBlock");
        check(block.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "System.Single", "System.Boolean" }) &&
              block.Parameters[1].IsOptional && Equals(block.Parameters[1].Constant, false),
            "0.5.1 gamepad text entry supports the optional keyboard allowance");
        foreach (string name in new[] { "KeyDown", "KeyUp", "KeyHold", "GetUINavigate" })
            check(Calls("InputManager", name, "InputManager", "GamepadTextInputBlocked"),
                "0.5.1 native text-entry input gate retained in " + name);
        check(Calls("ChatManager", "GamepadTextInputSubmitted", "ChatManager", "StateSet"),
            "0.5.1 gamepad chat submission uses the native chat state machine");

        // Inspect every declarative EEV patch, not just PonyElitePatches, so a
        // later adapter cannot silently replace the native fixes checked above.
        var protectedPonyMethods = new HashSet<string>
        {
            "FixedUpdate", "MountingVisualAnchorSetup", "MountingRiderCollisionTick",
            "MountingRiderCollisionPairsCache", "MountingRiderCollisionRestore",
            "RiderDismount", "FullReset", "OnDisable"
        };
        foreach (var type in mod.GetTypes())
        foreach (var method in type.Methods)
        {
            if (!method.CustomAttributes.Any(a => a.AttributeType.Name is "HarmonyPrefix" or "HarmonyPostfix" or "HarmonyTranspiler")) continue;
            var arguments = type.CustomAttributes.Concat(method.CustomAttributes)
                .Where(a => a.AttributeType.Name == "HarmonyPatch").SelectMany(a => a.ConstructorArguments).ToArray();
            var owner = arguments.Select(a => a.Value).OfType<TypeReference>().FirstOrDefault();
            var target = arguments.Select(a => a.Value).OfType<string>().FirstOrDefault();
            if (owner == null || target == null) continue;
            check(!(owner.Name == "EnemySnatcher" && protectedPonyMethods.Contains(target)) &&
                  owner.Name is not ("PlayerTumble" or "InputManager"),
                "EEV leaves native 0.5.1 riding/tumble/input fixes intact: " + method.FullName);
            if (owner.Name == "ChatManager")
                check(method.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPostfix") && target == "Update",
                    "EEV chat feedback does not replace native chat processing");
        }
        var pony = mod.Types.Single(t => t.Name == "PonyElitePatches");
        var privateBindings = pony.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
            .Where(i => i.OpCode == OpCodes.Ldstr).Select(i => (string)i.Operand).ToHashSet();
        foreach (string field in new[] { "riderSeatVisualLocalOffset", "riderSeatVisualLocalEuler", "riderCollisionPairs", "riderCollisionTumble" })
            check(!privateBindings.Contains(field), "EEV does not mutate native 0.5.1 rider state: " + field);
    }
}
