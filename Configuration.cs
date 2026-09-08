using Dalamud.Configuration;

namespace ClassySentinel;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Visible { get; set; } = true;

    public bool Locked { get; set; }

    public bool ShowCategoryHeaders { get; set; } = true;

    public bool ClickThroughWhenLocked { get; set; }

    public float ButtonSize { get; set; } = 40f;

    public int ButtonsPerRow { get; set; } = 8;

    // Stable ClassJob row ID -> gearset ID. Never tied to UI position.
    public Dictionary<uint, int> DefaultGearsets { get; set; } = new();

    public Dictionary<string, bool> CategoryVisibility { get; set; } = new();

    public Dictionary<string, BarPosition> BarPositions { get; set; } = new();

    public bool IsCategoryVisible(JobCategory category)
        => !CategoryVisibility.TryGetValue(category.ToString(), out var visible) || visible;
}

[Serializable]
public sealed class BarPosition
{
    public float X { get; set; }

    public float Y { get; set; }
}

