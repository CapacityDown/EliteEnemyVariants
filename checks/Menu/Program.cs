using System.Reflection;
using BepInEx.Configuration;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Photon.Pun;
using Photon.Realtime;
using REPOJP.EliteEnemyVariants;
using UnityEngine;

static class Program
{
    private static int Checks;
    private static void Check(bool value,string name) { if(!value)throw new InvalidOperationException(name);Checks++; }
    private static void Poll(EliteMenuState state) => typeof(EliteMenuState).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(state,null);
    private static IEnumerable<TypeDefinition> Nested(TypeDefinition type)
    { yield return type;foreach(var nested in type.NestedTypes)foreach(var child in Nested(nested))yield return child; }
    static void Main(string[] args)
    {
        EliteMenu.CheckIssues(Check);
        foreach(bool ready in new[]{false,true}) foreach(bool multiplayer in new[]{false,true})
        foreach(bool room in new[]{false,true}) foreach(bool master in new[]{false,true})
            Check(EliteMenuPolicy.CanEdit(ready,multiplayer,room,master)==(ready&&(room?master:!multiplayer)),"authority matrix");
        Check(EliteMenuPolicy.ButtonTop(0,0)==36,"standalone placement");
        Check(EliteMenuPolicy.ButtonTop(70,0)==82,"under Roles");
        Check(EliteMenuPolicy.ButtonTop(0,70)==82,"under Events");
        Check(EliteMenuPolicy.ButtonTop(70,110)==122,"under both mods");
        Check(EliteMenuPolicy.ButtonTop(120,70)==132,"late Roles placement recomputed");
        foreach(float units in new[]{1f,120f}) foreach(float notches in new[]{-3f,-0.5f,0.5f,1f,3f})
        foreach(float travel in new[]{200f,1000f,9000f})
        foreach(float handle in new[]{40f,120f,250f})
        {
            float step=EliteMenuPolicy.WheelStep(notches*units,units,travel,handle);
            float contentTravel=step*travel/handle;
            Check(Math.Abs(contentTravel-notches*97.5f)<0.001f,"Roles three-body-line wheel distance and magnitude");
        }
        foreach(float bad in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            Check(EliteMenuPolicy.WheelStep(1,bad,100,10)==0,"invalid wheel units");
            Check(EliteMenuPolicy.WheelStep(1,1,bad,10)==0,"invalid scroll travel");
            Check(EliteMenuPolicy.WheelStep(1,1,100,bad)==0,"invalid scroll handle");
        }
        Check(Enum.GetValues<EliteArchetype>().Length==30,"all 30 enemies");
        long all=Enum.GetValues<EliteArchetype>().Aggregate(0L,(mask,enemy)=>mask|1L<<(int)enemy);
        Check(all==EliteMenuPolicy.AllEnemies,"stable complete mask");
        foreach(EliteArchetype enemy in Enum.GetValues<EliteArchetype>())
        {
            Check(!string.IsNullOrWhiteSpace(EliteGuideCatalog.Description(enemy)),"description "+enemy);
            Check(EliteMenuPolicy.TryDecode(EliteMenuPolicy.Encode(4,1L<<(int)enemy,true,1),4,out long mask,out bool active)&&active&&mask==(1L<<(int)enemy),"round trip "+enemy);
        }
        foreach(string? bad in new string?[]{null,"","2:4:1:1:0","1:3:1:1:0","1:4:-1:1:0","1:4:1073741824:1:0","1:4:1:2:0","1:4:1:1:-1","1:4:1:1:2147483648","1:4:1:1:0:extra",new('x',129)})
            Check(!EliteMenuPolicy.TryDecode(bad,4,out _,out _),"invalid snapshot rejected");
        Check(!EliteMenuPolicy.TryDecode("1:4:1:1:0",0,out _,out _),"missing master rejected");
        Check(EliteMenuPolicy.TryDecode("1:4:0:0:0",4,out long off,out bool disabled)&&off==0&&!disabled,"all disabled snapshot");

        ConfigFile file=new();EliteEnemyConfig config=new();EliteMenuState state=new();state.Initialize(file,config);
        Check(state.TryRead(out long enabled,out _)&&enabled==all,"single player reads local");
        Check(state.SetEnabled(EliteArchetype.Rugrat,false)&&!config.ChanceFor(EliteArchetype.Rugrat).Enabled.Value&&file.Saves==1,"host toggle saved");
        Check(file.SaveOnConfigSet,"automatic save restored");
        file.Fail=true;
        try { state.SetEnabled(EliteArchetype.Rugrat,true);throw new Exception("save should fail"); }catch(IOException){}
        Check(!config.ChanceFor(EliteArchetype.Rugrat).Enabled.Value&&file.SaveOnConfigSet,"save failure rollback");file.Fail=false;
        Check(!state.SetEnabled((EliteArchetype)30,true),"unknown setting rejected");
        GameManager.instance=null;Check(!EliteMenuState.CanEdit,"not ready cannot edit");GameManager.instance=new();
        PhotonNetwork.InRoom=true;SemiFunc.Multiplayer=true;PhotonNetwork.IsMasterClient=true;
        Room roomObject=new();PhotonNetwork.CurrentRoom=roomObject;PhotonNetwork.MasterClient=PhotonNetwork.LocalPlayer;
        Poll(state);Check(roomObject.Publishes==1,"host publishes optional settings");
        Time.unscaledTime=1;Poll(state);Check(roomObject.Publishes==1,"unchanged state not repeatedly sent");
        state.RefreshDisplay();Time.unscaledTime=3;Poll(state);Check(roomObject.Publishes==2,"host refresh republishes");
        Player guest=new(){ActorNumber=2};PhotonNetwork.IsMasterClient=false;PhotonNetwork.LocalPlayer=guest;PhotonNetwork.PlayerList=new[]{guest};
        Check(!state.SetEnabled(EliteArchetype.Rugrat,true)&&file.Saves==1,"guest cannot alter host or local config");
        Check(state.TryRead(out enabled,out _)&&(enabled&(1L<<(int)EliteArchetype.Rugrat))==0,"guest reads host not own values");
        PhotonNetwork.ServerTimestamp=50;state.RefreshDisplay();Check(guest.CustomProperties["EEV.Menu.Request"] is int,"guest can request display refresh");
        PhotonNetwork.ServerTimestamp=60;state.RefreshDisplay();Check((int)guest.CustomProperties["EEV.Menu.Request"]! ==50,"guest refresh rate limit");
        PhotonNetwork.MasterClient=new(){ActorNumber=7};state.OnMasterClientSwitched(PhotonNetwork.MasterClient);
        Check(!state.TryRead(out _,out _),"old host snapshot rejected after migration");
        PhotonNetwork.MasterClient=new(){ActorNumber=1};PhotonNetwork.LocalPlayer=PhotonNetwork.MasterClient;PhotonNetwork.IsMasterClient=true;
        Time.unscaledTime=6;Poll(state);Check(roomObject.Publishes==3,"host responds to guest refresh without RPC");
        PhotonNetwork.CurrentRoom=new();state.OnJoinedRoom();Time.unscaledTime=7;Poll(state);
        Check(PhotonNetwork.CurrentRoom.Publishes==1,"new room publishes even unchanged settings");
        PhotonNetwork.IsMasterClient=false;Check(!state.TryRead(out _,out _)||PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("EEV.Menu"),"client has no invented snapshot");
        PhotonNetwork.CurrentRoom.CustomProperties.Clear();Check(!state.TryRead(out _,out _),"vanilla or old host shows unavailable");

        string redacted=EliteReportRedactor.Clean("Alice RoomSecret 76561198000000000 192.168.2.1:22 2001:db8::1 x@y.example token=secret ghp_example C:\\Users\\Alice\\private.log",new[]{"Alice","RoomSecret"});
        foreach(string secret in new[]{"Alice","RoomSecret","76561198000000000","192.168.2.1","2001:db8","x@y.example","token=secret","ghp_example","C:\\Users"})
            Check(!redacted.Contains(secret),"redaction "+secret);
        Check(EliteReportRedactor.Clean(new string('a',90000),Array.Empty<string>()).Length<80100,"bounded report size");
        Check(EliteReportRedactor.Clean("safe\0text",Array.Empty<string>())=="safetext","control characters removed");
        Check(EliteReportRedactor.Clean("/home/alice/private.txt",Array.Empty<string>()).Contains("[REDACTED]"),"Unix path masked");

        using ModuleDefinition mod=ModuleDefinition.ReadModule(args[0]);
        using ModuleDefinition game=ModuleDefinition.ReadModule(args[1]);
        var nativePopup = game.Types.Single(t=>t.Name=="MenuPageTwoOptions");
        Check(nativePopup.Methods.Single(m=>m.Name=="Update").Body.Instructions.Any(i=>i.Operand is MethodReference call && call.Name=="ButtonEventOption2"),"native Escape routes to cancel");
        foreach (string name in new[]{"ButtonEventOption1","ButtonEventOption2"})
            Check(nativePopup.Methods.Single(m=>m.Name==name).Body.Instructions.Any(i=>i.Operand is MethodReference call && call.Name=="PageReactivatePageUnderThisPage"),"native choice restores underlying page: "+name);
        var ui=mod.Types.Where(t=>t.Name.StartsWith("EliteMenu")||t.Name=="EliteBugReport").SelectMany(Nested).ToArray();
        foreach(var method in ui.SelectMany(t=>t.Methods).Where(m=>m.HasBody))foreach(var instruction in method.Body.Instructions)
        {
            if(instruction.Operand is FieldReference f&&f.DeclaringType.Scope.Name=="Assembly-CSharp")
                Check(game.Types.Single(t=>t.FullName==f.DeclaringType.FullName).Fields.Single(field=>field.Name==f.Name).IsPublic,"public game field "+f.Name);
            if(instruction.Operand is MethodReference call)
            {
                Check(call.Name!="RPC","UI has no client-required RPC");
                Check(!(call.DeclaringType.Name=="EliteRegistry"||call.DeclaringType.Name=="EliteSelector"),"menu neither counts nor selects enemies");
            }
        }
        var refs=mod.AssemblyReferences.Select(r=>r.Name).ToArray();
        Check(!refs.Contains("RoleShuffle")&&!refs.Contains("StagePhysicsEvents"),"no hard assembly coupling to other mods");
        Console.WriteLine($"PASS: {Checks} menu checks; runtime uses network/config stubs; field contracts use installed game.");
    }
}
