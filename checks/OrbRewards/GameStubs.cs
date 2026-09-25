using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => original;
    }
    public class GameObject : Object
    {
        public readonly Dictionary<Type, object> Components = new();
        public T? GetComponentInChildren<T>(bool includeInactive) where T : class =>
            Components.TryGetValue(typeof(T), out object? value) ? (T)value : null;
    }
    public struct Vector3 { }
    public struct Quaternion { }
}

namespace Photon.Pun
{
    public static class PhotonNetwork
    {
        public static UnityEngine.GameObject InstantiateRoomObject(string name, UnityEngine.Vector3 position,
            UnityEngine.Quaternion rotation, byte group, object[]? data = null) => new();
    }
}

public class Enemy { public bool Enhanced; }
public class EnemyParent : UnityEngine.GameObject { public void Despawn() { } }
public class EnemyValuable { }
public class ValuableObject
{
    public bool dollarValueSet;
    public float dollarValueOriginal;
    public float dollarValueCurrent;
    public float RolledValue = 1000f;
    public void DollarValueSetLogic()
    {
        if (dollarValueSet) return;
        dollarValueOriginal = dollarValueCurrent = RolledValue;
        dollarValueSet = true;
    }
}
public static class SemiFunc
{
    public static bool Host = true;
    public static bool IsMasterClientOrSingleplayer() => Host;
}

namespace REPOJP.EliteEnemyVariants
{
    public static class EliteRegistry
    {
        public static bool IsEnhanced(Enemy? enemy) => enemy?.Enhanced == true;
    }
    public sealed class BoolSetting { public bool Value = true; }
    public sealed class SettingsStub { public readonly BoolSetting Enabled = new(); }
    public sealed class LoggerStub
    {
        public int Warnings;
        public void LogWarning(string message) => Warnings++;
    }
    public sealed class EliteEnemyVariantsPlugin
    {
        public static readonly EliteEnemyVariantsPlugin Instance = new();
        public static readonly LoggerStub ModLogger = new();
        public readonly SettingsStub Settings = new();
    }
}
