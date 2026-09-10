using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ClassySentinel;

public sealed class SettingsWindow : Window
{
    private readonly Plugin plugin;

    public SettingsWindow(Plugin plugin)
        : base("Classy Sentinel Settings##ClassySentinel-Settings", ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
        Size = new Vector2(500, 680);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var changed = false;

        var panelVisible = plugin.ManualPanelOpen || plugin.Controller.IsActive;
        if (ImGui.Button(panelVisible ? "Close launcher" : "Open launcher"))
            plugin.SetManualPanelOpen(!panelVisible);
        ImGui.SameLine();
        ImGui.TextDisabled("The launcher is hidden by default during gameplay.");

        var locked = plugin.Configuration.Locked;
        if (ImGui.Checkbox("Lock panel position", ref locked))
        {
            plugin.Configuration.Locked = locked;
            changed = true;
        }

        var clickThrough = plugin.Configuration.ClickThroughWhenLocked;
        if (ImGui.Checkbox("Click through panel while locked", ref clickThrough))
        {
            plugin.Configuration.ClickThroughWhenLocked = clickThrough;
            changed = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("This disables mouse interaction while locked. The temporary R3 launcher still works.");

        var headers = plugin.Configuration.ShowCategoryHeaders;
        if (ImGui.Checkbox("Show category headers", ref headers))
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
        ImGui.Separator();
        ImGui.Text("Controller");
        ImGui.TextDisabled("R3 - Open / cancel Classy Sentinel");
        ImGui.TextDisabled("D-pad - Navigate visible gear sets");
        ImGui.TextDisabled("X / Cross - Equip the exact selected gear set and exit");
        ImGui.TextDisabled("Circle - Cancel");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Visible categories");

        foreach (var category in Enum.GetValues<JobCategory>())
        {
            var categoryVisible = plugin.Configuration.IsCategoryVisible(category);
            if (ImGui.Checkbox(category.DisplayName(), ref categoryVisible))
            {
                plugin.Configuration.CategoryVisibility[category.ToString()] = categoryVisible;
                changed = true;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Visible gear sets");
        ImGui.TextDisabled("Each job's default is shown automatically. Additional sets are opt-in.");

        foreach (var category in Enum.GetValues<JobCategory>())
        {
            var jobs = plugin.Gearsets.JobsIn(category).ToArray();
            if (jobs.Length == 0)
                continue;

            ImGui.TextDisabled(category.DisplayName());
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
                    var label = $"{gearset.JobAbbreviation} - {gearset.GearsetName} [#{gearset.GearsetId + 1}] ({marker})##gearset-visibility-{gearset.ClassJobId}-{gearset.GearsetId}";
                    if (ImGui.Checkbox(label, ref gearsetVisible))
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

        DrawUnavailableGearsets(ref changed);

        ImGui.Spacing();
        if (ImGui.Button("Refresh gear sets"))
            plugin.Gearsets.ForceRefresh();

        ImGui.SameLine();
        if (ImGui.Button("Reset panel position"))
        {
            plugin.ResetPanelPosition();
            changed = true;
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Every launcher tile equips the exact gear set named in its tooltip.");
        ImGui.TextDisabled("Duplicate job icons receive a gear-set number badge.");

        if (changed)
            plugin.SaveConfiguration();
    }

    private void DrawUnavailableGearsets(ref bool changed)
    {
        var unavailableDefaults = plugin.Configuration.DefaultGearsetEntries
            .Where(saved => plugin.Gearsets.FindExact(saved.Value) is null)
            .ToArray();
        var unavailableExtras = plugin.Configuration.AdditionalGearsets
            .Where(saved => plugin.Gearsets.FindExact(saved) is null)
            .ToArray();

        if (unavailableDefaults.Length == 0 && unavailableExtras.Length == 0)
            return;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Unavailable saved gear sets");
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
}
