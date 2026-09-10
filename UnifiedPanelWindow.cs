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
        => (plugin.ManualPanelOpen || plugin.Controller.IsActive)
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
            var gearsets = GetVisibleGearsets(category);
            if (gearsets.Count == 0)
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

            for (var index = 0; index < gearsets.Count; index++)
            {
                var gearset = gearsets[index];
                var duplicateCount = gearsets.Count(other => other.ClassJobId == gearset.ClassJobId);
                DrawGearsetButton(gearset, duplicateCount > 1);

                var shouldWrap = (index + 1) % Math.Max(1, plugin.Configuration.ButtonsPerRow) == 0;
                if (!shouldWrap && index < gearsets.Count - 1)
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
            var gearsets = GetVisibleGearsets(category);
            for (var index = 0; index < gearsets.Count; index += rowSize)
            {
                rows.Add(new NavigationRow(
                    category,
                    gearsets.Skip(index).Take(rowSize).Select(GearsetReference.From).ToArray()));
            }
        }

        return rows;
    }

    public void ResetPosition()
    {
        Position = GetDefaultPosition();
        forcePositionOnce = true;
    }

    private IReadOnlyList<GearsetInfo> GetVisibleGearsets(JobCategory category)
    {
        if (!plugin.Configuration.IsCategoryVisible(category))
            return Array.Empty<GearsetInfo>();

        return plugin.Gearsets.VisibleGearsetsIn(category);
    }

    private void DrawPanelHeader()
    {
        ImGui.TextColored(HeaderGold, "CLASSY SENTINEL");
        if (plugin.Controller.IsActive)
        {
            ImGui.SameLine();
            ImGui.TextColored(ControllerBlue, "R3 SELECT");
        }

        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawGearsetButton(GearsetInfo gearset, bool hasDuplicateJob)
    {
        var defaultGearset = plugin.Gearsets.GetDefault(gearset.ClassJobId);
        var isDefault = defaultGearset is not null && defaultGearset.GearsetId == gearset.GearsetId;
        var currentGearset = plugin.Gearsets.GetCurrentGearset();
        var isCurrent = currentGearset is not null
            ? currentGearset.GearsetId == gearset.GearsetId && currentGearset.ClassJobId == gearset.ClassJobId
            : isDefault && Plugin.PlayerState.ClassJob.IsValid && Plugin.PlayerState.ClassJob.RowId == gearset.ClassJobId;
        var reference = GearsetReference.From(gearset);
        var isControllerFocused = plugin.Controller.IsActive
                                  && reference.Equals(plugin.Controller.SelectedGearset);
        var size = new Vector2(plugin.Configuration.ButtonSize) * GetScale();

        ImGui.PushID($"gearset-{reference.ImGuiId}");
        ImGui.PushStyleColor(ImGuiCol.Button, isControllerFocused && !isCurrent ? ControllerIdle : isCurrent ? CurrentIdle : ButtonIdle);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, isCurrent ? CurrentHover : ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.Border, isControllerFocused ? ControllerBlue : isCurrent ? HeaderGold : PanelBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, (isControllerFocused || isCurrent ? 2f : 1f) * GetScale());
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f * GetScale());

        bool clicked;
        if (gearset.IconId != 0)
        {
            var texture = Plugin.TextureProvider
                .GetFromGameIcon(new GameIconLookup(gearset.IconId))
                .GetWrapOrDefault();
            clicked = texture is not null
                ? ImGui.ImageButton(texture.Handle, size, Vector2.Zero, Vector2.One, Vector4.Zero, Vector4.One)
                : ImGui.Button(gearset.JobAbbreviation, size);
        }
        else
        {
            clicked = ImGui.Button(gearset.JobAbbreviation, size);
        }

        if (clicked)
            plugin.Gearsets.Equip(gearset);

        if (hasDuplicateJob)
            DrawDuplicateBadge(gearset);

        if (ImGui.IsItemHovered())
            DrawTooltip(gearset, isDefault, isCurrent, isControllerFocused);

        DrawMouseGearsetMenu(gearset, isDefault);

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
        ImGui.PopID();
    }

    private void DrawMouseGearsetMenu(GearsetInfo gearset, bool isDefault)
    {
        if (!ImGui.BeginPopupContextItem("gearsets"))
            return;

        ImGui.TextColored(HeaderGold, $"{gearset.JobName} ({gearset.JobAbbreviation})");
        ImGui.Text($"Gear Set: {gearset.GearsetName}");
        ImGui.TextDisabled($"Gear Set #{gearset.GearsetId + 1}");
        ImGui.Separator();

        if (ImGui.MenuItem("Equip this gear set"))
            plugin.Gearsets.Equip(gearset);

        if (!isDefault && ImGui.MenuItem("Make default for this job"))
            plugin.SetDefaultGearset(gearset);

        var visibilityLabel = isDefault ? "Hide default tile" : "Remove from launcher";
        if (ImGui.MenuItem(visibilityLabel))
        {
            if (isDefault)
                plugin.SetDefaultGearsetVisibility(gearset, false);
            else
                plugin.SetAdditionalGearsetVisibility(gearset, false);
        }

        ImGui.EndPopup();
    }

    private void DrawDuplicateBadge(GearsetInfo gearset)
    {
        var badge = $"#{gearset.GearsetId + 1}";
        var scale = GetScale();
        var textSize = ImGui.CalcTextSize(badge);
        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();
        var padding = new Vector2(3f, 1f) * scale;
        var badgeSize = textSize + (padding * 2f);
        var badgeMin = new Vector2(itemMax.X - badgeSize.X - (2f * scale), itemMax.Y - badgeSize.Y - (2f * scale));
        var badgeMax = badgeMin + badgeSize;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(badgeMin, badgeMax, ImGui.ColorConvertFloat4ToU32(PanelBackground), 3f * scale);
        drawList.AddText(badgeMin + padding, ImGui.ColorConvertFloat4ToU32(HeaderGold), badge);
    }

    private void DrawControllerFooter()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(ControllerBlue, "R3 MODE  |  X EQUIP");
    }

    private static void DrawTooltip(GearsetInfo gearset, bool isDefault, bool isCurrent, bool isControllerFocused)
    {
        ImGui.BeginTooltip();
        ImGui.TextColored(HeaderGold, $"{gearset.JobName} ({gearset.JobAbbreviation})");
        ImGui.Text($"Gear Set: {gearset.GearsetName}");
        ImGui.TextDisabled($"Gear Set #{gearset.GearsetId + 1}");
        if (gearset.ItemLevel > 0)
            ImGui.TextDisabled($"Item level {gearset.ItemLevel}");
        if (isDefault)
            ImGui.TextColored(HeaderGold, "Default gear set");
        if (isCurrent)
            ImGui.TextColored(HeaderGold, "Currently equipped gear set");
        if (isControllerFocused)
            ImGui.TextColored(ControllerBlue, "Controller selection");
        ImGui.TextDisabled("Right-click for gear-set options.");
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
