using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using REPOJP.EliteEnemyVariants;
using UnityEngine;
using UnityEngine.AI;

static class Program
{
    private static int Checks;
    private static void Check(bool condition,string name)
    { if(!condition) throw new InvalidOperationException(name); Checks++; }
    private static EliteState Reset()
    {
        SemiFunc.Host=true;SemiFunc.Occupied=false;SemiFunc.Players.Clear();SemiFunc.Points.Clear();
        Physics.Loot.Clear();Physics.Floor=true;Physics.Blocked=(_,_)=>false;
        NavMesh.Reachable=true;NavMesh.Snap=p=>p;Time.time=0f;
        EliteEnemyVariantsPlugin.Instance.Settings.Enabled.Value=true;
        EliteRegistry.Enhanced=true;
        return EliteRegistry.State=new EliteState();
    }
    private static PlayerAvatar Player(float x,float z)
    {
        PlayerAvatar p=new();p.transform.position=new(x,0,z);SemiFunc.Players.Add(p);return p;
    }
    private static ValuableObject Loot(float x,float z,float value=1000f)
    {
        ValuableObject v=new(){Value=value};v.Phys.transform.position=new(x,0,z);
        Physics.Loot.Add(new Collider{Valuable=v});return v;
    }
    private static void Point(float x,float z)
    { LevelPoint p=new();p.transform.position=new(x,0,z);SemiFunc.Points.Add(p); }
    private static IEnumerable<TypeDefinition> WithNested(TypeDefinition type)
    {
        yield return type;
        foreach(TypeDefinition nested in type.NestedTypes)
        foreach(TypeDefinition child in WithNested(nested)) yield return child;
    }
    public static void Main(string[] args)
    {
        Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE","cecil");
        EliteState state=Reset();PlayerAvatar player=Player(2,0);
        Check(TacticalPositioning.IsActive(state.Enemy,out _),"host enhanced active");
        SemiFunc.Host=false;Check(!TacticalPositioning.IsActive(state.Enemy,out _),"client excluded");
        SemiFunc.Host=true;EliteRegistry.Enhanced=false;Check(!TacticalPositioning.IsActive(state.Enemy,out _),"normal excluded");
        EliteRegistry.Enhanced=true;state.Spawned=false;Check(!TacticalPositioning.IsActive(state.Enemy,out _),"despawn excluded");
        state.Spawned=true;state.AdapterTickFaulted=true;Check(!TacticalPositioning.IsActive(state.Enemy,out _),"failed adapter excluded");
        state.AdapterTickFaulted=false;EliteEnemyVariantsPlugin.Instance.Settings.Enabled.Value=false;
        Check(!TacticalPositioning.IsActive(state.Enemy,out _),"disabled config excluded");

        state=Reset();player=Player(2,0);
        Check(!TacticalPositioning.AnimalDestination(state,player,new(),out _),"empty room retains vanilla");
        Loot(4,0);Loot(4,1);Loot(4,-1);Loot(-4,0,6000);
        TacticalRuntimeState animal=new();
        Check(TacticalPositioning.AnimalDestination(state,player,animal,out Vector3 destination)&&destination.x>0,"Animal prioritizes cargo cluster");
        Physics.Loot.Clear();Loot(-4,0);Time.time=1.1f;
        Check(TacticalPositioning.AnimalDestination(state,player,animal,out destination)&&destination.x<0,"moving loot redirects Animal");
        NavMesh.Reachable=false;Time.time=2.2f;
        Check(!TacticalPositioning.AnimalDestination(state,player,animal,out _),"Animal rejects partial paths");
        NavMesh.Reachable=true;Physics.Loot[0].Valuable.Phys.Impact.Indestructible=true;Time.time=3.3f;
        Check(!TacticalPositioning.AnimalDestination(state,player,animal,out _),"Animal ignores protected valuables");

        state=Reset();PlayerAvatar captive=Player(5,0);Point(0,10);
        Vector3 oldDestination=new(15,0,0);TacticalRuntimeState hidden=new();
        Check(!TacticalPositioning.EvadeRescuer(state,captive,oldDestination,hidden,out _),"captive is not a rescuer");
        PlayerAvatar rescuer=Player(5,0);Time.time=1f;rescuer.Protected=true;
        Check(!TacticalPositioning.EvadeRescuer(state,captive,oldDestination,hidden,out _),"Hidden respects protected player");
        rescuer.Protected=false;Time.time=2f;Physics.Blocked=(_,_)=>true;
        Check(!TacticalPositioning.EvadeRescuer(state,captive,oldDestination,hidden,out _),"Hidden does not detect rescuer through wall");
        Physics.Blocked=(_,_)=>false;Time.time=3f;Point(10,0);
        Check(TacticalPositioning.EvadeRescuer(state,captive,oldDestination,hidden,out destination)&&destination.z>0,"Hidden avoids blocked escape corridor");
        Time.time=4f;
        Check(!TacticalPositioning.EvadeRescuer(state,captive,oldDestination,hidden,out _),"one rescue evasion per capture");
        Check(TacticalPositioning.EvadeRescuer(state,captive,oldDestination,new(),out _),"new capture resets evasion budget");
        rescuer.transform.position=new(-5,0,0);
        Check(!TacticalPositioning.EvadeRescuer(state,captive,oldDestination,new(),out _),"rescuer behind does not trigger");

        state=Reset();player=Player(1,0);EnemyFloater mentalist=new();
        TacticalRuntimeState positioning=new();
        positioning.BeginReposition(player,new Vector3(3,0,0),10f);
        Check(positioning.Repositioning&&positioning.PreparationUsed&&positioning.RepositionTarget==player&&
            positioning.RepositionDeadline==12f,"Mentalist position preparation preserves target and has a 2-second deadline");
        Check(!TacticalPositioning.MentalistPosition(state,mentalist,player,out _),"Mentalist stays when moving offers no advantage");
        Loot(5,0);Loot(5,1);Loot(5,-1);
        Check(TacticalPositioning.MentalistPosition(state,mentalist,player,out destination)&&destination.x>0&&destination.magnitude<=3.01f,
            "Mentalist moves a bounded distance to include loot");
        player.Protected=true;
        Check(!TacticalPositioning.MentalistPosition(state,mentalist,player,out _),"Mentalist respects protected target");
        player.Protected=false;Physics.Blocked=(_,_)=>true;
        Check(!TacticalPositioning.MentalistPosition(state,mentalist,player,out _),"Mentalist does not position toward unseen target");
        Physics.Blocked=(_,_)=>false;NavMesh.Reachable=false;
        Check(!TacticalPositioning.MentalistPosition(state,mentalist,player,out _),"Mentalist rejects partial paths");
        NavMesh.Reachable=true;Physics.Floor=false;
        Check(!TacticalPositioning.MentalistPosition(state,mentalist,player,out _),"Mentalist rejects unsupported floor");

        state=Reset();player=Player(0,0);player.rbVelocity=new(0,0,2);Point(0,-16);Point(0,16);
        Check(TacticalPositioning.LoomPosition(state,player,out destination)&&destination.z>0,"Loom prioritizes travel direction");
        PlayerAvatar nearby=Player(0,15);
        Check(TacticalPositioning.LoomPosition(state,player,out destination)&&destination.z<0,"Loom avoids spawning beside any player");
        nearby.Disabled=true;
        Check(TacticalPositioning.LoomPosition(state,player,out destination)&&destination.z>0,"dead player does not block candidate");
        SemiFunc.Points.Clear();Point(0,7);
        Check(!TacticalPositioning.LoomPosition(state,player,out _),"Loom enforces 8m minimum after sampling");
        SemiFunc.Points.Clear();Point(0,31);
        Check(!TacticalPositioning.LoomPosition(state,player,out _),"Loom enforces 30m maximum after sampling");
        SemiFunc.Points.Clear();Point(0,16);NavMesh.Reachable=false;
        Check(!TacticalPositioning.LoomPosition(state,player,out _),"Loom needs route from arrival to target");
        NavMesh.Reachable=true;SemiFunc.Occupied=true;
        Check(!TacticalPositioning.LoomPosition(state,player,out _),"Loom rejects occupied destination");
        SemiFunc.Occupied=false;SemiFunc.Host=false;
        Check(!TacticalPositioning.LoomPosition(state,player,out _),"Loom client cannot position");
        Check(!TacticalPositioning.MentalistPosition(state,mentalist,player,out _),"Mentalist client cannot position");
        Check(!TacticalPositioning.EvadeRescuer(state,player,oldDestination,new(),out _),"Hidden client cannot position");
        Check(!TacticalPositioning.AnimalDestination(state,player,new(),out _),"Animal client cannot position");

        state=Reset();player=Player(6,0);
        Check(!TacticalPositioning.ThrowingFlank(state,player,out _),"Rugrat uses vanilla when throw line clear");
        Physics.Blocked=(from,to)=>MathF.Abs(from.z)<0.1f;
        Check(TacticalPositioning.ThrowingFlank(state,player,out destination)&&MathF.Abs(destination.z)>=2f,
            "Rugrat moves sideways to a clear throwing angle");
        player.Protected=true;
        Check(!TacticalPositioning.ThrowingFlank(state,player,out _),"Rugrat respects protected target");
        player.Protected=false;NavMesh.Reachable=false;
        Check(!TacticalPositioning.ThrowingFlank(state,player,out _),"Rugrat rejects incomplete flank path");
        TacticalRuntimeState flank=new();flank.FlankAttempted=true;flank.BeginFlank(new(4,0,0));
        Check(flank.ContinueFlank(Vector3.zero),"flank starts");
        Time.time=2f;Check(!flank.ContinueFlank(Vector3.zero),"flank deadline does not extend itself");
        Check(flank.FlankAttempted,"flank expiry preserves attempt budget");
        flank.ResetFlank();Check(!flank.FlankAttempted&&!flank.FlankMoving,"new throw resets flank budget");
        flank.BeginFlank(new(4,0,0));Check(!flank.ContinueFlank(new(3.5f,0,0)),"flank stops on arrival");

        state=Reset();TacticalRuntimeState recovery=new();PlayerDeathHead head=new();
        Check(recovery.RememberDroppedHead(head,Vector3.zero),"Headgrab records stun drop");
        Check(TacticalPositioning.RecoverableHead(recovery),"Headgrab can recover unattended head");
        Check(!recovery.RememberDroppedHead(head,Vector3.zero),"same head cannot restart retry budget");
        head.physGrabObject.playerGrabbing.Add(new());
        Check(!TacticalPositioning.RecoverableHead(recovery),"Headgrab yields to rescuer holding head");
        head.physGrabObject.playerGrabbing.Clear();head.transform.position=new(5,0,0);
        Check(!TacticalPositioning.RecoverableHead(recovery),"Headgrab abandons carried-away head");
        head.transform.position=Vector3.zero;head.playerAvatar.Disabled=false;
        Check(!TacticalPositioning.RecoverableHead(recovery),"Headgrab does not target revived player's head");
        head.playerAvatar.Disabled=true;head.overrideSpectated=true;
        Check(!TacticalPositioning.RecoverableHead(recovery),"Headgrab does not steal another monster's head");
        head.overrideSpectated=false;Time.time=30;
        Check(!TacticalPositioning.RecoverableHead(recovery),"Headgrab pending recovery expires");
        recovery=new();Check(recovery.RememberDroppedHead(head,Vector3.zero),"real respawn resets recovery budget");

        state=Reset();Player(5,0);Point(0,10);TacticalRuntimeState tick=new();
        Check(TacticalPositioning.EvadePursuer(state,new(15,0,0),tick,out destination)&&destination.z>0,
            "Tick evades a pursuer ahead without needing a captive");
        Time.time=1f;Check(!TacticalPositioning.EvadePursuer(state,new(15,0,0),tick,out _),"Tick evades only once while full");
        Check(!TacticalPositioning.EvadeRescuer(state,null,new(15,0,0),new(),out _),"Hidden still requires a captive");

        state=Reset();player=Player(0,0);player.rbVelocity=new(2,0,0);TacticalRuntimeState ambush=new();
        TacticalPositioning.RememberSeenPlayer(ambush,player);Point(-8,0);Point(8,0);Point(0,8);
        SemiFunc.Points[0].ModuleConnect=true;SemiFunc.Points[1].ModuleConnect=true;
        Check(TacticalPositioning.AmbushExit(state,ambush,SemiFunc.Points,out LevelPoint? chosen)&&chosen==SemiFunc.Points[1],
            "Heart Hugger favors an exit ahead of last seen movement");
        player.transform.position=new(-20,0,0);
        Check(TacticalPositioning.AmbushExit(state,ambush,SemiFunc.Points,out chosen)&&chosen==SemiFunc.Points[1],
            "Heart Hugger does not track unseen live position for ranking");
        Player(8,0);
        Check(TacticalPositioning.AmbushExit(state,ambush,SemiFunc.Points,out chosen)&&chosen==SemiFunc.Points[0],
            "Heart Hugger rejects emergence beside living players");
        Time.time=13;Check(!TacticalPositioning.AmbushExit(state,ambush,SemiFunc.Points,out _),"Heart Hugger forgets stale sightings");
        Time.time=0;SemiFunc.Points[0].ModuleConnect=false;SemiFunc.Points[1].ModuleConnect=false;
        Check(!TacticalPositioning.AmbushExit(state,ambush,SemiFunc.Points,out _),"Heart Hugger retains vanilla when no exit qualifies");
        SemiFunc.Points[0].Room=new();SemiFunc.Points[0].ConnectedPoints.Add(new LevelPoint{Room=new()});
        Check(TacticalPositioning.AmbushExit(state,ambush,SemiFunc.Points,out chosen)&&chosen==SemiFunc.Points[0],
            "Heart Hugger recognizes connections to another room");

        state=Reset();player=Player(0,0);state.Enemy.transform.position=new(0,0,5);Vector3 bomb=new(8,0,0);
        Check(TacticalPositioning.BombPincer(state,player,bomb,out destination)&&destination.x<0,
            "Cleanup Crew moves opposite bomb while keeping safe path clearance");
        Check(TacticalPositioning.SafeBombRoute(state,bomb,destination),"Cleanup Crew checks route safety");
        Check(!TacticalPositioning.SafeBombRoute(state,new(0,0,5),destination),"Cleanup Crew aborts as bomb enters path");
        Check(!TacticalPositioning.BombPincer(state,player,new(0,0,4),out _),"Cleanup Crew keeps vanilla retreat near bomb");
        Physics.Blocked=(_,_)=>true;
        Check(!TacticalPositioning.BombPincer(state,player,bomb,out _),"Cleanup Crew does not flank toward unseen player");

        state=Reset();Check(!TacticalPositioning.SweepCorner(state,Vector3.zero,out _),"Reaper stops in open room with no corner");
        Physics.Blocked=(_,_)=>true;
        Check(TacticalPositioning.SweepCorner(state,Vector3.zero,out destination)&&destination.magnitude<=4f,
            "Reaper sweeps one reachable nearby occluded corner");
        TacticalRuntimeState sweep=new(){SweepUsed=true,Sweeping=true,SweepPoint=destination};
        Check(sweep.SweepUsed&&sweep.Sweeping&&sweep.SweepPoint.magnitude<=4f,"Reaper stores one bounded sweep destination");
        sweep=new();Check(!sweep.SweepUsed&&!sweep.Sweeping,"real respawn clears Reaper sweep flags");
        NavMesh.Reachable=false;Check(!TacticalPositioning.SweepCorner(state,Vector3.zero,out _),"Reaper rejects blocked route");
        NavMesh.Reachable=true;SemiFunc.Host=false;
        Check(!TacticalPositioning.SweepCorner(state,Vector3.zero,out _),"Reaper client cannot select sweep");
        Check(!TacticalPositioning.ThrowingFlank(state,player,out _),"Rugrat client cannot flank");
        Check(!TacticalPositioning.BombPincer(state,player,bomb,out _),"Cleanup Crew client cannot flank");
        Check(!TacticalPositioning.AmbushExit(state,ambush,SemiFunc.Points,out _),"Heart Hugger client cannot select exit");
        Check(!TacticalPositioning.EvadePursuer(state,new(15,0,0),new(),out _),"Tick client cannot evade");

        if(args.Length!=2)throw new ArgumentException("Pass installed Assembly-CSharp.dll and built EliteEnemyVariants.dll.");
        using ModuleDefinition game=ModuleDefinition.ReadModule(args[0]);
        using ModuleDefinition mod=ModuleDefinition.ReadModule(args[1]);
        string[] relevant={"AnimalElitePatches","HiddenElitePatches","MentalistElitePatches","LoomElitePatches","TacticalPositioning",
            "RugratElitePatches","HeadgrabElitePatches","TickElitePatches","HeartHuggerElitePatches","CleanupCrewElitePatches","ReaperElitePatches"};
        foreach(TypeDefinition type in mod.Types.Where(t=>relevant.Contains(t.Name)).SelectMany(WithNested))
        foreach(MethodDefinition method in type.Methods.Where(m=>m.HasBody))
        {
            var code=method.Body.Instructions;
            for(int i=0;i<code.Count;i++)
            {
                if(code[i].Operand is GenericInstanceMethod fieldRef&&fieldRef.Name=="FieldRefAccess"&&
                    fieldRef.GenericArguments.Count==2&&i>0&&code[i-1].Operand is string name)
                {
                    TypeDefinition? owner=game.Types.SingleOrDefault(t=>t.FullName==fieldRef.GenericArguments[0].FullName);
                    Check(owner!=null&&owner.Fields.Any(f=>f.Name==name&&f.FieldType.FullName==fieldRef.GenericArguments[1].FullName),
                        "runtime field contract "+fieldRef.GenericArguments[0].Name+"."+name);
                }
                if(code[i].Operand is FieldReference field&&field.DeclaringType.Scope.Name=="Assembly-CSharp")
                {
                    TypeDefinition? owner=game.Types.SingleOrDefault(t=>t.FullName==field.DeclaringType.FullName);
                    Check(owner==null||owner.Fields.Single(f=>f.Name==field.Name).IsPublic,"no direct private field access "+field.FullName);
                }
            }
        }
        TypeDefinition patch=mod.Types.Single(t=>t.Name=="MentalistElitePatches");
        foreach(string name in new[]{"UpdateStatePrefix","StateGoToPlayerPrefix"})
        {
            MethodDefinition method=patch.Methods.Single(m=>m.Name==name);
            Check(method.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyPrefix"),"Mentalist prefix installed "+name);
            Check(method.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="IsActive"),"Mentalist prefix host gate "+name);
        }
        Check(game.Types.Single(t=>t.Name=="EnemyFloater").Methods.Single(m=>m.Name=="UpdateState").Parameters[0].Name=="_state",
            "Mentalist transition prefix parameter contract");
        Check(mod.Types.Single(t=>t.Name=="TacticalPositioning").Methods.Where(m=>m.HasBody).All(m=>
            m.Body.Instructions.All(i=>i.Operand is not MethodReference call||call.Name!="RPC")),"tactics require no new RPC");
        foreach(var contract in new[]{
            ("EnemyValuableThrower","StateTargetPlayer"), ("EnemyHeadGrabber","DeathHeadRelease"),
            ("EnemyHeadGrabber","StateGotoHead"), ("EnemyHeadGrabber","StateGotoHeadOver"), ("EnemyHeadGrabber","StateGrabHead"),
            ("EnemyHeadGrabber","UpdateHeadTarget"), ("EnemyHeadGrabber","UpdateState"),
            ("EnemyHeartHugger","OnVision"), ("EnemyHeartHugger","SetPositionToRandomLevelPoint"),
            ("EnemyBombThrower","StateBackAwayHead"), ("EnemyRunner","StateAttackPlayer"),
            ("EnemyNavMeshAgent","GetDestination"), ("EnemyNavMeshAgent","HasPath")})
            Check(game.Types.Single(t=>t.Name==contract.Item1).Methods.Count(m=>m.Name==contract.Item2)==1,
                "installed game method contract "+contract.Item1+"."+contract.Item2);
        TypeDefinition tickPatch=mod.Types.Single(t=>t.Name=="TickElitePatches");
        Check(tickPatch.Methods.Where(m=>m.HasBody).All(m=>m.Body.Instructions.All(i=>
            i.OpCode.Code!=Mono.Cecil.Cil.Code.Stfld||i.Operand is not FieldReference f||f.Name!="hasBeenFull")),
            "Tick never rewrites fullness or its reward flag");
        TypeDefinition support=mod.Types.Single(t=>t.Name=="DedicatedBehaviorSupport");
        Check(support.Methods.Single(m=>m.Name=="OnSpawn").Body.Instructions.Any(i=>
            i.OpCode.Code==Mono.Cecil.Cil.Code.Newobj&&i.Operand is MethodReference m&&m.DeclaringType.Name=="DedicatedEnemyRuntimeState"),
            "real spawn allocates fresh per-enemy tactical state");
        Console.WriteLine($"PASS: {Checks} checks; decision logic uses physics/navigation stubs; member contracts use installed game and built mod.");
    }
}
