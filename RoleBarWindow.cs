using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace ClassySentinel;

public sealed class RoleBarWindow : Window
{
    private const int StyleColorCount = 2;
    private const int StyleVarCount = 3;

    private static readonly Vector4 PanelBackground = new(0.035f, 0.04f, 0.05f, 0.92f);
    private static readonly Vector4 PanelBorder = new(0.58f, 0.47f, 0.27f, 0.88f);
    private static readonly Vector4 ButtonIdle = new(0.09f, 0.105f, 0.125f, 0.96f);
    private static readonly Vector4 ButtonHover = new(0.24f, 0.29f, 0.35f, 1f);
    private static readonly Vector4 ButtonActive = new(0.40f, 0.32f, 0.16f, 1f);
    private static readonly Vector4 CurrentIdle = new(0.43f, 0.32f, 0.10f, 1f);
    private static readonly Vector4 CurrentHover = new(0.68f, 0.52f, 0.18f, 1f);
    private static readonly Vector4 HeaderGold = new(0.92f, 0.78f, 0.44f, 1f);

    private readonly Plugin plugin;
    private readonly JobCategory category;
    private bool forcePositionOnce;

    public RoleBarWindow(Plugin plugin, JobCategory category)
        : base($"{category.DisplayName()}##ClassySentinel-{category}")
    {
        this.plugin = plugin;
        this.category = category;
        IsOpen = true;
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        Position = GetConfiguredOrDefaultPosition();
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions()
        => plugin.Configuration.Visible
           && plugin.Configuration.IsCategoryVisible(category)
           && Plugin.PlayerState.IsLoaded
           && plugin.Gearsets.JobsIn(category).Any();

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

        ImGui.PushStyleColor(ImGuiCol.WindowBg, PanelBackground);
        ImGui.PushStyleColor(ImGuiCol.Border, PanelBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 7f * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8f, 7f) * ImGuiHelpers.GlobalScale);
    }

    public override void Draw()
    {
        var jobs = plugin.Gearsets.JobsIn(category).ToArray();
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
    }

    public override void PostDraw()
    {
        var position = ImGui.GetWindowPos();
        plugin.RememberBarPosition(category, position);

        if (forcePositionOnce)
        {
            forcePositionOnce = false;
            PositionCondition = ImGuiCond.FirstUseEver;
        }

        ImGui.PopStyleVar(StyleVarCount);
        ImGui.PopStyleColor(StyleColorCount);
    }

    public void ResetPosition()
    {
        Position = GetDefaultPosition();
        forcePositionOnce = true;
    }

    private void DrawJobButton(IGrouping<uint, GearsetInfo> jobGearsets)
    {
        var gearsets = jobGearsets.OrderBy(x => x.GearsetId).ToArray();
        var defaultGearset = plugin.Gearsets.GetDefault(jobGearsets.Key) ?? gearsets[0];
        var isCurrent = Plugin.PlayerState.ClassJob.IsValid
                        && Plugin.PlayerState.ClassJob.RowId == jobGearsets.Key;
        var size = new Vector2(plugin.Configuration.ButtonSize) * ImGuiHelpers.GlobalScale;

        ImGui.PushID($"job-{jobGearsets.Key}");
        ImGui.PushStyleColor(ImGuiCol.Button, isCurrent ? CurrentIdle : ButtonIdle);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, isCurrent ? CurrentHover : ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.Border, isCurrent ? HeaderGold : PanelBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, (isCurrent ? 2f : 1f) * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f * ImGuiHelpers.GlobalScale);

        var clicked = false;
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
            DrawTooltip(defaultGearset, gearsets.Length, isCurrent);

        if (ImGui.BeginPopupContextItem("gearsets"))
        {
            ImGui.TextColored(HeaderGold, $"{defaultGearset.JobName} ({defaultGearset.JobAbbreviation})");
            ImGui.TextDisabled("Choose a gear set");
            ImGui.Separator();

            foreach (var gearset in gearsets)
            {
                var isDefault = gearset.GearsetId == defaultGearset.GearsetId;
                var label = $"{(isDefault ? "★ " : string.Empty)}{gearset.GearsetName}  [#{gearset.GearsetId + 1}]##equip-{gearset.GearsetId}";
                if (ImGui.MenuItem(label))
                    plugin.Gearsets.Equip(gearset);
            }

            if (gearsets.Length > 1 && ImGui.BeginMenu("Set default gear set"))
            {
                foreach (var gearset in gearsets)
                {
                    var isDefault = gearset.GearsetId == defaultGearset.GearsetId;
                    if (ImGui.MenuItem($"{gearset.GearsetName}##default-{gearset.GearsetId}", string.Empty, isDefault))
                        plugin.SetDefaultGearset(jobGearsets.Key, gearset.GearsetId);
                }

                ImGui.EndMenu();
            }

            ImGui.EndPopup();
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
        ImGui.PopID();
    }

    private static void DrawTooltip(GearsetInfo gearset, int gearsetCount, bool isCurrent)
    {
        ImGui.BeginTooltip();
        ImGui.TextColored(HeaderGold, $"{gearset.JobName} ({gearset.JobAbbreviation})");
        ImGui.Text($"Default: {gearset.GearsetName} [#{gearset.GearsetId + 1}]");
        if (gearset.ItemLevel > 0)
            ImGui.TextDisabled($"Item level {gearset.ItemLevel}");
        if (isCurrent)
            ImGui.TextColored(HeaderGold, "Current job");
        if (gearsetCount > 1)
            ImGui.TextDisabled($"Right-click for {gearsetCount} gear sets and default selection.");
        else
            ImGui.TextDisabled("Right-click for gear set options.");
        ImGui.EndTooltip();
    }

    private Vector2 GetConfiguredOrDefaultPosition()
    {
        if (plugin.Configuration.BarPositions.TryGetValue(category.ToString(), out var saved))
            return new Vector2(saved.X, saved.Y);
        return GetDefaultPosition();
    }

    private Vector2 GetDefaultPosition()
    {
        var ordinal = (int)category;
        return new Vector2(180f, 220f + (ordinal * 62f)) * ImGuiHelpers.GlobalScale;
    }
}

