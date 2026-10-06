using System.Numerics;
using System.Text.Json;
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
    ("migrates existing users explicitly to Classic", () => AssertThemeMigration(
        initialVersion: 4,
        initialTheme: ThemeMigrationPolicy.Classic,
        expectedChanged: true,
        expectedVersion: 5,
        expectedTheme: ThemeMigrationPolicy.Classic)),
    ("defers theme migration until legacy gear sets migrate", () => AssertThemeMigration(
        initialVersion: 3,
        initialTheme: ThemeMigrationPolicy.Modern,
        expectedChanged: false,
        expectedVersion: 3,
        expectedTheme: ThemeMigrationPolicy.Modern)),
    ("preserves an explicit Modern selection", () => AssertThemeMigration(
        initialVersion: 5,
        initialTheme: ThemeMigrationPolicy.Modern,
        expectedChanged: false,
        expectedVersion: 5,
        expectedTheme: ThemeMigrationPolicy.Modern)),
    ("persists the Modern compact-header state and restore size", AssertModernWindowStatePersists),
    ("normalizes an invalid theme to Classic", () => AssertThemeMigration(
        initialVersion: 5,
        initialTheme: 99,
        expectedChanged: true,
        expectedVersion: 5,
        expectedTheme: ThemeMigrationPolicy.Classic)),
    ("repairs null saved gear-set references without removing valid entries", AssertNullReferencesAreRepaired),
    ("repairs null saved gear-set collections", AssertNullCollectionsAreRepaired),
    ("treats a null exact gear-set lookup as no match", AssertNullLookupIsSafe),
    ("keeps a valid unavailable gear-set reference", AssertUnavailableReferenceIsPreserved),
    ("keeps a normal valid gear-set configuration unchanged", AssertValidConfigurationIsUnchanged),
    ("persists repaired gear-set references across restart", AssertRepairPersists),
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS: {test.Name}");
}

Console.WriteLine($"{tests.Length} regression tests passed.");

static void AssertPosition(Vector2 expected, Vector2 actual)
{
    if (Vector2.DistanceSquared(expected, actual) >= 0.0001f)
        throw new InvalidOperationException($"Expected {expected}, received {actual}.");
}

static void AssertThemeMigration(
    int initialVersion,
    int initialTheme,
    bool expectedChanged,
    int expectedVersion,
    int expectedTheme)
{
    var version = initialVersion;
    var theme = initialTheme;
    var changed = ThemeMigrationPolicy.Apply(ref version, ref theme);
    if (changed != expectedChanged || version != expectedVersion || theme != expectedTheme)
    {
        throw new InvalidOperationException(
            $"Expected changed={expectedChanged}, version={expectedVersion}, theme={expectedTheme}; " +
            $"received changed={changed}, version={version}, theme={theme}.");
    }
}

static void AssertModernWindowStatePersists()
{
    var configuration = new Configuration
    {
        Theme = ThemeMigrationPolicy.Modern,
        ModernWindowCollapsed = true,
        ModernExpandedWidth = 930f,
        ModernExpandedHeight = 740f,
    };

    var json = JsonSerializer.Serialize(configuration);
    var reloaded = JsonSerializer.Deserialize<Configuration>(json)
                   ?? throw new InvalidOperationException("Modern window state did not deserialize.");
    if (!reloaded.ModernWindowCollapsed
        || reloaded.ModernExpandedWidth != 930f
        || reloaded.ModernExpandedHeight != 740f)
    {
        throw new InvalidOperationException("Modern compact-header state or restore size did not persist.");
    }
}

static void AssertNullReferencesAreRepaired()
{
    var validDefault = Reference(2, 19, "WAR");
    var validExtra = Reference(3, 19, "WAR Alt");
    var validHidden = Reference(4, 24, "WHM");
    var configuration = JsonSerializer.Deserialize<Configuration>(
        """
        {
          "DefaultGearsetEntries": {
            "19": { "GearsetId": 2, "ClassJobId": 19, "GearsetName": "WAR" },
            "24": null
          },
          "AdditionalGearsets": [
            { "GearsetId": 3, "ClassJobId": 19, "GearsetName": "WAR Alt" },
            null,
            { "GearsetId": -1, "ClassJobId": 0, "GearsetName": "" }
          ],
          "HiddenDefaultGearsets": [
            { "GearsetId": 4, "ClassJobId": 24, "GearsetName": "WHM" },
            null
          ]
        }
        """) ?? throw new InvalidOperationException("Malformed configuration did not deserialize.");

    if (!configuration.TryRepairSavedGearsetReferences())
        throw new InvalidOperationException("Expected malformed references to be repaired.");
    if (configuration.DefaultGearsetEntries.Count != 1
        || !configuration.DefaultGearsetEntries[19].Equals(validDefault)
        || configuration.AdditionalGearsets.Count != 1
        || !configuration.AdditionalGearsets[0].Equals(validExtra)
        || configuration.HiddenDefaultGearsets.Count != 1
        || !configuration.HiddenDefaultGearsets[0].Equals(validHidden))
    {
        throw new InvalidOperationException("Repair did not preserve every valid saved reference.");
    }
}

static void AssertNullCollectionsAreRepaired()
{
    var configuration = JsonSerializer.Deserialize<Configuration>(
        """
        {
          "DefaultGearsetEntries": null,
          "AdditionalGearsets": null,
          "HiddenDefaultGearsets": null
        }
        """) ?? throw new InvalidOperationException("Null collections did not deserialize.");

    if (!configuration.TryRepairSavedGearsetReferences()
        || configuration.DefaultGearsetEntries is null
        || configuration.AdditionalGearsets is null
        || configuration.HiddenDefaultGearsets is null)
    {
        throw new InvalidOperationException("Null saved-reference collections were not repaired.");
    }
}

static void AssertUnavailableReferenceIsPreserved()
{
    var unavailable = Reference(7, 35, "BLU Solo");
    var configuration = new Configuration();
    configuration.AdditionalGearsets.Add(unavailable);

    if (configuration.TryRepairSavedGearsetReferences())
        throw new InvalidOperationException("A valid unavailable reference was treated as malformed.");
    if (GearsetReferenceLookup.FindExact(Array.Empty<GearsetInfo>(), unavailable) is not null)
        throw new InvalidOperationException("An unavailable reference unexpectedly matched a live gear set.");
    if (!configuration.AdditionalGearsets.Contains(unavailable))
        throw new InvalidOperationException("A valid unavailable reference was removed.");
}

static void AssertNullLookupIsSafe()
{
    if (GearsetReferenceLookup.FindExact(new[] { Gearset(1, 19, "WAR") }, null) is not null)
        throw new InvalidOperationException("A null saved reference unexpectedly resolved.");
}

static void AssertValidConfigurationIsUnchanged()
{
    var live = Gearset(9, 37, "GNB");
    var reference = GearsetReference.From(live);
    var configuration = new Configuration();
    configuration.DefaultGearsetEntries[live.ClassJobId] = reference;
    configuration.AdditionalGearsets.Add(Reference(10, 37, "GNB Alt"));
    configuration.HiddenDefaultGearsets.Add(reference);

    if (configuration.TryRepairSavedGearsetReferences())
        throw new InvalidOperationException("A normal valid configuration was modified.");
    if (!Reference(9, 37, "GNB").Equals(reference)
        || GearsetReferenceLookup.FindExact(new[] { live }, reference) != live)
    {
        throw new InvalidOperationException("A valid exact gear-set reference no longer resolves correctly.");
    }
}

static void AssertRepairPersists()
{
    var valid = Reference(12, 22, "DRG");
    var configuration = new Configuration();
    configuration.DefaultGearsetEntries[22] = valid;
    configuration.AdditionalGearsets.Add(null!);
    configuration.HiddenDefaultGearsets.Add(Reference(13, 22, "DRG Alt"));

    if (!configuration.TryRepairSavedGearsetReferences())
        throw new InvalidOperationException("Expected the source configuration to be repaired.");

    var json = JsonSerializer.Serialize(configuration);
    var reloaded = JsonSerializer.Deserialize<Configuration>(json)
                   ?? throw new InvalidOperationException("Repaired configuration did not deserialize.");
    if (reloaded.TryRepairSavedGearsetReferences()
        || reloaded.DefaultGearsetEntries.Count != 1
        || !reloaded.DefaultGearsetEntries[22].Equals(valid)
        || reloaded.AdditionalGearsets.Count != 0
        || reloaded.HiddenDefaultGearsets.Count != 1)
    {
        throw new InvalidOperationException("The repaired configuration did not persist cleanly.");
    }
}

static GearsetReference Reference(int gearsetId, uint classJobId, string name)
    => new()
    {
        GearsetId = gearsetId,
        ClassJobId = classJobId,
        GearsetName = name,
    };

static GearsetInfo Gearset(int gearsetId, uint classJobId, string name)
    => new(
        gearsetId,
        classJobId,
        name,
        "Job",
        "JOB",
        0,
        0,
        0,
        JobCategory.Tank,
        RoleHue.Tank);
