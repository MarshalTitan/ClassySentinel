using System.Numerics;
using ClassySentinel;

var tests = new (string Name, Action Run)[]
{
    ("keeps a valid saved position unchanged", () => AssertPosition(
        new Vector2(1400f, 700f),
        PanelPositionPolicy.KeepReachable(
            new Vector2(1400f, 700f),
            new Vector2(180f, 220f),
            Vector2.Zero,
            new Vector2(1920f, 1080f),
            new Vector2(420f, 280f)))),
    ("keeps a bottom-right panel reachable after a display shrink", () => AssertPosition(
        new Vector2(860f, 440f),
        PanelPositionPolicy.KeepReachable(
            new Vector2(1400f, 700f),
            new Vector2(180f, 220f),
            Vector2.Zero,
            new Vector2(1280f, 720f),
            new Vector2(420f, 280f)))),
    ("clamps a completely off-screen position", () => AssertPosition(
        new Vector2(1500f, 800f),
        PanelPositionPolicy.KeepReachable(
            new Vector2(4000f, 3000f),
            new Vector2(180f, 220f),
            new Vector2(100f, 50f),
            new Vector2(1800f, 1000f),
            new Vector2(400f, 250f)))),
    ("clamps coordinates above and left of the work area", () => AssertPosition(
        new Vector2(100f, 50f),
        PanelPositionPolicy.KeepReachable(
            new Vector2(-900f, -500f),
            new Vector2(180f, 220f),
            new Vector2(100f, 50f),
            new Vector2(1800f, 1000f),
            new Vector2(400f, 250f)))),
    ("uses the fallback for invalid saved coordinates", () => AssertPosition(
        new Vector2(180f, 220f),
        PanelPositionPolicy.KeepReachable(
            new Vector2(float.NaN, float.PositiveInfinity),
            new Vector2(180f, 220f),
            Vector2.Zero,
            new Vector2(1920f, 1080f),
            new Vector2(420f, 280f)))),
    ("anchors an oversized panel in the work area", () => AssertPosition(
        new Vector2(100f, 50f),
        PanelPositionPolicy.KeepReachable(
            new Vector2(900f, 600f),
            new Vector2(180f, 220f),
            new Vector2(100f, 50f),
            new Vector2(800f, 600f),
            new Vector2(1200f, 900f)))),
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS: {test.Name}");
}

Console.WriteLine($"{tests.Length} panel-position regression tests passed.");

static void AssertPosition(Vector2 expected, Vector2 actual)
{
    if (Vector2.DistanceSquared(expected, actual) >= 0.0001f)
        throw new InvalidOperationException($"Expected {expected}, received {actual}.");
}
