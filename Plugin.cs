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
            HelpMessage = "Toggle Classy Sentinel. Options: show, hide, focus, unfocus, lock, unlock, config, refresh",
        };
        CommandManager.AddHandler(CommandName, command);
        CommandManager.AddHandler(ShortCommandName, command);

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleBars;
        PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        Framework.Update += OnFrameworkUpdate;

        Gearsets.ForceRefresh();
    }

    public Configuration Configuration { get; }

    public GearsetService Gearsets { get; }

    public ControllerNavigation Controller { get; }

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

    public void SetDefaultGearset(uint classJobId, int gearsetId)
    {
        Configuration.DefaultGearsets[classJobId] = gearsetId;
        SaveConfiguration();
    }

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

    public void SetJobVisibility(uint classJobId, bool visible)
    {
        if (visible)
            Configuration.HiddenClassJobIds.Remove(classJobId);
        else
            Configuration.HiddenClassJobIds.Add(classJobId);
        SaveConfiguration();
    }

    public void ActivateControllerFocus()
    {
        Configuration.Visible = true;
        mainPanel.IsOpen = true;
        mainPanel.BringToFront();
        Controller.Activate(mainPanel.BuildNavigationRows());
        SaveConfiguration();
    }

    public void DeactivateControllerFocus() => Controller.Deactivate();

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
        var rows = mainPanel.BuildNavigationRows();
        Controller.Update(rows, Configuration.Visible && PlayerState.IsLoaded && rows.Count > 0);
        if (configurationDirty && DateTime.UtcNow >= saveConfigurationAfterUtc)
            SaveConfiguration();
    }

    private void ToggleBars()
    {
        Configuration.Visible = !Configuration.Visible;
        if (!Configuration.Visible)
            Controller.Deactivate();
        SaveConfiguration();
    }

    private void OpenSettings() => settingsWindow.IsOpen = true;

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "show":
                Configuration.Visible = true;
                SaveConfiguration();
                break;
            case "hide":
                Configuration.Visible = false;
                Controller.Deactivate();
                SaveConfiguration();
                break;
            case "focus":
            case "controller":
                ActivateControllerFocus();
                break;
            case "unfocus":
                DeactivateControllerFocus();
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
