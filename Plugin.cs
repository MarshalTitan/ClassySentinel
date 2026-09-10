using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ClassySentinel;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/classysentinel";
    private const string ShortCommandName = "/csentinel";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGamepadState GamepadState { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("ClassySentinel");
    private readonly UnifiedPanelWindow mainPanel;
    private readonly SettingsWindow settingsWindow;
    private bool configurationDirty;
    private DateTime saveConfigurationAfterUtc;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Gearsets = new GearsetService(DataManager, PlayerState, ChatGui, Log, Configuration);
        Controller = new ControllerNavigation(this, GamepadState);
        mainPanel = new UnifiedPanelWindow(this);
        settingsWindow = new SettingsWindow(this);
        windowSystem.AddWindow(mainPanel);
        windowSystem.AddWindow(settingsWindow);

        var command = new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle Classy Sentinel. Options: show, hide, lock, unlock, config, refresh",
        };
        CommandManager.AddHandler(CommandName, command);
        CommandManager.AddHandler(ShortCommandName, command);

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleBars;
        PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        Framework.Update += OnFrameworkUpdate;

        Gearsets.ForceRefresh();
        MaintainGearsetReferences();
    }

    public Configuration Configuration { get; }

    public GearsetService Gearsets { get; }

    public ControllerNavigation Controller { get; }

    public bool ManualPanelOpen { get; private set; }

    public void Dispose()
    {
        Controller.Dispose();
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleBars;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(ShortCommandName);
        windowSystem.RemoveAllWindows();

        if (configurationDirty)
            PluginInterface.SavePluginConfig(Configuration);
    }

    public void SetDefaultGearset(GearsetInfo gearset)
    {
        var previousDefault = Gearsets.GetDefault(gearset.ClassJobId);
        var preservePrevious = previousDefault is not null
                               && previousDefault.GearsetId != gearset.GearsetId
                               && Configuration.IsDefaultGearsetVisible(previousDefault);

        Configuration.DefaultGearsetEntries[gearset.ClassJobId] = GearsetReference.From(gearset);
        Configuration.DefaultGearsets.Remove(gearset.ClassJobId);
        Configuration.AdditionalGearsets.RemoveAll(saved => saved.Matches(gearset));
        if (preservePrevious
            && previousDefault is not null
            && !Configuration.AdditionalGearsets.Any(saved => saved.Matches(previousDefault)))
        {
            Configuration.AdditionalGearsets.Add(GearsetReference.From(previousDefault));
        }
        Configuration.HiddenDefaultGearsets.RemoveAll(saved => saved.ClassJobId == gearset.ClassJobId);
        SaveConfiguration();
    }

    public void SetAdditionalGearsetVisibility(GearsetInfo gearset, bool visible)
    {
        Configuration.AdditionalGearsets.RemoveAll(saved => saved.Matches(gearset));
        if (visible)
            Configuration.AdditionalGearsets.Add(GearsetReference.From(gearset));
        SaveConfiguration();
    }

    public void SetManualPanelOpen(bool open)
    {
        ManualPanelOpen = open;
        if (!open && Controller.IsActive)
            Controller.Deactivate();
    }

    public void CloseManualPanel() => ManualPanelOpen = false;

    public void RememberPanelPosition(Vector2 position)
    {
        if (Configuration.Locked)
            return;

        var current = Configuration.PanelPosition;
        if (current is not null
            && Math.Abs(current.X - position.X) < 0.5f
            && Math.Abs(current.Y - position.Y) < 0.5f)
        {
            return;
        }

        Configuration.PanelPosition = new BarPosition { X = position.X, Y = position.Y };
        MarkConfigurationDirty();
    }

    public void ResetPanelPosition()
    {
        Configuration.PanelPosition = null;
        mainPanel.ResetPosition();
        SaveConfiguration();
    }

    public void SetDefaultGearsetVisibility(GearsetInfo gearset, bool visible)
    {
        Configuration.HiddenDefaultGearsets.RemoveAll(saved => saved.Matches(gearset));
        if (!visible)
            Configuration.HiddenDefaultGearsets.Add(GearsetReference.From(gearset));
        SaveConfiguration();
    }

    public void SaveConfiguration()
    {
        PluginInterface.SavePluginConfig(Configuration);
        configurationDirty = false;
    }

    private void MarkConfigurationDirty()
    {
        configurationDirty = true;
        saveConfigurationAfterUtc = DateTime.UtcNow.AddSeconds(1);
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        Gearsets.RefreshIfDue();
        MaintainGearsetReferences();
        var rows = mainPanel.BuildNavigationRows();
        Controller.Update(rows, PlayerState.IsLoaded && rows.Count > 0);
        if (configurationDirty && DateTime.UtcNow >= saveConfigurationAfterUtc)
            SaveConfiguration();
    }

    private void MaintainGearsetReferences()
    {
        var changed = Configuration.TryMigrateGearsetReferences(Gearsets.Gearsets);
        changed |= Configuration.EnsureAutomaticDefaults(Gearsets.Gearsets);
        if (changed)
            SaveConfiguration();
    }

    private void ToggleBars()
    {
        if (Controller.IsActive)
        {
            Controller.Deactivate();
            return;
        }

        ManualPanelOpen = !ManualPanelOpen;
    }

    private void OpenSettings() => settingsWindow.IsOpen = true;

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "show":
                SetManualPanelOpen(true);
                break;
            case "hide":
                ManualPanelOpen = false;
                Controller.Deactivate();
                break;
            case "lock":
                Configuration.Locked = true;
                SaveConfiguration();
                break;
            case "unlock":
                Configuration.Locked = false;
                SaveConfiguration();
                break;
            case "config":
            case "settings":
                OpenSettings();
                break;
            case "refresh":
                Gearsets.ForceRefresh();
                break;
            default:
                ToggleBars();
                break;
        }
    }
}
