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

        var visible = plugin.Configuration.Visible;
        if (ImGui.Checkbox("Show unified job panel", ref visible))
        {
            plugin.Configuration.Visible = visible;
            changed = true;
        }

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
            ImGui.SetTooltip("This disables mouse interaction while locked. Controller focus and commands still work.");

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

        var activationChord = plugin.Configuration.EnableControllerActivationChord;
        if (ImGui.Checkbox("Enable L1 + R1 focus shortcut", ref activationChord))
        {
            plugin.Configuration.EnableControllerActivationChord = activationChord;
            changed = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Off by default. The shortcut is only observed while the panel is visible; input is intercepted only after focus activates.");

        if (plugin.Controller.IsActive)
        {
            if (ImGui.Button("Exit controller focus"))
                plugin.DeactivateControllerFocus();
        }
        else if (ImGui.Button("Activate controller focus"))
        {
            plugin.ActivateControllerFocus();
        }

        ImGui.TextDisabled("While focused: D-pad/left stick moves, A equips, X opens gear sets, B exits.");
        ImGui.TextDisabled("In the gear-set picker: A equips, Y sets default, B returns.");

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
        ImGui.Text("Visible jobs");

        foreach (var category in Enum.GetValues<JobCategory>())
        {
            var jobs = plugin.Gearsets.JobsIn(category).ToArray();
            if (jobs.Length == 0)
                continue;

            ImGui.TextDisabled(category.DisplayName());
            foreach (var job in jobs)
            {
                var first = job.First();
                var jobVisible = plugin.Configuration.IsJobVisible(job.Key);
                if (ImGui.Checkbox($"{first.JobAbbreviation} - {first.JobName}##job-visibility-{job.Key}", ref jobVisible))
                    plugin.SetJobVisibility(job.Key, jobVisible);
            }
        }

        if (ImGui.Button("Show all jobs"))
        {
            plugin.Configuration.HiddenClassJobIds.Clear();
            changed = true;
        }

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
        ImGui.TextDisabled("Left-click a job to equip its default gear set.");
        ImGui.TextDisabled("Right-click a job to choose or set another default.");

        if (changed)
            plugin.SaveConfiguration();
    }
}
