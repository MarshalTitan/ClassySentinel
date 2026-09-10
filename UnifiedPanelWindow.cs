using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace ClassySentinel;

public sealed class UnifiedPanelWindow : Window
{
    private const int StyleColorCount = 2;
    private const int StyleVarCount = 3;

    private static readonly Vector4 PanelBackground = new(0.026f, 0.031f, 0.040f, 0.95f);
    private static readonly Vector4 PanelBorder = new(0.58f, 0.47f, 0.27f, 0.92f);
    private static readonly Vector4 ButtonIdle = new(0.075f, 0.09f, 0.115f, 0.98f);
    private static readonly Vector4 ButtonHover = new(0.23f, 0.29f, 0.37f, 1f);
    private static readonly Vector4 ButtonActive = new(0.40f, 0.32f, 0.16f, 1f);
    private static readonly Vector4 CurrentIdle = new(0.43f, 0.32f, 0.10f, 1f);
    private static readonly Vector4 CurrentHover = new(0.68f, 0.52f, 0.18f, 1f);
    private static readonly Vector4 HeaderGold = new(0.92f, 0.78f, 0.44f, 1f);
    private static readonly Vector4 ControllerBlue = new(0.20f, 0.78f, 0.96f, 1f);
    private static readonly Vector4 ControllerIdle = new(0.08f, 0.24f, 0.34f, 1f);

    private readonly Plugin plugin;
    private bool forcePositionOnce;

    public UnifiedPanelWindow(Plugin plugin)
        : base("Classy Sentinel##ClassySentinel-Panel")
    {
        this.plugin = plugin;
        IsOpen = true;
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        Position = GetConfiguredOrDefaultPosition();
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions()
        => plugin.Configuration.Visible
           && Plugin.PlayerState.IsLoaded
           && BuildNavigationRows().Count > 0;

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoDocking;

        if (plugin.Configuration.Locked)
            Flags |= ImGuiWindowFlags.NoMove;
        if (plugin.Configuration.Locked && plugin.Configuration.ClickThroughWhenLocked)
            Flags |= ImGuiWindowFlags.NoInputs;

        if (forcePositionOnce)
            PositionCondition = ImGuiCond.Always;

        var scale = GetScale();
        ImGui.PushStyleColor(ImGuiCol.WindowBg, PanelBackground);
        ImGui.PushStyleColor(ImGuiCol.Border, PanelBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10f, 9f) * scale);
    }

    public override void Draw()
    {
        ImGui.SetWindowFontScale(Math.Clamp(plugin.Configuration.PanelScale, 0.6f, 1.6f));
        DrawPanelHeader();

        var firstCategory = true;
        foreach (var category in Enum.GetValues<JobCategory>())
        {
            var jobs = GetVisibleJobs(category);
            if (jobs.Length == 0)
                continue;

            if (!firstCategory)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            if (plugin.Configuration.ShowCategoryHeaders)
            {
                ImGui.TextColored(HeaderGold, category.DisplayName().ToUpperInvariant());
                ImGui.Spacing();
            }

            for (var index = 0; index < jobs.Length; index++)
            {
                DrawJobButton(jobs[index]);

                var shouldWrap = (index + 1) % Math.Max(1, plugin.Configuration.ButtonsPerRow) == 0;
                if (!shouldWrap && index < jobs.Length - 1)
                    ImGui.SameLine();
            }

            firstCategory = false;
        }

        if (plugin.Controller.IsActive)
            DrawControllerFooter();
    }

    public override void PostDraw()
    {
        plugin.RememberPanelPosition(ImGui.GetWindowPos());

        if (forcePositionOnce)
        {
            forcePositionOnce = false;
            PositionCondition = ImGuiCond.FirstUseEver;
        }

        ImGui.PopStyleVar(StyleVarCount);
        ImGui.PopStyleColor(StyleColorCount);
    }

    public IReadOnlyList<NavigationRow> BuildNavigationRows()
    {
        var rows = new List<NavigationRow>();
        var rowSize = Math.Max(1, plugin.Configuration.ButtonsPerRow);

        foreach (var category in Enum.GetValues<JobCategory>())
        {
            var jobs = GetVisibleJobs(category);
            for (var index = 0; index < jobs.Length; index += rowSize)
            {
                rows.Add(new NavigationRow(
                    category,
                    jobs.Skip(index).Take(rowSize).Select(job => job.Key).ToArray()));
            }
        }

        return rows;
    }

    public void ResetPosition()
    {
        Position = GetDefaultPosition();
        forcePositionOnce = true;
    }

    private IGrouping<uint, GearsetInfo>[] GetVisibleJobs(JobCategory category)
    {
        if (!plugin.Configuration.IsCategoryVisible(category))
            return Array.Empty<IGrouping<uint, GearsetInfo>>();

        return plugin.Gearsets.JobsIn(category)
            .Where(job => plugin.Configuration.IsJobVisible(job.Key))
            .ToArray();
    }

    private void DrawPanelHeader()
    {
        ImGui.TextColored(HeaderGold, "CLASSY SENTINEL");
        ImGui.SameLine();

        var active = plugin.Controller.IsActive;
        ImGui.PushStyleColor(ImGuiCol.Button, active ? ControllerIdle : ButtonIdle);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ButtonActive);
        if (ImGui.SmallButton(active ? "EXIT GAMEPAD##controller" : "GAMEPAD FOCUS##controller"))
        {
            if (active)
                plugin.DeactivateControllerFocus();
            else
                plugin.ActivateControllerFocus();
        }
        ImGui.PopStyleColor(3);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(active
                ? "Return controller input to FFXIV."
                : "Intentionally focus this panel for controller navigation.");

        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawJobButton(IGrouping<uint, GearsetInfo> jobGearsets)
    {
        var gearsets = jobGearsets.OrderBy(x => x.GearsetId).ToArray();
        var defaultGearset = plugin.Gearsets.GetDefault(jobGearsets.Key) ?? gearsets[0];
        var isCurrent = Plugin.PlayerState.ClassJob.IsValid
                        && Plugin.PlayerState.ClassJob.RowId == jobGearsets.Key;
        var isControllerFocused = plugin.Controller.IsActive
                                  && plugin.Controller.FocusedClassJobId == jobGearsets.Key;
        var size = new Vector2(plugin.Configuration.ButtonSize) * GetScale();

        ImGui.PushID($"job-{jobGearsets.Key}");
        ImGui.PushStyleColor(ImGuiCol.Button, isControllerFocused && !isCurrent ? ControllerIdle : isCurrent ? CurrentIdle : ButtonIdle);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, isCurrent ? CurrentHover : ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.Border, isControllerFocused ? ControllerBlue : isCurrent ? HeaderGold : PanelBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, (isControllerFocused || isCurrent ? 2f : 1f) * GetScale());
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f * GetScale());

        bool clicked;
        if (defaultGearset.IconId != 0)
        {
            var texture = Plugin.TextureProvider
                .GetFromGameIcon(new GameIconLookup(defaultGearset.IconId))
                .GetWrapOrDefault();
            clicked = texture is not null
                ? ImGui.ImageButton(texture.Handle, size, Vector2.Zero, Vector2.One, Vector4.Zero, Vector4.One)
                : ImGui.Button(defaultGearset.JobAbbreviation, size);
        }
        else
        {
            clicked = ImGui.Button(defaultGearset.JobAbbreviation, size);
        }

        if (clicked)
            plugin.Gearsets.Equip(defaultGearset);

        if (ImGui.IsItemHovered())
            DrawTooltip(defaultGearset, gearsets.Length, isCurrent, isControllerFocused);

        DrawMouseGearsetMenu(jobGearsets.Key, defaultGearset, gearsets);

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
        ImGui.PopID();
    }

    private void DrawMouseGearsetMenu(uint classJobId, GearsetInfo defaultGearset, IReadOnlyList<GearsetInfo> gearsets)
    {
        if (!ImGui.BeginPopupContextItem("gearsets"))
            return;

        ImGui.TextColored(HeaderGold, $"{defaultGearset.JobName} ({defaultGearset.JobAbbreviation})");
        ImGui.TextDisabled("Choose a gear set");
        ImGui.Separator();

        foreach (var gearset in gearsets)
        {
            var isDefault = gearset.GearsetId == defaultGearset.GearsetId;
            var label = $"{(isDefault ? "* " : string.Empty)}{gearset.GearsetName}  [#{gearset.GearsetId + 1}]##equip-{gearset.GearsetId}";
            if (ImGui.MenuItem(label))
                plugin.Gearsets.Equip(gearset);
        }

        if (gearsets.Count > 1 && ImGui.BeginMenu("Set default gear set"))
        {
            foreach (var gearset in gearsets)
            {
                var isDefault = gearset.GearsetId == defaultGearset.GearsetId;
                if (ImGui.MenuItem($"{gearset.GearsetName}##default-{gearset.GearsetId}", string.Empty, isDefault))
                    plugin.SetDefaultGearset(classJobId, gearset.GearsetId);
            }

            ImGui.EndMenu();
        }

        ImGui.EndPopup();
    }

    private void DrawControllerFooter()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (plugin.Controller.IsGearsetPickerOpen)
        {
            ImGui.TextColored(ControllerBlue, "ALTERNATE GEAR SET");
            var gearsets = plugin.Controller.PickerGearsets;
            for (var index = 0; index < gearsets.Count; index++)
            {
                var gearset = gearsets[index];
                var isSelected = index == plugin.Controller.PickerIndex;
                var isDefault = plugin.Gearsets.GetDefault(gearset.ClassJobId)?.GearsetId == gearset.GearsetId;
                ImGui.TextColored(
                    isSelected ? ControllerBlue : Vector4.One,
                    $"{(isSelected ? ">" : " ")} {(isDefault ? "*" : " ")} {gearset.GearsetName} [#{gearset.GearsetId + 1}]");
            }

            ImGui.TextDisabled("D-pad/Stick choose  |  A equip  |  Y default  |  B back");
        }
        else
        {
            ImGui.TextColored(ControllerBlue, "CONTROLLER FOCUS ACTIVE");
            ImGui.TextDisabled("D-pad/Stick move  |  A equip  |  X gear sets  |  B exit");
        }
    }

    private static void DrawTooltip(GearsetInfo gearset, int gearsetCount, bool isCurrent, bool isControllerFocused)
    {
        ImGui.BeginTooltip();
        ImGui.TextColored(HeaderGold, $"{gearset.JobName} ({gearset.JobAbbreviation})");
        ImGui.Text($"Default: {gearset.GearsetName} [#{gearset.GearsetId + 1}]");
        if (gearset.ItemLevel > 0)
            ImGui.TextDisabled($"Item level {gearset.ItemLevel}");
        if (isCurrent)
            ImGui.TextColored(HeaderGold, "Current job");
        if (isControllerFocused)
            ImGui.TextColored(ControllerBlue, "Controller focus");
        ImGui.TextDisabled(gearsetCount > 1
            ? $"Right-click for {gearsetCount} gear sets and default selection."
            : "Right-click for gear set options.");
        ImGui.EndTooltip();
    }

    private Vector2 GetConfiguredOrDefaultPosition()
    {
        var saved = plugin.Configuration.PanelPosition;
        return saved is null ? GetDefaultPosition() : new Vector2(saved.X, saved.Y);
    }

    private static Vector2 GetDefaultPosition() => new(180f, 220f);

    private float GetScale()
        => ImGuiHelpers.GlobalScale * Math.Clamp(plugin.Configuration.PanelScale, 0.6f, 1.6f);
}
