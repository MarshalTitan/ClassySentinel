using Dalamud.Game.ClientState.GamePad;
using Dalamud.Hooking;
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
    private readonly Hook<PadDevice.Delegates.Poll> gamepadPollHook;
    private readonly RepeatGate leftGate = new();
    private readonly RepeatGate rightGate = new();
    private readonly RepeatGate upGate = new();
    private readonly RepeatGate downGate = new();
    private readonly EdgeGate r3Gate = new();
    private readonly EdgeGate crossGate = new();
    private readonly EdgeGate circleGate = new();

    private GearsetReference? selectedGearset;
    private GamepadButtonsFlags buttonsAwaitingRelease;
    private GamepadButtonsFlags latestRawButtons;
    private bool selectorAvailable;
    private bool pollR3WasDown;
    private bool captureFaulted;

    public unsafe ControllerNavigation(Plugin plugin, IGameInteropProvider gameInteropProvider)
    {
        this.plugin = plugin;
        gamepadPollHook = gameInteropProvider.HookFromAddress<PadDevice.Delegates.Poll>(
            (nint)PadDevice.StaticVirtualTablePointer->Poll,
            GamepadPollDetour);
        gamepadPollHook.Enable();
    }

    public bool IsActive { get; private set; }

    // This is the one authoritative temporary controller selection.
    public GearsetReference? SelectedGearset => selectedGearset;

    public void Update(IReadOnlyList<NavigationRow> rows, bool isSelectorAvailable)
    {
        selectorAvailable = !captureFaulted && isSelectorAvailable && rows.Count > 0;
        UpdateButtonsAwaitingRelease();

        // Update every action edge on every frame. This prevents an input held
        // while opening or closing the selector from becoming a second action.
        var r3Pressed = r3Gate.Pressed(IsDown(GamepadButtons.R3));
        var crossPressed = crossGate.Pressed(IsDown(GamepadButtons.South));
        var circlePressed = circleGate.Pressed(IsDown(GamepadButtons.East));

        if (!selectorAvailable)
        {
            Deactivate();
            return;
        }

        if (!IsActive)
        {
            if (r3Pressed)
                Activate(rows);

            return;
        }

        if (r3Pressed || circlePressed)
        {
            Deactivate();
            return;
        }

        EnsureValidSelection(rows);

        if (crossPressed && selectedGearset is not null)
        {
            var gearset = selectedGearset;

            // Close first so this physical press can produce at most one equip
            // request and normal gameplay resumes immediately afterward.
            Deactivate();
            plugin.Gearsets.Equip(gearset);
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

        selectedGearset = FindInitialSelection(rows);
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        LatchHeldSelectorButtons();
        IsActive = false;
        selectedGearset = null;
        ResetMovementState();
        plugin.CloseManualPanel();
    }

    public void Dispose()
    {
        Deactivate();
        gamepadPollHook.Dispose();
    }

    private void EnsureValidSelection(IReadOnlyList<NavigationRow> rows)
    {
        if (selectedGearset is not null && rows.Any(row => row.Gearsets.Contains(selectedGearset)))
            return;

        selectedGearset = FindInitialSelection(rows);
    }

    private void MoveHorizontal(IReadOnlyList<NavigationRow> rows, int delta)
    {
        if (!TryFindSelectedCell(rows, out var rowIndex, out var column))
            return;

        var row = rows[rowIndex].Gearsets;
        selectedGearset = row[Math.Clamp(column + delta, 0, row.Count - 1)];
    }

    private void MoveVertical(IReadOnlyList<NavigationRow> rows, int delta)
    {
        if (!TryFindSelectedCell(rows, out var rowIndex, out var column))
            return;

        var nextRowIndex = Math.Clamp(rowIndex + delta, 0, rows.Count - 1);
        var nextRow = rows[nextRowIndex].Gearsets;
        selectedGearset = nextRow[Math.Min(column, nextRow.Count - 1)];
    }

    private bool TryFindSelectedCell(IReadOnlyList<NavigationRow> rows, out int rowIndex, out int column)
    {
        for (var row = 0; row < rows.Count; row++)
        {
            for (var col = 0; col < rows[row].Gearsets.Count; col++)
            {
                if (rows[row].Gearsets[col].Equals(selectedGearset))
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

    private GearsetReference FindInitialSelection(IReadOnlyList<NavigationRow> rows)
    {
        var visible = rows.SelectMany(row => row.Gearsets).ToArray();
        var currentGearset = plugin.Gearsets.GetCurrentGearset();
        if (currentGearset is not null)
        {
            var currentReference = GearsetReference.From(currentGearset);
            if (visible.Contains(currentReference))
                return currentReference;
        }

        var currentJobId = Plugin.PlayerState.ClassJob.IsValid
            ? Plugin.PlayerState.ClassJob.RowId
            : 0;
        var currentDefault = plugin.Gearsets.GetDefault(currentJobId);
        if (currentDefault is not null)
        {
            var defaultReference = GearsetReference.From(currentDefault);
            if (visible.Contains(defaultReference))
                return defaultReference;
        }

        return visible[0];
    }

    private void LatchHeldSelectorButtons()
    {
        foreach (var (button, flag) in ButtonMappings())
        {
            if (IsDown(button))
                buttonsAwaitingRelease |= flag;
        }
    }

    private void UpdateButtonsAwaitingRelease()
    {
        if (buttonsAwaitingRelease == GamepadButtonsFlags.None)
            return;

        var stillHeld = GamepadButtonsFlags.None;
        foreach (var (button, flag) in ButtonMappings())
        {
            if ((buttonsAwaitingRelease & flag) != 0 && IsDown(button))
                stillHeld |= flag;
        }

        buttonsAwaitingRelease = stillHeld;
    }

    private unsafe nint GamepadPollDetour(PadDevice* padDevice)
    {
        var original = gamepadPollHook.Original(padDevice);

        try
        {
            // Capture the current physical buttons immediately after polling,
            // before FFXIV can act on them. The framework update consumes this
            // private snapshot; ImGui navigation and the virtual cursor are not
            // involved.
            latestRawButtons = padDevice->GamepadInputData.Buttons;

            var r3Down = IsFlagDown(GamepadButtonsFlags.R3);
            var openingPress = selectorAvailable && r3Down && !pollR3WasDown;
            pollR3WasDown = r3Down;

            var buttonsToSuppress = buttonsAwaitingRelease;
            if (IsActive || openingPress)
                buttonsToSuppress |= SelectorButtons;

            if (buttonsToSuppress != GamepadButtonsFlags.None)
                SuppressButtons(padDevice, buttonsToSuppress);
        }
        catch (Exception exception)
        {
            if (!captureFaulted)
                Plugin.Log.Error(exception, "Temporary Classy Sentinel controller capture failed; disabling R3 selection.");
            captureFaulted = true;
        }

        return original;
    }

    private static unsafe void SuppressButtons(PadDevice* padDevice, GamepadButtonsFlags buttons)
    {
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

    private bool IsDown(GamepadButtons button)
        => button switch
        {
            GamepadButtons.R3 => IsFlagDown(GamepadButtonsFlags.R3),
            GamepadButtons.DpadUp => IsFlagDown(GamepadButtonsFlags.DPadUp),
            GamepadButtons.DpadDown => IsFlagDown(GamepadButtonsFlags.DPadDown),
            GamepadButtons.DpadLeft => IsFlagDown(GamepadButtonsFlags.DPadLeft),
            GamepadButtons.DpadRight => IsFlagDown(GamepadButtonsFlags.DPadRight),
            GamepadButtons.South => IsFlagDown(GamepadButtonsFlags.Cross),
            GamepadButtons.East => IsFlagDown(GamepadButtonsFlags.Circle),
            _ => false,
        };

    private bool IsFlagDown(GamepadButtonsFlags button) => (latestRawButtons & button) != 0;

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
