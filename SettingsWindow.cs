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
        Size = new Vector2(450, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var changed = false;

        var visible = plugin.Configuration.Visible;
        if (ImGui.Checkbox("Show role bars", ref visible))
        {
            plugin.Configuration.Visible = visible;
            changed = true;
        }

        var locked = plugin.Configuration.Locked;
        if (ImGui.Checkbox("Lock bar positions", ref locked))
        {
            plugin.Configuration.Locked = locked;
            changed = true;
        }

        var clickThrough = plugin.Configuration.ClickThroughWhenLocked;
        if (ImGui.Checkbox("Click through bars while locked", ref clickThrough))
        {
            plugin.Configuration.ClickThroughWhenLocked = clickThrough;
            changed = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("This also disables job switching. Use only when you want the bars to act as a display.");

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

        var buttonsPerRow = plugin.Configuration.ButtonsPerRow;
        if (ImGui.SliderInt("Maximum buttons per row", ref buttonsPerRow, 1, 16))
        {
            plugin.Configuration.ButtonsPerRow = buttonsPerRow;
            changed = true;
        }

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
        if (ImGui.Button("Refresh gear sets"))
            plugin.Gearsets.ForceRefresh();

        ImGui.SameLine();
        if (ImGui.Button("Reset bar positions"))
        {
            plugin.ResetBarPositions();
            changed = true;
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Left-click a job to equip its default gear set.");
        ImGui.TextDisabled("Right-click a job to choose or set another default.");

        if (changed)
            plugin.SaveConfiguration();
    }
}

