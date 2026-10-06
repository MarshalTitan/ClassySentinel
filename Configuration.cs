using Dalamud.Configuration;

namespace ClassySentinel;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = ThemeMigrationPolicy.CurrentConfigurationVersion;

    // Persisted as an integer so an invalid or future value can be normalized
    // safely without preventing the rest of the configuration from loading.
    public int Theme { get; set; } = ThemeMigrationPolicy.Classic;

    // Sentinel Modern uses an in-theme compact header instead of ImGui's
    // native collapsed title bar. Preserve both the state and restore size.
    public bool ModernWindowCollapsed { get; set; }

    public float ModernExpandedWidth { get; set; }

    public float ModernExpandedHeight { get; set; }

    // Retained only so version 3 configurations deserialize cleanly. Panel
    // visibility is now temporary runtime state and is never restored on load.
    public bool Visible { get; set; }

    public bool Locked { get; set; }

    public bool ShowCategoryHeaders { get; set; } = true;

    public bool ClickThroughWhenLocked { get; set; }

    public float ButtonSize { get; set; } = 40f;

    public float PanelScale { get; set; } = 1f;

    public int ButtonsPerRow { get; set; } = 8;

    // Version 3 migration source. New defaults use the compound identity below.
    public Dictionary<uint, int> DefaultGearsets { get; set; } = new();

    // Stable ClassJob row ID -> exact saved gear-set identity.
    public Dictionary<uint, GearsetReference> DefaultGearsetEntries { get; set; } = new();

    // Exact identities for user-selected non-default launcher entries.
    public List<GearsetReference> AdditionalGearsets { get; set; } = new();

    // Exact identities for automatic defaults the user chose to hide.
    public List<GearsetReference> HiddenDefaultGearsets { get; set; } = new();

    // Version 3 migration source. New visibility state is gear-set-specific.
    public HashSet<uint> HiddenClassJobIds { get; set; } = new();

    public Dictionary<string, bool> CategoryVisibility { get; set; } = new();

    public BarPosition? PanelPosition { get; set; }

    public bool IsCategoryVisible(JobCategory category)
        => !CategoryVisibility.TryGetValue(category.ToString(), out var visible) || visible;

    public bool IsDefaultGearsetVisible(GearsetInfo gearset)
        => !HiddenDefaultGearsets.Any(
            saved => GearsetReference.IsValidSavedReference(saved) && saved.Matches(gearset));

    public bool IsAdditionalGearsetVisible(GearsetInfo gearset)
        => AdditionalGearsets.Any(
            saved => GearsetReference.IsValidSavedReference(saved) && saved.Matches(gearset));

    public bool TryRepairSavedGearsetReferences()
    {
        var changed = false;

        if (DefaultGearsetEntries is null)
        {
            DefaultGearsetEntries = new Dictionary<uint, GearsetReference>();
            changed = true;
        }
        else
        {
            var invalidDefaultKeys = DefaultGearsetEntries
                .Where(saved => saved.Key == 0
                                || !GearsetReference.IsValidSavedReference(saved.Value)
                                || saved.Value.ClassJobId != saved.Key)
                .Select(saved => saved.Key)
                .ToArray();
            foreach (var key in invalidDefaultKeys)
                DefaultGearsetEntries.Remove(key);

            changed |= invalidDefaultKeys.Length > 0;
        }

        if (AdditionalGearsets is null)
        {
            AdditionalGearsets = new List<GearsetReference>();
            changed = true;
        }
        else
        {
            changed |= AdditionalGearsets.RemoveAll(
                saved => !GearsetReference.IsValidSavedReference(saved)) > 0;
        }

        if (HiddenDefaultGearsets is null)
        {
            HiddenDefaultGearsets = new List<GearsetReference>();
            changed = true;
        }
        else
        {
            changed |= HiddenDefaultGearsets.RemoveAll(
                saved => !GearsetReference.IsValidSavedReference(saved)) > 0;
        }

        return changed;
    }

    public bool TryMigrateGearsetReferences(IReadOnlyList<GearsetInfo> discoveredGearsets)
    {
        if (Version >= 4)
            return false;

        // Wait until character gear sets are available so an old slot ID can
        // be paired with its ClassJob and name instead of being migrated blind.
        if (DefaultGearsets.Count > 0 && discoveredGearsets.Count == 0)
            return false;

        foreach (var (classJobId, gearsetId) in DefaultGearsets)
        {
            var exactLegacyMatch = discoveredGearsets.FirstOrDefault(
                gearset => gearset.ClassJobId == classJobId && gearset.GearsetId == gearsetId);
            if (exactLegacyMatch is not null)
                DefaultGearsetEntries[classJobId] = GearsetReference.From(exactLegacyMatch);
        }

        foreach (var job in discoveredGearsets.GroupBy(gearset => gearset.ClassJobId))
        {
            if (!DefaultGearsetEntries.ContainsKey(job.Key))
                DefaultGearsetEntries[job.Key] = GearsetReference.From(job.OrderBy(gearset => gearset.GearsetId).First());
        }

        foreach (var hiddenClassJobId in HiddenClassJobIds)
        {
            if (DefaultGearsetEntries.TryGetValue(hiddenClassJobId, out var hiddenDefault))
                HiddenDefaultGearsets.Add(hiddenDefault);
        }

        DefaultGearsets.Clear();
        HiddenClassJobIds.Clear();
        Visible = false;
        Version = 4;
        return true;
    }

    public bool TryMigrateTheme()
    {
        var version = Version;
        var theme = Theme;
        if (!ThemeMigrationPolicy.Apply(ref version, ref theme))
            return false;

        Version = version;
        Theme = theme;
        return true;
    }

    public bool EnsureAutomaticDefaults(IReadOnlyList<GearsetInfo> discoveredGearsets)
    {
        if (Version < 4 || discoveredGearsets.Count == 0)
            return false;

        var changed = false;
        foreach (var job in discoveredGearsets.GroupBy(gearset => gearset.ClassJobId))
        {
            if (DefaultGearsetEntries.ContainsKey(job.Key))
                continue;

            DefaultGearsetEntries[job.Key] = GearsetReference.From(job.OrderBy(gearset => gearset.GearsetId).First());
            changed = true;
        }

        return changed;
    }
}

[Serializable]
public sealed class BarPosition
{
    public float X { get; set; }

    public float Y { get; set; }
}
