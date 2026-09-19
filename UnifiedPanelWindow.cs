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
    private static readonly Vector4 ControllerGlow = new(0.35f, 0.88f, 1f, 0.48f);
    private static readonly Vector4 ControllerLabelText = new(0.94f, 0.98f, 1f, 1f);
    private static readonly Vector2 MinimumReachableArea = new(48f, 32f);

    private readonly Plugin plugin;
    private bool forcePositionOnce = true;

    public UnifiedPanelWindow(Plugin plugin)
        : base("Classy Sentinel##ClassySentinel-Panel")
    {
        this.plugin = plugin;
        IsOpen = true;
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        Position = GetConfiguredOrDefaultPosition();
        PositionCondition = ImGuiCond.Always;
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
        {
            var viewport = ImGui.GetMainViewport();
            Position = PanelPositionPolicy.KeepReachable(
                GetConfiguredOrDefaultPosition(),
                GetDefaultPosition(),
                viewport.WorkPos,
                viewport.WorkSize,
                MinimumReachableArea);
            PositionCondition = ImGuiCond.Always;
        }

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
                var headerPalette = GetRolePalette(GetCategoryRoleHue(category));
                ImGui.TextColored(headerPalette.Header, category.DisplayName().ToUpperInvariant());
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

        RememberCurrentPosition();
    }

    public override void PostDraw()
    {
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
        var palette = GetRolePalette(gearset.RoleHue);
        var idle = isCurrent ? Blend(palette.Idle, CurrentIdle, 0.25f) : palette.Idle;
        var hover = isCurrent ? Blend(palette.Hover, CurrentHover, 0.25f) : palette.Hover;
        var border = isControllerFocused ? ControllerBlue : isCurrent ? HeaderGold : palette.Border;
        var borderSize = isControllerFocused ? 3f : isCurrent ? 2f : 1f;

        ImGui.PushID($"gearset-{reference.ImGuiId}");
        ImGui.PushStyleColor(ImGuiCol.Button, idle);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, palette.Active);
        ImGui.PushStyleColor(ImGuiCol.Border, border);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, borderSize * GetScale());
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

        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();
        if (isCurrent)
            DrawCurrentEquippedOverlay(itemMin, itemMax);

        if (hasDuplicateJob)
            DrawDuplicateBadge(gearset, itemMax);

        if (isControllerFocused)
        {
            DrawControllerFocusOverlay(itemMin, itemMax);
            DrawControllerSelectionLabel(gearset, itemMin, itemMax);
        }

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

    private void DrawDuplicateBadge(GearsetInfo gearset, Vector2 itemMax)
    {
        var badge = $"#{gearset.GearsetId + 1}";
        var scale = GetScale();
        var textSize = ImGui.CalcTextSize(badge);
        var padding = new Vector2(3f, 1f) * scale;
        var badgeSize = textSize + (padding * 2f);
        var badgeMin = new Vector2(itemMax.X - badgeSize.X - (2f * scale), itemMax.Y - badgeSize.Y - (2f * scale));
        var badgeMax = badgeMin + badgeSize;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(badgeMin, badgeMax, ImGui.ColorConvertFloat4ToU32(PanelBackground), 3f * scale);
        drawList.AddText(badgeMin + padding, ImGui.ColorConvertFloat4ToU32(HeaderGold), badge);
    }

    private void DrawControllerFocusOverlay(Vector2 itemMin, Vector2 itemMax)
    {
        var scale = GetScale();
        var expansion = new Vector2(2f * scale);
        ImGui.GetWindowDrawList().AddRect(
            itemMin - expansion,
            itemMax + expansion,
            ImGui.ColorConvertFloat4ToU32(ControllerGlow),
            6f * scale,
            ImDrawFlags.None,
            2f * scale);
    }

    private void DrawCurrentEquippedOverlay(Vector2 itemMin, Vector2 itemMax)
    {
        var scale = GetScale();
        var inset = new Vector2(2.5f * scale);
        ImGui.GetWindowDrawList().AddRect(
            itemMin + inset,
            itemMax - inset,
            ImGui.ColorConvertFloat4ToU32(HeaderGold),
            3.5f * scale,
            ImDrawFlags.None,
            1f * scale);
    }

    private void DrawControllerSelectionLabel(GearsetInfo gearset, Vector2 itemMin, Vector2 itemMax)
    {
        var scale = GetScale();
        var padding = new Vector2(7f, 4f) * scale;
        var textSize = ImGui.CalcTextSize(gearset.GearsetName);
        var labelSize = textSize + (padding * 2f);
        var labelMin = new Vector2(itemMin.X, itemMax.Y + (5f * scale));
        var viewport = ImGui.GetMainViewport();
        var workMin = viewport.WorkPos;
        var workMax = viewport.WorkPos + viewport.WorkSize;

        if (labelMin.X + labelSize.X > workMax.X)
            labelMin.X = workMax.X - labelSize.X;
        labelMin.X = Math.Max(workMin.X, labelMin.X);

        if (labelMin.Y + labelSize.Y > workMax.Y)
            labelMin.Y = itemMin.Y - labelSize.Y - (5f * scale);
        labelMin.Y = Math.Max(workMin.Y, labelMin.Y);

        var labelMax = labelMin + labelSize;
        var drawList = ImGui.GetForegroundDrawList();
        drawList.AddRectFilled(labelMin, labelMax, ImGui.ColorConvertFloat4ToU32(PanelBackground), 5f * scale);
        drawList.AddRect(labelMin, labelMax, ImGui.ColorConvertFloat4ToU32(ControllerBlue), 5f * scale);
        drawList.AddText(labelMin + padding, ImGui.ColorConvertFloat4ToU32(ControllerLabelText), gearset.GearsetName);
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

    private void RememberCurrentPosition()
    {
        // Draw() executes while this panel is still the active ImGui window.
        // WindowHost invokes PostDraw() only after ImGui.End(), so querying the
        // current window there can capture an unrelated window's coordinates.
        var position = ImGui.GetWindowPos();
        var viewport = ImGui.GetMainViewport();
        var reachablePosition = PanelPositionPolicy.KeepReachable(
            position,
            GetDefaultPosition(),
            viewport.WorkPos,
            viewport.WorkSize,
            ImGui.GetWindowSize());

        if (Vector2.DistanceSquared(position, reachablePosition) >= 0.25f)
        {
            ImGui.SetWindowPos(reachablePosition, ImGuiCond.Always);
            position = reachablePosition;
        }

        // Keep the Window fallback synchronized without forcing it each frame.
        Position = position;
        plugin.RememberPanelPosition(position);
    }

    private float GetScale()
        => ImGuiHelpers.GlobalScale * Math.Clamp(plugin.Configuration.PanelScale, 0.6f, 1.6f);

    private static RoleHue GetCategoryRoleHue(JobCategory category) => category switch
    {
        JobCategory.Tank => RoleHue.Tank,
        JobCategory.Healer => RoleHue.Healer,
        JobCategory.MeleeDps => RoleHue.Melee,
        JobCategory.PhysicalRangedDps => RoleHue.PhysicalRanged,
        JobCategory.MagicalRangedDps => RoleHue.MagicalRanged,
        _ => RoleHue.Neutral,
    };

    private static RolePalette GetRolePalette(RoleHue roleHue) => roleHue switch
    {
        RoleHue.Tank => new(
            new Vector4(0.045f, 0.090f, 0.155f, 0.98f),
            new Vector4(0.095f, 0.205f, 0.350f, 1f),
            new Vector4(0.145f, 0.285f, 0.460f, 1f),
            new Vector4(0.24f, 0.46f, 0.70f, 0.90f),
            new Vector4(0.46f, 0.68f, 0.91f, 1f)),
        RoleHue.Healer => new(
            new Vector4(0.045f, 0.125f, 0.075f, 0.98f),
            new Vector4(0.095f, 0.265f, 0.155f, 1f),
            new Vector4(0.145f, 0.365f, 0.215f, 1f),
            new Vector4(0.27f, 0.61f, 0.38f, 0.90f),
            new Vector4(0.48f, 0.80f, 0.56f, 1f)),
        RoleHue.Melee => new(
            new Vector4(0.145f, 0.055f, 0.060f, 0.98f),
            new Vector4(0.310f, 0.105f, 0.110f, 1f),
            new Vector4(0.430f, 0.150f, 0.155f, 1f),
            new Vector4(0.69f, 0.29f, 0.29f, 0.90f),
            new Vector4(0.91f, 0.49f, 0.49f, 1f)),
        RoleHue.PhysicalRanged => new(
            new Vector4(0.155f, 0.090f, 0.035f, 0.98f),
            new Vector4(0.330f, 0.185f, 0.065f, 1f),
            new Vector4(0.455f, 0.255f, 0.090f, 1f),
            new Vector4(0.76f, 0.46f, 0.18f, 0.90f),
            new Vector4(0.93f, 0.65f, 0.35f, 1f)),
        RoleHue.MagicalRanged => new(
            new Vector4(0.105f, 0.050f, 0.150f, 0.98f),
            new Vector4(0.225f, 0.105f, 0.325f, 1f),
            new Vector4(0.315f, 0.150f, 0.440f, 1f),
            new Vector4(0.57f, 0.32f, 0.73f, 0.90f),
            new Vector4(0.75f, 0.50f, 0.91f, 1f)),
        _ => new RolePalette(ButtonIdle, ButtonHover, ButtonActive, PanelBorder, HeaderGold),
    };

    private static Vector4 Blend(Vector4 first, Vector4 second, float amount)
        => Vector4.Lerp(first, second, Math.Clamp(amount, 0f, 1f));

    private readonly record struct RolePalette(
        Vector4 Idle,
        Vector4 Hover,
        Vector4 Active,
        Vector4 Border,
        Vector4 Header);
}
