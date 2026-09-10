using Dalamud.Configuration;

namespace ClassySentinel;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool Visible { get; set; } = true;

    public bool Locked { get; set; }

    public bool ShowCategoryHeaders { get; set; } = true;

    public bool ClickThroughWhenLocked { get; set; }

    public float ButtonSize { get; set; } = 40f;

    public float PanelScale { get; set; } = 1f;

    public int ButtonsPerRow { get; set; } = 8;

    public bool EnableControllerActivationChord { get; set; }

    // Stable ClassJob row ID -> gearset ID. Never tied to UI position.
    public Dictionary<uint, int> DefaultGearsets { get; set; } = new();

    // Stable ClassJob row IDs. Newly discovered jobs are visible by default.
    public HashSet<uint> HiddenClassJobIds { get; set; } = new();

    public Dictionary<string, bool> CategoryVisibility { get; set; } = new();

    public BarPosition? PanelPosition { get; set; }

    public bool IsCategoryVisible(JobCategory category)
        => !CategoryVisibility.TryGetValue(category.ToString(), out var visible) || visible;

    public bool IsJobVisible(uint classJobId) => !HiddenClassJobIds.Contains(classJobId);
}

[Serializable]
public sealed class BarPosition
{
    public float X { get; set; }

    public float Y { get; set; }
}
