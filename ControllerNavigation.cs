using Dalamud.Game.ClientState.GamePad;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace ClassySentinel;

public sealed class ControllerNavigation : IDisposable
{
    private static readonly GamepadButtonsFlags SelectorButtons =
        GamepadButtonsFlags.R3
        | GamepadButtonsFlags.DPadUp
        | GamepadButtonsFlags.DPadDown
        | GamepadButtonsFlags.DPadLeft
        | GamepadButtonsFlags.DPadRight
        | GamepadButtonsFlags.Cross
        | GamepadButtonsFlags.Circle;

    private readonly Plugin plugin;
    private readonly IGamepadState gamepadState;
    private readonly RepeatGate leftGate = new();
    private readonly RepeatGate rightGate = new();
    private readonly RepeatGate upGate = new();
    private readonly RepeatGate downGate = new();
    private readonly EdgeGate r3Gate = new();
    private readonly EdgeGate crossGate = new();
    private readonly EdgeGate circleGate = new();

    private uint? selectedClassJobId;
    private GamepadButtonsFlags buttonsAwaitingRelease;

    public ControllerNavigation(Plugin plugin, IGamepadState gamepadState)
    {
        this.plugin = plugin;
        this.gamepadState = gamepadState;
    }

    public bool IsActive { get; private set; }

    // This is the one authoritative temporary controller selection.
    public uint? SelectedClassJobId => selectedClassJobId;

    public void Update(IReadOnlyList<NavigationRow> rows, bool selectorAvailable)
    {
        SuppressConsumedButtonsUntilReleased();

        // Update every action edge on every frame. This prevents an input held
        // while opening or closing the selector from becoming a second action.
        var r3Pressed = r3Gate.Pressed(IsDown(GamepadButtons.R3));
        var crossPressed = crossGate.Pressed(IsDown(GamepadButtons.South));
        var circlePressed = circleGate.Pressed(IsDown(GamepadButtons.East));

        if (!selectorAvailable || rows.Count == 0)
        {
            Deactivate();
            return;
        }

        if (!IsActive)
        {
            if (r3Pressed)
            {
                Activate(rows);
                SuppressSelectorButtons();
            }

            return;
        }

        // Filter only the buttons owned by this temporary selector. We never
        // enable Dalamud's global ImGui gamepad navigation, so the virtual mouse
        // cursor remains untouched.
        SuppressSelectorButtons();

        if (r3Pressed || circlePressed)
        {
            Deactivate();
            return;
        }

        EnsureValidSelection(rows);

        if (crossPressed && selectedClassJobId.HasValue)
        {
            var classJobId = selectedClassJobId.Value;

            // Close first so this physical press can produce at most one equip
            // request and normal gameplay resumes immediately afterward.
            Deactivate();
            plugin.Gearsets.EquipDefault(classJobId);
            return;
        }

        var now = DateTime.UtcNow;
        if (leftGate.Pulse(IsDown(GamepadButtons.DpadLeft), now))
            MoveHorizontal(rows, -1);
        else if (rightGate.Pulse(IsDown(GamepadButtons.DpadRight), now))
            MoveHorizontal(rows, 1);
        else if (upGate.Pulse(IsDown(GamepadButtons.DpadUp), now))
            MoveVertical(rows, -1);
        else if (downGate.Pulse(IsDown(GamepadButtons.DpadDown), now))
            MoveVertical(rows, 1);
    }

    public void Activate(IReadOnlyList<NavigationRow> rows)
    {
        if (rows.Count == 0)
            return;

        IsActive = true;
        ResetMovementState();

        var currentJobId = Plugin.PlayerState.ClassJob.IsValid
            ? Plugin.PlayerState.ClassJob.RowId
            : 0;
        selectedClassJobId = rows.Any(row => row.ClassJobIds.Contains(currentJobId))
            ? currentJobId
            : rows[0].ClassJobIds[0];
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        LatchHeldSelectorButtons();
        if (buttonsAwaitingRelease != GamepadButtonsFlags.None)
            SuppressButtons(buttonsAwaitingRelease);
        IsActive = false;
        selectedClassJobId = null;
        ResetMovementState();
    }

    public void Dispose() => Deactivate();

    private void EnsureValidSelection(IReadOnlyList<NavigationRow> rows)
    {
        if (selectedClassJobId.HasValue && rows.Any(row => row.ClassJobIds.Contains(selectedClassJobId.Value)))
            return;

        var currentJobId = Plugin.PlayerState.ClassJob.IsValid
            ? Plugin.PlayerState.ClassJob.RowId
            : 0;
        selectedClassJobId = rows.Any(row => row.ClassJobIds.Contains(currentJobId))
            ? currentJobId
            : rows[0].ClassJobIds[0];
    }

    private void MoveHorizontal(IReadOnlyList<NavigationRow> rows, int delta)
    {
        if (!TryFindSelectedCell(rows, out var rowIndex, out var column))
            return;

        var row = rows[rowIndex].ClassJobIds;
        selectedClassJobId = row[Math.Clamp(column + delta, 0, row.Count - 1)];
    }

    private void MoveVertical(IReadOnlyList<NavigationRow> rows, int delta)
    {
        if (!TryFindSelectedCell(rows, out var rowIndex, out var column))
            return;

        var nextRowIndex = Math.Clamp(rowIndex + delta, 0, rows.Count - 1);
        var nextRow = rows[nextRowIndex].ClassJobIds;
        selectedClassJobId = nextRow[Math.Min(column, nextRow.Count - 1)];
    }

    private bool TryFindSelectedCell(IReadOnlyList<NavigationRow> rows, out int rowIndex, out int column)
    {
        for (var row = 0; row < rows.Count; row++)
        {
            for (var col = 0; col < rows[row].ClassJobIds.Count; col++)
            {
                if (rows[row].ClassJobIds[col] == selectedClassJobId)
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

    private void LatchHeldSelectorButtons()
    {
        foreach (var (button, flag) in ButtonMappings())
        {
            if (IsDown(button))
                buttonsAwaitingRelease |= flag;
        }
    }

    private void SuppressConsumedButtonsUntilReleased()
    {
        if (buttonsAwaitingRelease == GamepadButtonsFlags.None)
            return;

        var suppressThisFrame = buttonsAwaitingRelease;
        var stillHeld = GamepadButtonsFlags.None;
        foreach (var (button, flag) in ButtonMappings())
        {
            if ((buttonsAwaitingRelease & flag) != 0 && IsDown(button))
                stillHeld |= flag;
        }

        buttonsAwaitingRelease = stillHeld;
        // Also filter the release frame before dropping the latch.
        SuppressButtons(suppressThisFrame);
    }

    private void SuppressSelectorButtons() => SuppressButtons(SelectorButtons);

    private unsafe void SuppressButtons(GamepadButtonsFlags buttons)
    {
        if (gamepadState.GamepadInputAddress == 0)
            return;

        var padDevice = (PadDevice*)gamepadState.GamepadInputAddress;
        ref var input = ref padDevice->GamepadInputData;
        var keepMask = ~buttons;

        input.Buttons &= keepMask;
        input.ButtonsPressed &= keepMask;
        input.ButtonsReleased &= keepMask;
        input.ButtonsRepeat &= keepMask;

        if ((buttons & GamepadButtonsFlags.R3) != 0)
            input.R3 = 0;
        if ((buttons & GamepadButtonsFlags.Cross) != 0)
            input.Cross = 0;
        if ((buttons & GamepadButtonsFlags.Circle) != 0)
            input.Circle = 0;
        if ((buttons & GamepadButtonsFlags.DPadUp) != 0)
            input.DPadUp = 0;
        if ((buttons & GamepadButtonsFlags.DPadDown) != 0)
            input.DPadDown = 0;
        if ((buttons & GamepadButtonsFlags.DPadLeft) != 0)
            input.DPadLeft = 0;
        if ((buttons & GamepadButtonsFlags.DPadRight) != 0)
            input.DPadRight = 0;
    }

    private static IEnumerable<(GamepadButtons Button, GamepadButtonsFlags Flag)> ButtonMappings()
    {
        yield return (GamepadButtons.R3, GamepadButtonsFlags.R3);
        yield return (GamepadButtons.DpadUp, GamepadButtonsFlags.DPadUp);
        yield return (GamepadButtons.DpadDown, GamepadButtonsFlags.DPadDown);
        yield return (GamepadButtons.DpadLeft, GamepadButtonsFlags.DPadLeft);
        yield return (GamepadButtons.DpadRight, GamepadButtonsFlags.DPadRight);
        yield return (GamepadButtons.South, GamepadButtonsFlags.Cross);
        yield return (GamepadButtons.East, GamepadButtonsFlags.Circle);
    }

    private bool IsDown(GamepadButtons button) => gamepadState.Raw(button) > 0.5f;

    private void ResetMovementState()
    {
        leftGate.Reset();
        rightGate.Reset();
        upGate.Reset();
        downGate.Reset();
    }

    private sealed class EdgeGate
    {
        private bool wasDown;

        public bool Pressed(bool isDown)
        {
            var pressed = isDown && !wasDown;
            wasDown = isDown;
            return pressed;
        }
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
