using System;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

// Finite, testable direction search. Geometry is provided by the host adapter.
internal static class PonyCeilingThrow
{
    internal const float ClearanceDistance = 3f;
    internal const float DownwardSlope = 0.2f;
    private static readonly float[] Yaws = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

    internal static bool Finite(Vector3 point) =>
        Finite(point.x) && Finite(point.y) && Finite(point.z);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    internal static Vector3 Choose(Vector3 preferred, Vector3 forward, Func<Vector3, bool> clear)
    {
        if (!Finite(preferred) || !Finite(forward)) return Vector3.zero;
        Vector3 planar = Vector3.ProjectOnPlane(preferred, Vector3.up);
        if (planar.sqrMagnitude < 0.0001f) planar = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (planar.sqrMagnitude < 0.0001f) planar = Vector3.forward;
        planar.Normalize();
        // Try a shallow downward fling first. Horizontal is safer when a low
        // ceiling leaves no room for the opposite, upward wind-up movement.
        for (int pass = 0; pass < 2; pass++)
        foreach (float yaw in Yaws)
        {
            Vector3 direction = (Quaternion.AngleAxis(yaw, Vector3.up) * planar +
                Vector3.down * (pass == 0 ? DownwardSlope : 0f)).normalized;
            if (clear(direction)) return direction;
        }
        // No safe initial corridor: the native release runs without extra force.
        return Vector3.zero;
    }
}

internal sealed class PonyThrowCache
{
    private int _sequence = -1;
    private Vector3 _direction;

    internal Vector3 Resolve(int sequence, Func<Vector3> choose)
    {
        if (sequence < 0) return Vector3.zero;
        if (_sequence != sequence)
        {
            _direction = choose();
            _sequence = sequence;
        }
        return _direction;
    }
}
