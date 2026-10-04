// Data-only substitutes for headless encoding tests. No native Unity behavior is tested here.
namespace UnityEngine;

public struct Vector2(float x, float y)
{
    public float x = x;
    public float y = y;
}

public struct Vector3(float x, float y, float z)
{
    public float x = x;
    public float y = y;
    public float z = z;
    public static Vector3 operator +(Vector3 left, Vector3 right) =>
        new(left.x + right.x, left.y + right.y, left.z + right.z);
    public static Vector3 operator -(Vector3 left, Vector3 right) =>
        new(left.x - right.x, left.y - right.y, left.z - right.z);
}

public struct Quaternion(float x, float y, float z, float w)
{
    public float x = x;
    public float y = y;
    public float z = z;
    public float w = w;
    // Store Euler input verbatim. Tests verify decoding, not native quaternion conversion.
    public static Quaternion Euler(Vector3 value) => new(value.x, value.y, value.z, 0);
    public static Quaternion Euler(float x, float y, float z) => new(x, y, z, 0);
}

public static class Mathf
{
    public static float Lerp(float min, float max, float value) => min + (max - min) * Math.Clamp(value, 0, 1);
    public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    public static float InverseLerp(float min, float max, float value) =>
        min == max ? 0 : Math.Clamp((value - min) / (max - min), 0, 1);
    public static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.ToEven);
}

public struct Vector4(float x, float y, float z, float w)
{
    public float x = x;
    public float y = y;
    public float z = z;
    public float w = w;
}

public struct Color;
