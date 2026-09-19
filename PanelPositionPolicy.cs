using System.Numerics;

namespace ClassySentinel;

internal static class PanelPositionPolicy
{
    public static Vector2 KeepReachable(
        Vector2 position,
        Vector2 fallback,
        Vector2 workPosition,
        Vector2 workSize,
        Vector2 windowSize)
    {
        if (!IsFinite(position))
            position = IsFinite(fallback) ? fallback : Vector2.Zero;

        if (!IsFinite(workPosition)
            || !IsFinite(workSize)
            || workSize.X <= 0f
            || workSize.Y <= 0f)
        {
            return position;
        }

        var effectiveWindowSize = IsFinite(windowSize)
                                  && windowSize.X > 0f
                                  && windowSize.Y > 0f
            ? Vector2.Min(windowSize, workSize)
            : Vector2.Min(new Vector2(48f, 32f), workSize);
        var maximum = workPosition + workSize - effectiveWindowSize;

        return Vector2.Clamp(position, workPosition, maximum);
    }

    public static bool IsFinite(Vector2 position)
        => float.IsFinite(position.X) && float.IsFinite(position.Y);
}
