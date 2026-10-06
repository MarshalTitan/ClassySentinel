using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using SentinelCore.UI;

namespace ClassySentinel;

public sealed class SettingsWindow : Window, IDisposable
{
    private static readonly Vector2 ClassicMinimumWindowSize = new(620f, 520f);
    private static readonly SentinelModernNavItem[] ModernPrimaryNavigation =
    [
        new("general", null, "General")
        {
            DrawIcon = static context => DrawModernNavigationIcon(FontAwesomeIcon.Cog, context),
        },
        new("appearance", null, "Appearance")
        {
            DrawIcon = static context => DrawModernNavigationIcon(FontAwesomeIcon.Palette, context),
        },
        new("gearsets", null, "Gear sets")
        {
            DrawIcon = static context => DrawModernNavigationIcon(FontAwesomeIcon.Tools, context),
        },
    ];

    private readonly Plugin plugin;
    private readonly SentinelThemeState<SettingsPage> themeState;
    private readonly SentinelModernStyleScope modernStyle = new();
    private readonly SentinelModernAppShellState modernShellState = new();
    private readonly Action<string> selectModernPage;
    private readonly Action drawModernPage;
    private readonly Action drawModernActionDock;
    private readonly Action requestModernCollapse;
    private readonly Action requestModernClose;
    private readonly Action<SentinelModernIconDrawContext> drawModernPluginIcon;
    private readonly Action drawModernLauncherControl;
    private readonly Action drawModernButtonSizeControl;
    private readonly Action drawModernPanelScaleControl;
    private readonly Action drawModernButtonsPerRowControl;
    private readonly Action drawModernResetPositionControl;
    private readonly ImGuiWindowFlags classicWindowFlags;
    private SentinelStyleScope? classicStyle;
    private bool modernChanged;
    private bool modernThemeActive;
    private bool expandOnNextDraw;
    private bool disposed;

    public SettingsWindow(Plugin plugin)
        : base("Classy Sentinel Settings##ClassySentinel-Settings")
    {
        this.plugin = plugin;
        themeState = new SentinelThemeState<SettingsPage>(
            SettingsPage.General,
            SentinelThemeState<SettingsPage>.NormalizeTheme(plugin.Configuration.Theme));
        selectModernPage = SelectModernPage;
        drawModernPage = DrawModernPage;
        drawModernActionDock = DrawModernActionDock;
        requestModernCollapse = RequestModernCollapse;
        requestModernClose = RequestModernClose;
        drawModernPluginIcon = DrawModernPluginIcon;
        drawModernLauncherControl = DrawModernLauncherControl;
        drawModernButtonSizeControl = DrawModernButtonSizeControl;
        drawModernPanelScaleControl = DrawModernPanelScaleControl;
        drawModernButtonsPerRowControl = DrawModernButtonsPerRowControl;
        drawModernResetPositionControl = DrawModernResetPositionControl;
        classicWindowFlags = Flags;
        Size = new Vector2(900, 720);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = ClassicMinimumWindowSize,
        };
    }

    public override void PreDraw()
    {
        classicStyle?.Dispose();
        classicStyle = null;
        modernStyle.Pop();
        modernThemeActive = themeState.IsModern;
        if (expandOnNextDraw)
        {
            ImGui.SetNextWindowCollapsed(false, ImGuiCond.Always);
            expandOnNextDraw = false;
        }

        var scale = ImGuiHelpers.GlobalScale;
        if (modernThemeActive)
        {
            Flags = SentinelModernWindowChrome.UseCustomHeader(classicWindowFlags);
            modernStyle.PushAppShell(scale);
            var shellMinimum = SentinelModernAppLayout.MinimumWindowSize(
                scale,
                hasSecondarySidebar: false,
                hasActionDock: true);
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = Vector2.Max(ClassicMinimumWindowSize, shellMinimum),
            };
        }
        else
        {
            Flags = classicWindowFlags;
            classicStyle = SentinelStyleScope.PushWindow(scale);
            SizeConstraints = new WindowSizeConstraints { MinimumSize = ClassicMinimumWindowSize };
        }
    }

    public override void Draw()
    {
        var changed = false;
        if (modernThemeActive)
            changed |= DrawModern();
        else
            DrawClassic(ref changed);

        if (changed)
            plugin.SaveConfiguration();
    }

    public override void PostDraw()
    {
        modernStyle.Pop();
        classicStyle?.Dispose();
        classicStyle = null;
    }

    public void OpenAndExpand()
    {
        IsOpen = true;
        expandOnNextDraw = true;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        modernStyle.Dispose();
        modernShellState.Dispose();
        classicStyle?.Dispose();
        classicStyle = null;
        disposed = true;
    }

    private void DrawClassic(ref bool changed)
    {
        ImGui.Text("Theme");
        ImGui.SameLine();
        ImGui.TextDisabled("Classic");
        ImGui.SameLine();
        if (ImGui.SmallButton("Use Sentinel Modern"))
            SelectTheme(SentinelThemeKind.Modern);

        ImGui.Spacing();
        DrawLauncherControl();
        DrawPositionAndAppearance(ref changed, modern: false);
        DrawControllerHelp(modern: false);
        DrawCategoryVisibility(ref changed, modern: false);
        DrawGearsetVisibility(ref changed, modern: false);
        DrawMaintenanceActions(modern: false);
    }

    private bool DrawModern()
    {
        modernChanged = false;
        var panelVisible = plugin.ManualPanelOpen || plugin.Controller.IsActive;
        var options = new SentinelModernAppShellOptions(
            "ClassySentinel.Modern2",
            "Classy Sentinel",
            GetModernPageId(themeState.SelectedPage))
        {
            DrawPluginIcon = drawModernPluginIcon,
            ContextLabel = GetModernPageLabel(themeState.SelectedPage),
            Status = new SentinelModernStatusPillOptions(
                panelVisible ? "LAUNCHER OPEN" : "READY",
                panelVisible ? SentinelModernPillTone.Accent : SentinelModernPillTone.Ready),
            Scale = ImGuiHelpers.GlobalScale,
            DeltaTime = ImGui.GetIO().DeltaTime,
            ReducedMotion = Plugin.PluginInterface.UiBuilder.ShouldUseReducedMotion,
            AmbientIntensity = 0.9f,
            SurfaceStyle = SentinelModernAppSurfaceStyle.Unified,
            EnableWindowDragging = true,
            RequestCollapse = requestModernCollapse,
            RequestClose = requestModernClose,
        };

        SentinelModernAppShell.Draw(
            options,
            modernShellState,
            ModernPrimaryNavigation,
            selectModernPage,
            drawModernPage,
            drawActionDock: drawModernActionDock);
        return modernChanged;
    }

    private void DrawModernPage()
    {
        var scale = ImGuiHelpers.GlobalScale;
        switch (themeState.SelectedPage)
        {
            case SettingsPage.General:
                SentinelModernSettingsRow.Draw(
                    "ClassySentinel.Launcher",
                    "Quick launcher",
                    "Open or close the compact gear-set launcher without changing its saved position.",
                    drawModernLauncherControl,
                    scale: scale);
                DrawControllerHelp(modern: true);
                break;

            case SettingsPage.Appearance:
                DrawPositionAndAppearance(ref modernChanged, modern: true);
                break;

            case SettingsPage.Gearsets:
                DrawCategoryVisibility(ref modernChanged, modern: true);
                DrawGearsetVisibility(ref modernChanged, modern: true);
                DrawMaintenanceActions(modern: true);
                break;
        }
    }

    private void DrawModernActionDock()
    {
        var scale = ImGuiHelpers.GlobalScale;
        SentinelModernActionDock.Status("Sentinel Modern 2 is active");
        ImGui.SameLine();
        if (SentinelModernActionDock.PrimaryButton(
                "ClassySentinel.SwitchToClassic",
                "Switch to Classic",
                new Vector2(180f * scale, 0f),
                scale))
            SelectTheme(SentinelThemeKind.Classic);
    }

    private void DrawLauncherControl()
    {
        var panelVisible = plugin.ManualPanelOpen || plugin.Controller.IsActive;
        if (ImGui.Button(panelVisible ? "Close launcher" : "Open launcher"))
            plugin.SetManualPanelOpen(!panelVisible);
        ImGui.SameLine();
        ImGui.TextDisabled("Hidden by default during gameplay.");
    }

    private void DrawModernLauncherControl()
    {
        var panelVisible = plugin.ManualPanelOpen || plugin.Controller.IsActive;
        if (ImGui.Button(
                panelVisible ? "Close launcher" : "Open launcher",
                new Vector2(-1f, 0f)))
            plugin.SetManualPanelOpen(!panelVisible);
    }

    private void DrawPositionAndAppearance(ref bool changed, bool modern)
    {
        if (modern)
        {
            DrawModernPositionAndAppearance(ref changed);
            return;
        }

        DrawSection("Panel", modern);

        var locked = plugin.Configuration.Locked;
        if (DrawBoolean("locked", "Lock panel position", ref locked, modern))
        {
            plugin.Configuration.Locked = locked;
            changed = true;
        }

        var clickThrough = plugin.Configuration.ClickThroughWhenLocked;
        if (DrawBoolean("click-through", "Click through panel while locked", ref clickThrough, modern))
        {
            plugin.Configuration.ClickThroughWhenLocked = clickThrough;
            changed = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("This disables mouse interaction while locked. The temporary R3 launcher still works.");

        var headers = plugin.Configuration.ShowCategoryHeaders;
        if (DrawBoolean("headers", "Show category headers", ref headers, modern))
        {
            plugin.Configuration.ShowCategoryHeaders = headers;
            changed = true;
        }

        var buttonSize = plugin.Configuration.ButtonSize;
        if (ImGui.SliderFloat("Button size", ref buttonSize, 28f, 64f, "%.0f px"))
        {
            plugin.Configuration.ButtonSize = buttonSize;
            changed = true;
        }

        var panelScale = plugin.Configuration.PanelScale;
        if (ImGui.SliderFloat("Panel scale", ref panelScale, 0.6f, 1.6f, "%.2fx"))
        {
            plugin.Configuration.PanelScale = panelScale;
            changed = true;
        }

        var buttonsPerRow = plugin.Configuration.ButtonsPerRow;
        if (ImGui.SliderInt("Maximum buttons per row", ref buttonsPerRow, 1, 16))
        {
            plugin.Configuration.ButtonsPerRow = buttonsPerRow;
            changed = true;
        }

        ImGui.Spacing();
        if (ImGui.Button("Reset panel position"))
        {
            plugin.ResetPanelPosition();
            changed = true;
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Other settings are preserved.");
    }

    private void DrawModernPositionAndAppearance(ref bool changed)
    {
        var scale = ImGuiHelpers.GlobalScale;
        SentinelModernUi.SectionHeader("Panel");

        var locked = plugin.Configuration.Locked;
        if (DrawBoolean("locked", "Lock panel position", ref locked, modern: true))
        {
            plugin.Configuration.Locked = locked;
            changed = true;
        }

        var clickThrough = plugin.Configuration.ClickThroughWhenLocked;
        if (DrawBoolean(
                "click-through",
                "Click through panel while locked",
                ref clickThrough,
                modern: true))
        {
            plugin.Configuration.ClickThroughWhenLocked = clickThrough;
            changed = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("This disables mouse interaction while locked. The temporary R3 launcher still works.");

        var headers = plugin.Configuration.ShowCategoryHeaders;
        if (DrawBoolean("headers", "Show category headers", ref headers, modern: true))
        {
            plugin.Configuration.ShowCategoryHeaders = headers;
            changed = true;
        }

        SentinelModernSettingsRow.Draw(
            "ClassySentinel.ButtonSize",
            "Button size",
            "Adjust each job tile without changing the launcher workflow.",
            drawModernButtonSizeControl,
            scale: scale);
        SentinelModernSettingsRow.Draw(
            "ClassySentinel.PanelScale",
            "Panel scale",
            "Scale the compact launcher independently of the game UI.",
            drawModernPanelScaleControl,
            scale: scale);
        SentinelModernSettingsRow.Draw(
            "ClassySentinel.ButtonsPerRow",
            "Maximum buttons per row",
            "Controls wrapping while keeping D-pad navigation aligned to visible rows.",
            drawModernButtonsPerRowControl,
            scale: scale);
        SentinelModernSettingsRow.Draw(
            "ClassySentinel.ResetPosition",
            "Saved position",
            "Return the launcher to its safe default anchor; all other settings stay intact.",
            drawModernResetPositionControl,
            scale: scale);
    }

    private void DrawModernButtonSizeControl()
    {
        var buttonSize = plugin.Configuration.ButtonSize;
        if (!ImGui.SliderFloat("##ButtonSize", ref buttonSize, 28f, 64f, "%.0f px"))
            return;

        plugin.Configuration.ButtonSize = buttonSize;
        modernChanged = true;
    }

    private void DrawModernPanelScaleControl()
    {
        var panelScale = plugin.Configuration.PanelScale;
        if (!ImGui.SliderFloat("##PanelScale", ref panelScale, 0.6f, 1.6f, "%.2fx"))
            return;

        plugin.Configuration.PanelScale = panelScale;
        modernChanged = true;
    }

    private void DrawModernButtonsPerRowControl()
    {
        var buttonsPerRow = plugin.Configuration.ButtonsPerRow;
        if (!ImGui.SliderInt("##ButtonsPerRow", ref buttonsPerRow, 1, 16))
            return;

        plugin.Configuration.ButtonsPerRow = buttonsPerRow;
        modernChanged = true;
    }

    private void DrawModernResetPositionControl()
    {
        if (ImGui.Button("Reset position", new Vector2(-1f, 0f)))
            plugin.ResetPanelPosition();
    }

    private static void DrawControllerHelp(bool modern)
    {
        DrawSection("Controller", modern);
        ImGui.TextDisabled("R3 - Open / cancel Classy Sentinel");
        ImGui.TextDisabled("D-pad - Navigate visible gear sets");
        ImGui.TextDisabled("X / Cross - Equip the exact selected gear set and exit");
        ImGui.TextDisabled("Circle - Cancel");
    }

    private void DrawCategoryVisibility(ref bool changed, bool modern)
    {
        DrawSection("Visible categories", modern);
        foreach (var category in Enum.GetValues<JobCategory>())
        {
            var categoryVisible = plugin.Configuration.IsCategoryVisible(category);
            if (!DrawBoolean($"category-{category}", category.DisplayName(), ref categoryVisible, modern))
                continue;

            plugin.Configuration.CategoryVisibility[category.ToString()] = categoryVisible;
            changed = true;
        }
    }

    private void DrawGearsetVisibility(ref bool changed, bool modern)
    {
        DrawSection("Visible gear sets", modern);
        ImGui.TextDisabled("Each job's default is shown automatically. Additional sets are opt-in.");

        foreach (var category in Enum.GetValues<JobCategory>())
        {
            var jobs = plugin.Gearsets.JobsIn(category).ToArray();
            if (jobs.Length == 0)
                continue;

            if (modern)
            {
                if (!SentinelModernControls.CollapsingSection(category.DisplayName()))
                    continue;
            }
            else
            {
                ImGui.Spacing();
                ImGui.TextDisabled(category.DisplayName());
            }

            foreach (var job in jobs)
            {
                var defaultGearset = plugin.Gearsets.GetDefault(job.Key);
                foreach (var gearset in job.OrderBy(entry => entry.GearsetId))
                {
                    var isDefault = defaultGearset is not null && defaultGearset.GearsetId == gearset.GearsetId;
                    var gearsetVisible = isDefault
                        ? plugin.Configuration.IsDefaultGearsetVisible(gearset)
                        : plugin.Configuration.IsAdditionalGearsetVisible(gearset);
                    var marker = isDefault ? "Default" : "Extra";
                    var label = $"{gearset.JobAbbreviation} - {gearset.GearsetName} [#{gearset.GearsetId + 1}] ({marker})";
                    var id = $"gearset-visibility-{gearset.ClassJobId}-{gearset.GearsetId}";
                    if (DrawBoolean(id, label, ref gearsetVisible, modern))
                    {
                        if (isDefault)
                            plugin.SetDefaultGearsetVisibility(gearset, gearsetVisible);
                        else
                            plugin.SetAdditionalGearsetVisibility(gearset, gearsetVisible);
                    }

                    if (!isDefault)
                    {
                        if (!modern)
                            ImGui.SameLine();
                        if (ImGui.SmallButton($"Make default##default-{gearset.ClassJobId}-{gearset.GearsetId}"))
                            plugin.SetDefaultGearset(gearset);
                    }
                }
            }
        }

        ImGui.Spacing();
        if (ImGui.Button("Show all default gear sets"))
        {
            plugin.Configuration.HiddenDefaultGearsets.Clear();
            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("Hide all extras"))
        {
            plugin.Configuration.AdditionalGearsets.Clear();
            changed = true;
        }

        DrawUnavailableGearsets(ref changed, modern);
    }

    private void DrawMaintenanceActions(bool modern)
    {
        DrawSection("Maintenance", modern);
        if (ImGui.Button("Refresh gear sets"))
            plugin.Gearsets.ForceRefresh();

        ImGui.Spacing();
        ImGui.TextDisabled("Every launcher tile equips the exact gear set named in its tooltip.");
        ImGui.TextDisabled("Duplicate job icons receive a gear-set number badge.");
    }

    private void DrawUnavailableGearsets(ref bool changed, bool modern)
    {
        var unavailableDefaults = plugin.Configuration.DefaultGearsetEntries
            .Where(saved => GearsetReference.IsValidSavedReference(saved.Value)
                            && plugin.Gearsets.FindExact(saved.Value) is null)
            .ToArray();
        var unavailableExtras = plugin.Configuration.AdditionalGearsets
            .Where(saved => GearsetReference.IsValidSavedReference(saved)
                            && plugin.Gearsets.FindExact(saved) is null)
            .ToArray();

        if (unavailableDefaults.Length == 0 && unavailableExtras.Length == 0)
            return;

        DrawSection("Unavailable saved gear sets", modern);
        ImGui.TextDisabled("These entries will never redirect to another gear-set slot.");

        foreach (var (classJobId, saved) in unavailableDefaults)
        {
            ImGui.TextDisabled($"Default: {saved.GearsetName} [#{saved.GearsetId + 1}] (ClassJob {classJobId})");
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##missing-default-{classJobId}-{saved.GearsetId}"))
            {
                plugin.Configuration.DefaultGearsetEntries.Remove(classJobId);
                plugin.Configuration.HiddenDefaultGearsets.RemoveAll(
                    entry => !GearsetReference.IsValidSavedReference(entry) || saved.Equals(entry));
                changed = true;
            }
        }

        foreach (var saved in unavailableExtras.ToArray())
        {
            ImGui.TextDisabled($"Extra: {saved.GearsetName} [#{saved.GearsetId + 1}] (ClassJob {saved.ClassJobId})");
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##missing-extra-{saved.ImGuiId}"))
            {
                plugin.Configuration.AdditionalGearsets.RemoveAll(
                    entry => !GearsetReference.IsValidSavedReference(entry) || saved.Equals(entry));
                changed = true;
            }
        }
    }

    private bool DrawBoolean(string id, string label, ref bool value, bool modern)
        => modern
            ? SentinelModernSwitch.Draw(
                id,
                label,
                ref value,
                modernShellState.Motion,
                ImGuiHelpers.GlobalScale)
            : ImGui.Checkbox($"{label}##{id}", ref value);

    private static void DrawSection(string title, bool modern)
    {
        ImGui.Spacing();
        if (modern)
        {
            SentinelModernUi.SectionHeader(title);
            return;
        }

        ImGui.Separator();
        ImGui.Text(title);
    }

    private void SelectTheme(SentinelThemeKind theme)
    {
        if (!themeState.SelectTheme(theme))
            return;

        plugin.Configuration.Theme = (int)theme;
        plugin.SaveConfiguration();
    }

    private void SelectModernPage(string id)
    {
        var page = id switch
        {
            "general" => SettingsPage.General,
            "gearsets" => SettingsPage.Gearsets,
            "appearance" => SettingsPage.Appearance,
            _ => themeState.SelectedPage,
        };
        themeState.SelectPage(page);
    }

    private void RequestModernCollapse()
    {
        ImGui.SetWindowCollapsed("Classy Sentinel Settings##ClassySentinel-Settings", true);
    }

    private void RequestModernClose() => IsOpen = false;

    private static string GetModernPageId(SettingsPage page)
        => page switch
        {
            SettingsPage.General => "general",
            SettingsPage.Gearsets => "gearsets",
            SettingsPage.Appearance => "appearance",
            _ => "general",
        };

    private static string GetModernPageLabel(SettingsPage page)
        => page switch
        {
            SettingsPage.General => "General",
            SettingsPage.Gearsets => "Gear sets",
            SettingsPage.Appearance => "Appearance",
            _ => "Settings",
        };

    private static void DrawModernPluginIcon(SentinelModernIconDrawContext context)
        => DrawFontAwesomeIcon(
            FontAwesomeIcon.ShieldAlt,
            context.DrawList,
            context.Minimum,
            context.Maximum,
            SentinelModernPalette.Text);

    private static void DrawModernNavigationIcon(
        FontAwesomeIcon icon,
        SentinelModernNavIconDrawContext context)
        => DrawFontAwesomeIcon(
            icon,
            context.DrawList,
            context.Minimum,
            context.Maximum,
            context.Colour);

    private static void DrawFontAwesomeIcon(
        FontAwesomeIcon icon,
        ImDrawListPtr drawList,
        Vector2 minimum,
        Vector2 maximum,
        Vector4 colour)
    {
        var glyph = icon.ToIconString();
        ImGui.PushFont(UiBuilder.IconFont);
        try
        {
            var size = ImGui.CalcTextSize(glyph);
            drawList.AddText(
                minimum + (((maximum - minimum) - size) * 0.5f),
                ImGui.ColorConvertFloat4ToU32(colour),
                glyph);
        }
        finally
        {
            ImGui.PopFont();
        }
    }

    private enum SettingsPage
    {
        General,
        Appearance,
        Gearsets,
    }
}
