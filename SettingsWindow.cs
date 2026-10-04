using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using SentinelCore.UI;

namespace ClassySentinel;

public sealed class SettingsWindow : Window
{
    private readonly Plugin plugin;
    private readonly SentinelThemeState<SettingsPage> themeState;
    private readonly SentinelModernStyleScope modernStyle = new();
    private readonly Action drawModernNavigation;
    private readonly Action drawModernContent;
    private SentinelStyleScope? classicStyle;
    private bool modernChanged;

    public SettingsWindow(Plugin plugin)
        : base("Classy Sentinel Settings##ClassySentinel-Settings")
    {
        this.plugin = plugin;
        themeState = new SentinelThemeState<SettingsPage>(
            SettingsPage.General,
            SentinelThemeState<SettingsPage>.NormalizeTheme(plugin.Configuration.Theme));
        drawModernNavigation = DrawModernNavigation;
        drawModernContent = DrawModernPage;
        Size = new Vector2(900, 720);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (themeState.IsModern)
            modernStyle.Push(scale);
        else
            classicStyle = SentinelStyleScope.PushWindow(scale);
    }

    public override void Draw()
    {
        var changed = false;
        if (themeState.IsModern)
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
        var options = new SentinelModernShellOptions(
            "ClassySentinel.ModernSettings",
            "SENTINEL",
            "Classy Sentinel",
            "Fast, exact gear-set selection for mouse, keyboard, and controller.")
        {
            ContextLabel = "Configuration",
            Status = new SentinelModernStatus(
                panelVisible ? "LAUNCHER OPEN" : "READY",
                panelVisible ? SentinelModernStatusTone.Accent : SentinelModernStatusTone.Success),
            Scale = ImGuiHelpers.GlobalScale,
        };

        SentinelModernConfigurationShell.Draw(
            options,
            drawModernNavigation,
            drawModernContent);
        return modernChanged;
    }

    private void DrawModernNavigation()
    {
        var scale = ImGuiHelpers.GlobalScale;
        SentinelModernNavigation.GroupLabel("SETTINGS");
        if (SentinelModernNavigation.Item("general", "General", themeState.SelectedPage == SettingsPage.General, scale))
            themeState.SelectPage(SettingsPage.General);
        if (SentinelModernNavigation.Item("appearance", "Appearance", themeState.SelectedPage == SettingsPage.Appearance, scale))
            themeState.SelectPage(SettingsPage.Appearance);
        if (SentinelModernNavigation.Item("gearsets", "Gear sets", themeState.SelectedPage == SettingsPage.Gearsets, scale))
            themeState.SelectPage(SettingsPage.Gearsets);

        ImGui.Spacing();
        SentinelModernNavigation.GroupLabel("THEME");
        if (SentinelModernNavigation.Item("theme-classic", "Classic", false, scale))
            SelectTheme(SentinelThemeKind.Classic);
        SentinelModernNavigation.Item("theme-modern", "Sentinel Modern", true, scale);
    }

    private void DrawModernPage()
    {
        switch (themeState.SelectedPage)
        {
            case SettingsPage.General:
                SentinelModernUi.PageHeading(
                    "General",
                    "Open the launcher and review the controls used to choose an exact saved gear set.");
                ImGui.Spacing();
                using (var card = SentinelModernCard.Begin("##GeneralCard"))
                {
                    if (card.IsVisible)
                    {
                        DrawLauncherControl();
                        DrawControllerHelp(modern: true);
                        ImGui.Spacing();
                        ImGui.TextDisabled("Every launcher tile equips the exact gear set named in its tooltip.");
                        ImGui.TextDisabled("Duplicate job icons receive a gear-set number badge.");
                    }
                }
                break;

            case SettingsPage.Appearance:
                SentinelModernUi.PageHeading(
                    "Appearance",
                    "Adjust the launcher without changing its saved position or interaction model.");
                ImGui.Spacing();
                using (var card = SentinelModernCard.Begin("##AppearanceCard"))
                {
                    if (card.IsVisible)
                        DrawPositionAndAppearance(ref modernChanged, modern: true);
                }
                break;

            case SettingsPage.Gearsets:
                SentinelModernUi.PageHeading(
                    "Gear sets",
                    "Choose which categories and exact saved gear sets appear in the launcher.");
                ImGui.Spacing();
                using (var card = SentinelModernCard.Begin("##GearsetsCard"))
                {
                    if (card.IsVisible)
                    {
                        DrawCategoryVisibility(ref modernChanged, modern: true);
                        DrawGearsetVisibility(ref modernChanged, modern: true);
                        DrawMaintenanceActions(modern: true);
                    }
                }
                break;
        }
    }

    private void DrawLauncherControl()
    {
        var panelVisible = plugin.ManualPanelOpen || plugin.Controller.IsActive;
        if (ImGui.Button(panelVisible ? "Close launcher" : "Open launcher"))
            plugin.SetManualPanelOpen(!panelVisible);
        ImGui.SameLine();
        ImGui.TextDisabled("Hidden by default during gameplay.");
    }

    private void DrawPositionAndAppearance(ref bool changed, bool modern)
    {
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
            .Where(saved => plugin.Gearsets.FindExact(saved.Value) is null)
            .ToArray();
        var unavailableExtras = plugin.Configuration.AdditionalGearsets
            .Where(saved => plugin.Gearsets.FindExact(saved) is null)
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
                plugin.Configuration.HiddenDefaultGearsets.RemoveAll(entry => entry.Equals(saved));
                changed = true;
            }
        }

        foreach (var saved in unavailableExtras.ToArray())
        {
            ImGui.TextDisabled($"Extra: {saved.GearsetName} [#{saved.GearsetId + 1}] (ClassJob {saved.ClassJobId})");
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##missing-extra-{saved.ImGuiId}"))
            {
                plugin.Configuration.AdditionalGearsets.RemoveAll(entry => entry.Equals(saved));
                changed = true;
            }
        }
    }

    private static bool DrawBoolean(string id, string label, ref bool value, bool modern)
        => modern
            ? SentinelModernControls.Toggle(id, label, ref value, ImGuiHelpers.GlobalScale)
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

    private enum SettingsPage
    {
        General,
        Appearance,
        Gearsets,
    }
}
