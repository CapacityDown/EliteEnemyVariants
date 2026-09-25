using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object
    {
        private static int NextId;
        private readonly int _id = ++NextId;
        public int GetInstanceID() => _id;
    }
    public class GameObject { public bool activeInHierarchy = true; }
    public class Transform { public Vector3 position; public Vector3 forward = new(0, 0, 1); }
    public class Component : Object { public Transform transform = new(); public GameObject gameObject = new(); }
    public class Rigidbody : Component { }
    public class Collider : Component
    {
        public ValuableObject Valuable = null!;
        public T? GetComponentInParent<T>() where T : class => Valuable as T;
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude => x*x + y*y + z*z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude > 0.00001f ? this / magnitude : zero;
        public static Vector3 zero => default;
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 down => new(0, -1, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a, float b) => new(a.x/b,a.y/b,a.z/b);
        public static float Distance(Vector3 a, Vector3 b) => (a-b).magnitude;
        public static float Dot(Vector3 a, Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 ClampMagnitude(Vector3 a, float max) => a.magnitude > max ? a.normalized*max : a;
    }
    public static class Mathf
    {
        public const float PI = MathF.PI;
        public static float Cos(float value) => MathF.Cos(value);
        public static float Sin(float value) => MathF.Sin(value);
        public static float Abs(float value) => MathF.Abs(value);
        public static float Min(float a, float b) => MathF.Min(a,b);
        public static float Clamp01(float value) => Math.Clamp(value,0,1);
    }
    public enum QueryTriggerInteraction { Ignore }
    public static class LayerMask
    {
        public static int GetMask(params string[] names) => names.Length > 1 ? 4 : names[0] switch
        { "Default" => 1, "PhysGrabObject" => 2, "Player" => 8, _ => 4 };
    }
    public static class Time { public static float time; }
    public static class Physics
    {
        public static readonly List<Collider> Loot = new();
        public static bool Floor = true;
        public static Func<Vector3,Vector3,bool> Blocked = (_,_) => false;
        public static Collider[] OverlapSphere(Vector3 origin, float radius, int mask) =>
            Loot.Where(c => Vector3.Distance(origin,c.Valuable.Phys.transform.position) <= radius).ToArray();
        public static bool Raycast(Vector3 origin, Vector3 direction, float distance, int mask) =>
            mask == 1 ? Floor : Blocked(origin, origin+direction*distance);
        public static bool Raycast(Vector3 origin, Vector3 direction, float distance, int mask, QueryTriggerInteraction query) =>
            Raycast(origin,direction,distance,mask);
    }
}
namespace UnityEngine.AI
{
    using UnityEngine;
    public enum NavMeshPathStatus { PathComplete, PathPartial, PathInvalid }
    public class NavMeshPath { public NavMeshPathStatus status; public Vector3[] corners = Array.Empty<Vector3>(); }
    public struct NavMeshHit { public Vector3 position; }
    public static class NavMesh
    {
        public const int AllAreas = -1;
        public static bool Reachable = true;
        public static Func<Vector3,Vector3> Snap = p => p;
        public static bool SamplePosition(Vector3 point, out NavMeshHit hit, float range, int mask)
        { hit = new NavMeshHit { position=Snap(point) }; return true; }
        public static bool CalculatePath(Vector3 from, Vector3 to, int mask, NavMeshPath path)
        { path.corners=new[]{from,to}; path.status=Reachable?NavMeshPathStatus.PathComplete:NavMeshPathStatus.PathPartial; return true; }
    }
}
public class PlayerAvatar : UnityEngine.Component
{
    public bool Disabled, Protected;
    public UnityEngine.Vector3 rbVelocity;
    public VisionTarget? PlayerVisionTarget;
}
public class VisionTarget { public UnityEngine.Transform VisionTransform = new(); }
public class Enemy : UnityEngine.Component
{
    public UnityEngine.Transform CenterTransform => transform;
    public EnemyNavMeshAgent Nav = new();
    public EnemyRigidbody Body = new();
}
public class EnemyFloater : UnityEngine.Component { public UnityEngine.Transform feetTransform = new(); }
public class EnemyNavMeshAgent
{
    public UnityEngine.Vector3 Origin;
    public UnityEngine.AI.NavMeshPath CalculatePath(UnityEngine.Vector3 point)
    {
        var path = new UnityEngine.AI.NavMeshPath();
        UnityEngine.AI.NavMesh.CalculatePath(Origin, point,-1,path); return path;
    }
}
public class EnemyRigidbody { public UnityEngine.Rigidbody rb = new(); }
public class PhysGrabObject : UnityEngine.Component
{
    public PhysGrabObjectImpactDetector Impact = new();
    public List<PhysGrabber> playerGrabbing = new();
}
public class PhysGrabber { }
public class PlayerDeathHead : UnityEngine.Component
{
    public PlayerAvatar playerAvatar = new() { Disabled=true };
    public PhysGrabObject physGrabObject = new();
    public bool overrideSpectated;
}
public class PhysGrabObjectImpactDetector { public bool Indestructible; }
public class ValuableObject : UnityEngine.Component
{
    public PhysGrabObject Phys = new();
    public float Value = 1000f;
}
public class LevelPoint : UnityEngine.Component
{
    public bool ModuleConnect;
    public RoomVolume? Room;
    public List<LevelPoint> ConnectedPoints = new();
}
public class RoomVolume { }
public static class SemiFunc
{
    public static bool Host = true, Occupied;
    public static List<PlayerAvatar> Players = new();
    public static List<LevelPoint> Points = new();
    public static bool IsMasterClientOrSingleplayer() => Host;
    public static List<PlayerAvatar> PlayerGetList() => Players;
    public static int LayerMaskGetVisionObstruct() => 4;
    public static List<LevelPoint> LevelPointGetWithinDistance(UnityEngine.Vector3 center,float min,float max) => Points;
    public static bool EnemyPhysObjectBoundingBoxCheck(UnityEngine.Vector3 from,UnityEngine.Vector3 to,UnityEngine.Rigidbody body) => Occupied;
}
namespace REPOJP.EliteEnemyVariants
{
    public class EliteState { public Enemy Enemy=new(); public bool Spawned=true, AdapterTickFaulted; }
    public static class EliteRegistry
    {
        public static EliteState State=new();
        public static bool Enhanced=true;
        public static bool TryGet(UnityEngine.Component component,out EliteState state) { state=State;return Enhanced; }
    }
    public class BoolSetting { public bool Value=true; }
    public class SettingsStub { public BoolSetting Enabled=new(); }
    public class EliteEnemyVariantsPlugin
    {
        public static EliteEnemyVariantsPlugin Instance=new();
        public SettingsStub Settings=new();
    }
    public static class DedicatedBehaviorSupport
    {
        public static bool IsValidTarget(PlayerAvatar? player) => player!=null&&!player.Disabled;
        public static bool CanUseAsUnseenTarget(PlayerAvatar? player) => IsValidTarget(player)&&!player!.Protected;
    }
    public static class GameFieldAccess
    {
        public static bool PlayerDisabled(PlayerAvatar player) => player.Disabled;
        public static PhysGrabObject ValuablePhysGrabObject(ValuableObject valuable) => valuable.Phys;
        public static PhysGrabObjectImpactDetector ImpactDetector(PhysGrabObject phys) => phys.Impact;
        public static bool IsIndestructible(PhysGrabObjectImpactDetector impact) => impact.Indestructible;
        public static float ValuableDollarValue(ValuableObject valuable) => valuable.Value;
        public static EnemyNavMeshAgent NavMeshAgent(Enemy enemy) => enemy.Nav;
        public static EnemyRigidbody Rigidbody(Enemy enemy) => enemy.Body;
    }
}
