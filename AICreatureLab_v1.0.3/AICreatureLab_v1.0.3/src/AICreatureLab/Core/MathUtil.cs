using System;
using System.Numerics;

namespace AICreatureLab.Core;

internal static class MathUtil
{
    public static float Clamp(float value, float min, float max) => MathF.Max(min, MathF.Min(max, value));

    public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    public static float SafeNormalize(float value, float max)
    {
        if (max <= 0f)
        {
            return 0f;
        }

        return Clamp(value / max, 0f, 1f);
    }

    public static float Wrap(float value, float max)
    {
        if (max <= 0f)
        {
            return 0f;
        }

        while (value < 0f)
        {
            value += max;
        }

        while (value >= max)
        {
            value -= max;
        }

        return value;
    }

    public static Vector2 AngleToVector(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    public static float SignedAngle(Vector2 from, Vector2 to)
    {
        var cross = from.X * to.Y - from.Y * to.X;
        var dot = Vector2.Dot(from, to);
        return MathF.Atan2(cross, dot);
    }

    public static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    public static float MapDistanceScore(float distance, float maxDistance)
    {
        if (maxDistance <= 0f)
        {
            return 0f;
        }

        return 1f - Clamp(distance / maxDistance, 0f, 1f);
    }
}
