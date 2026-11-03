// Numerics-backed Unity math subset for executing the production direction policy.
namespace UnityEngine;

internal struct Vector3(float x, float y, float z)
{
    public float x = x, y = y, z = z;
    public static Vector3 zero => new(0, 0, 0);
    public static Vector3 up => new(0, 1, 0);
    public static Vector3 down => new(0, -1, 0);
    public static Vector3 forward => new(0, 0, 1);
    public float sqrMagnitude => x * x + y * y + z * z;
    public Vector3 normalized => sqrMagnitude < 1e-10f ? zero : this * (1f / MathF.Sqrt(sqrMagnitude));
    public void Normalize() => this = normalized;
    public static Vector3 ProjectOnPlane(Vector3 v, Vector3 normal) =>
        v - normal * ((v.x * normal.x + v.y * normal.y + v.z * normal.z) / normal.sqrMagnitude);
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
}

internal struct Quaternion(System.Numerics.Quaternion value)
{
    public static Quaternion AngleAxis(float degrees, Vector3 axis) => new(
        System.Numerics.Quaternion.CreateFromAxisAngle(new(axis.x, axis.y, axis.z), degrees * MathF.PI / 180f));
    public static Vector3 operator *(Quaternion q, Vector3 v)
    {
        var rotated = System.Numerics.Vector3.Transform(new(v.x, v.y, v.z), q.value);
        return new(rotated.X, rotated.Y, rotated.Z);
    }
    private readonly System.Numerics.Quaternion value = value;
}
