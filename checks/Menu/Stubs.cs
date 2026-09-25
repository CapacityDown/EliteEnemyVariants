using System.Collections;

namespace UnityEngine { public static class Time { public static float unscaledTime; } }
namespace ExitGames.Client.Photon { public class Hashtable : Dictionary<object, object>
{ public new object? this[object key] { get => TryGetValue(key,out var value)?value:null; set { if(value!=null)base[key]=value; } } } }
namespace BepInEx.Configuration
{
    public class ConfigEntry<T>(T value) { public T Value = value; }
    public class ConfigFile { public bool SaveOnConfigSet=true, Fail; public int Saves; public void Save(){if(Fail)throw new IOException("test");Saves++;} }
}
namespace Photon.Realtime
{
    using ExitGames.Client.Photon;
    public class Player { public int ActorNumber; public Hashtable CustomProperties=new(); public void SetCustomProperties(Hashtable values){foreach(var v in values)CustomProperties[v.Key]=v.Value;} }
    public class Room { public Hashtable CustomProperties=new(); public int Publishes; public bool SetCustomProperties(Hashtable values){Publishes++;foreach(var v in values)CustomProperties[v.Key]=v.Value;return true;} }
}
namespace Photon.Pun
{
    using Photon.Realtime;
    public class MonoBehaviourPunCallbacks { public virtual void OnJoinedRoom(){} public virtual void OnLeftRoom(){} public virtual void OnMasterClientSwitched(Player player){} }
    public static class PhotonNetwork
    {
        public static bool InRoom, IsMasterClient;
        public static int ServerTimestamp;
        public static Room? CurrentRoom;
        public static Player LocalPlayer=new(){ActorNumber=1};
        public static Player? MasterClient=LocalPlayer;
        public static Player[] PlayerList=Array.Empty<Player>();
    }
}
public class GameManager { public static GameManager? instance=new(); }
public static class SemiFunc { public static bool Multiplayer; public static bool IsMultiplayer()=>Multiplayer; }
namespace REPOJP.EliteEnemyVariants
{
    using BepInEx.Configuration;
    internal sealed class EnemyChanceSettings { internal ConfigEntry<bool> Enabled=new(true); }
    internal sealed class EliteEnemyConfig
    {
        private readonly Dictionary<EliteArchetype,EnemyChanceSettings> _entries = new();
        internal ConfigEntry<bool> Enabled=new(true);
        internal EnemyChanceSettings ChanceFor(EliteArchetype enemy) { if(!_entries.TryGetValue(enemy,out var entry))_entries[enemy]=entry=new();return entry; }
    }
    internal static class EnemyDisplayNameResolver
    {
        internal static string Get(EliteArchetype enemy)=>enemy switch { EliteArchetype.ApexPredator=>"Apex Predator",EliteArchetype.BirthdayBoy=>"Birthday Boy",
            EliteArchetype.CleanupCrew=>"Cleanup Crew",EliteArchetype.HeartHugger=>"Heart Hugger",EliteArchetype.ShadowChild=>"Shadow Child",_=>enemy.ToString() };
    }
    internal sealed class EliteBugReport { internal void RememberPlayers(){} }
    internal sealed class LoggerStub { internal void LogWarning(string text){} }
    internal sealed class EliteEnemyVariantsPlugin
    {
        internal static readonly EliteEnemyVariantsPlugin Instance=new();
        internal static readonly LoggerStub ModLogger=new();
        internal readonly EliteBugReport BugReport=new();
    }
}
