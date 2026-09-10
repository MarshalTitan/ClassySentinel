using Dalamud.Game.ClientState.GamePad;
using Dalamud.Plugin.Services;

namespace ClassySentinel;

public sealed class ControllerNavigation : IDisposable
{
    private const float StickThreshold = 0.65f;

    private readonly Plugin plugin;
    private readonly IGamepadState gamepadState;
    private readonly RepeatGate leftGate = new();
    private readonly RepeatGate rightGate = new();
    private readonly RepeatGate upGate = new();
    private readonly RepeatGate downGate = new();
    private readonly Dictionary<GamepadButtons, bool> priorButtons = new();

    private bool restoreGamepadNav;
    private bool chordWasDown;
    private uint? focusedClassJobId;
    private GearsetInfo[] pickerGearsets = Array.Empty<GearsetInfo>();
    private int pickerIndex;

    public ControllerNavigation(Plugin plugin, IGamepadState gamepadState)
    {
        this.plugin = plugin;
        this.gamepadState = gamepadState;
    }

    public bool IsActive { get; private set; }

    public bool IsGearsetPickerOpen => pickerGearsets.Length > 0;

    public uint? FocusedClassJobId => focusedClassJobId;

    public IReadOnlyList<GearsetInfo> PickerGearsets => pickerGearsets;

    public int PickerIndex => pickerIndex;

    public void Update(IReadOnlyList<NavigationRow> rows, bool selectorAvailable)
    {
        if (!selectorAvailable || rows.Count == 0)
        {
            Deactivate();
            return;
        }

        if (!IsActive)
        {
            UpdateActivationChord(rows);
            return;
        }

        // Raw button state remains reliable while Dalamud is intercepting input.
        gamepadState.EnableGamepadNav = true;
        EnsureValidFocus(rows);

        var now = DateTime.UtcNow;
        var stick = gamepadState.LeftStick;
        var moveLeft = leftGate.Pulse(IsDown(GamepadButtons.DpadLeft) || stick.X <= -StickThreshold, now);
        var moveRight = rightGate.Pulse(IsDown(GamepadButtons.DpadRight) || stick.X >= StickThreshold, now);
        var moveUp = upGate.Pulse(IsDown(GamepadButtons.DpadUp) || stick.Y >= StickThreshold, now);
        var moveDown = downGate.Pulse(IsDown(GamepadButtons.DpadDown) || stick.Y <= -StickThreshold, now);

        if (IsGearsetPickerOpen)
        {
            if (moveLeft || moveUp)
                MovePicker(-1);
            else if (moveRight || moveDown)
                MovePicker(1);

            var equipPressed = ButtonPressed(GamepadButtons.South);
            var defaultPressed = ButtonPressed(GamepadButtons.North);
            var backPressed = ButtonPressed(GamepadButtons.East);

            if (equipPressed)
            {
                plugin.Gearsets.Equip(pickerGearsets[pickerIndex]);
                CloseGearsetPicker();
            }
            else if (defaultPressed)
            {
                var selected = pickerGearsets[pickerIndex];
                plugin.SetDefaultGearset(selected.ClassJobId, selected.GearsetId);
            }
            else if (backPressed)
            {
                CloseGearsetPicker();
            }

            return;
        }

        if (moveLeft)
            MoveHorizontal(rows, -1);
        else if (moveRight)
            MoveHorizontal(rows, 1);
        else if (moveUp)
            MoveVertical(rows, -1);
        else if (moveDown)
            MoveVertical(rows, 1);

        var equipDefaultPressed = ButtonPressed(GamepadButtons.South);
        var pickerPressed = ButtonPressed(GamepadButtons.West);
        var exitPressed = ButtonPressed(GamepadButtons.East);

        if (equipDefaultPressed && focusedClassJobId.HasValue)
            plugin.Gearsets.EquipDefault(focusedClassJobId.Value);
        else if (pickerPressed)
            OpenGearsetPicker();
        else if (exitPressed)
            Deactivate();
    }

    public void Activate(IReadOnlyList<NavigationRow> rows)
    {
        if (rows.Count == 0)
            return;

        if (!IsActive)
        {
            restoreGamepadNav = gamepadState.EnableGamepadNav;
            IsActive = true;
            PrimeActionButtons();
        }

        gamepadState.EnableGamepadNav = true;
        EnsureValidFocus(rows);
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        focusedClassJobId = null;
        CloseGearsetPicker();
        ResetInputState();
        gamepadState.EnableGamepadNav = restoreGamepadNav;
    }

    public void Dispose() => Deactivate();

    private void UpdateActivationChord(IReadOnlyList<NavigationRow> rows)
    {
        if (!plugin.Configuration.EnableControllerActivationChord)
        {
            chordWasDown = false;
            return;
        }

        // Optional and off by default. Reading does not consume gameplay input.
        var chordDown = IsDown(GamepadButtons.L1) && IsDown(GamepadButtons.R1);
        if (chordDown && !chordWasDown)
            Activate(rows);
        chordWasDown = chordDown;
    }

    private void EnsureValidFocus(IReadOnlyList<NavigationRow> rows)
    {
        if (focusedClassJobId.HasValue && rows.Any(row => row.ClassJobIds.Contains(focusedClassJobId.Value)))
            return;

        var currentJobId = Plugin.PlayerState.ClassJob.IsValid
            ? Plugin.PlayerState.ClassJob.RowId
            : 0;
        focusedClassJobId = rows.Any(row => row.ClassJobIds.Contains(currentJobId))
            ? currentJobId
            : rows[0].ClassJobIds[0];
    }

    private void MoveHorizontal(IReadOnlyList<NavigationRow> rows, int delta)
    {
        if (!TryFindFocusedCell(rows, out var rowIndex, out var column))
            return;

        var row = rows[rowIndex].ClassJobIds;
        var nextColumn = (column + delta + row.Count) % row.Count;
        focusedClassJobId = row[nextColumn];
    }

    private void MoveVertical(IReadOnlyList<NavigationRow> rows, int delta)
    {
        if (!TryFindFocusedCell(rows, out var rowIndex, out var column))
            return;

        // Vertical movement clamps at the panel edges; the destination chooses
        // the closest available column in the next visible row.
        var nextRowIndex = Math.Clamp(rowIndex + delta, 0, rows.Count - 1);
        var nextRow = rows[nextRowIndex].ClassJobIds;
        focusedClassJobId = nextRow[Math.Min(column, nextRow.Count - 1)];
    }

    private bool TryFindFocusedCell(IReadOnlyList<NavigationRow> rows, out int rowIndex, out int column)
    {
        for (var row = 0; row < rows.Count; row++)
        {
            for (var col = 0; col < rows[row].ClassJobIds.Count; col++)
            {
                if (rows[row].ClassJobIds[col] == focusedClassJobId)
                {
                    rowIndex = row;
                    column = col;
                    return true;
                }
            }
        }

        rowIndex = 0;
        column = 0;
        return false;
    }

    private void OpenGearsetPicker()
    {
        if (!focusedClassJobId.HasValue)
            return;

        pickerGearsets = plugin.Gearsets.Gearsets
            .Where(x => x.ClassJobId == focusedClassJobId.Value)
            .OrderBy(x => x.GearsetId)
            .ToArray();

        if (pickerGearsets.Length <= 1)
        {
            CloseGearsetPicker();
            return;
        }

        var currentDefault = plugin.Gearsets.GetDefault(focusedClassJobId.Value);
        pickerIndex = Math.Max(0, Array.FindIndex(pickerGearsets, x => x.GearsetId == currentDefault?.GearsetId));
    }

    private void CloseGearsetPicker()
    {
        pickerGearsets = Array.Empty<GearsetInfo>();
        pickerIndex = 0;
    }

    private void MovePicker(int delta)
    {
        pickerIndex = (pickerIndex + delta + pickerGearsets.Length) % pickerGearsets.Length;
    }

    private bool ButtonPressed(GamepadButtons button)
    {
        var down = IsDown(button);
        var previous = priorButtons.TryGetValue(button, out var prior) && prior;
        priorButtons[button] = down;
        return down && !previous;
    }

    private bool IsDown(GamepadButtons button) => gamepadState.Raw(button) > 0.5f;

    private void PrimeActionButtons()
    {
        foreach (var button in new[] { GamepadButtons.South, GamepadButtons.East, GamepadButtons.West, GamepadButtons.North })
            priorButtons[button] = IsDown(button);
    }

    private void ResetInputState()
    {
        priorButtons.Clear();
        leftGate.Reset();
        rightGate.Reset();
        upGate.Reset();
        downGate.Reset();
    }

    private sealed class RepeatGate
    {
        private bool wasDown;
        private DateTime repeatAfterUtc;

        public bool Pulse(bool isDown, DateTime now)
        {
            if (!isDown)
            {
                Reset();
                return false;
            }

            if (!wasDown)
            {
                wasDown = true;
                repeatAfterUtc = now.AddMilliseconds(350);
                return true;
            }

            if (now < repeatAfterUtc)
                return false;

            repeatAfterUtc = now.AddMilliseconds(110);
            return true;
        }

        public void Reset()
        {
            wasDown = false;
            repeatAfterUtc = DateTime.MinValue;
        }
    }
}
