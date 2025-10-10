using System;
using System.Runtime.InteropServices;
using System.Threading;
using Fub.Enums;

namespace Fub.Implementations.Input;

public static class InputManager
{
    // XInput button flags
    [Flags]
    private enum XINPUT_GAMEPAD_BUTTONS : ushort
    {
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState14(int dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState910(int dwUserIndex, out XINPUT_STATE pState);

    private static bool TryGetState(int userIndex, out XINPUT_STATE state)
    {
        // Try Win8+ first, then fallback
        if (XInputGetState14(userIndex, out state) == 0) return true;
        return XInputGetState910(userIndex, out state) == 0;
    }

    // Raw Input for controller vendor detection (best-effort)
    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICELIST
    {
        public IntPtr hDevice;
        public uint dwType;
    }

    private const uint RIM_TYPEHID = 2;

    [DllImport("User32.dll")]
    private static extern uint GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);

    [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

    private const uint RIDI_DEVICENAME = 0x20000007;

    // Repeat/hold config for movement
    private static InputAction _lastDir = InputAction.None;
    private static long _lastDirTimestamp;
    private static bool _didInitialRepeat;
    private const int InitialRepeatDelayMs = 260; // first delay when holding
    private const int RepeatIntervalMs = 110;     // subsequent repeats
    private const short AxisDeadzone = 8000;

    private static XINPUT_GAMEPAD_BUTTONS _prevButtons; // for edge detection

    public static InputMode DetectMode()
    {
        // Check 4 controllers
        for (int i = 0; i < 4; i++)
        {
            if (TryGetState(i, out _))
                return InputMode.Controller;
        }
        return InputMode.Keyboard;
    }

    public static ControllerType DetectControllerType()
    {
        try
        {
            uint deviceCount = 0;
            uint listStructSize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
            GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, listStructSize);
            if (deviceCount == 0) return ControllerType.Unknown;
            IntPtr pList = Marshal.AllocHGlobal((int)(deviceCount * listStructSize));
            try
            {
                if (GetRawInputDeviceList(pList, ref deviceCount, listStructSize) == 0xFFFFFFFF)
                    return ControllerType.Unknown;
                for (int i = 0; i < deviceCount; i++)
                {
                    var rid = Marshal.PtrToStructure<RAWINPUTDEVICELIST>(pList + i * (int)listStructSize);
                    if (rid.dwType != RIM_TYPEHID) continue;
                    uint nameSize = 0;
                    GetRawInputDeviceInfo(rid.hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref nameSize);
                    if (nameSize == 0) continue;
                    IntPtr pName = Marshal.AllocHGlobal((int)(nameSize * sizeof(char)));
                    try
                    {
                        if (GetRawInputDeviceInfo(rid.hDevice, RIDI_DEVICENAME, pName, ref nameSize) > 0)
                        {
                            string deviceName = Marshal.PtrToStringUni(pName) ?? string.Empty;
                            string upper = deviceName.ToUpperInvariant();
                            if (upper.Contains("VID_045E")) return ControllerType.Xbox;       // Microsoft
                            if (upper.Contains("VID_054C")) return ControllerType.PlayStation; // Sony
                            if (upper.Contains("VID_057E") || upper.Contains("NINTENDO")) return ControllerType.Switch; // Nintendo
                        }
                    }
                    finally { Marshal.FreeHGlobal(pName); }
                }
            }
            finally { Marshal.FreeHGlobal(pList); }
        }
        catch
        {
            // best-effort only
        }
        return ControllerType.Unknown;
    }

    public static InputAction ReadNextAction(InputMode mode)
    {
        return mode == InputMode.Controller ? ReadControllerAction() : ReadKeyboardAction();
    }

    private static InputAction ReadKeyboardAction()
    {
        var key = Console.ReadKey(true);
        return key.Key switch
        {
            ConsoleKey.W or ConsoleKey.UpArrow => InputAction.MoveUp,
            ConsoleKey.S or ConsoleKey.DownArrow => InputAction.MoveDown,
            ConsoleKey.A or ConsoleKey.LeftArrow => InputAction.MoveLeft,
            ConsoleKey.D or ConsoleKey.RightArrow => InputAction.MoveRight,
            ConsoleKey.Spacebar or ConsoleKey.E => InputAction.Interact,
            ConsoleKey.I => InputAction.Inventory,
            ConsoleKey.P => InputAction.Party,
            ConsoleKey.M => InputAction.Map,
            ConsoleKey.H or ConsoleKey.F1 => InputAction.Help,
            ConsoleKey.Escape => InputAction.Menu,
            ConsoleKey.R => InputAction.Search,
            ConsoleKey.L => InputAction.Log,
            _ => InputAction.None
        };
    }

    private static InputAction ReadControllerAction()
    {
        // Poll first detected controller, apply repeat gating and edge detection
        while (true)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!TryGetState(i, out var state)) continue;
                var buttons = (XINPUT_GAMEPAD_BUTTONS)state.Gamepad.wButtons;

                // Determine directional intent (DPad preferred, then left stick)
                InputAction dir = InputAction.None;
                if (Has(buttons, XINPUT_GAMEPAD_BUTTONS.DPadUp)) dir = InputAction.MoveUp;
                else if (Has(buttons, XINPUT_GAMEPAD_BUTTONS.DPadDown)) dir = InputAction.MoveDown;
                else if (Has(buttons, XINPUT_GAMEPAD_BUTTONS.DPadLeft)) dir = InputAction.MoveLeft;
                else if (Has(buttons, XINPUT_GAMEPAD_BUTTONS.DPadRight)) dir = InputAction.MoveRight;
                else
                {
                    if (state.Gamepad.sThumbLY > AxisDeadzone) dir = InputAction.MoveUp;
                    else if (state.Gamepad.sThumbLY < -AxisDeadzone) dir = InputAction.MoveDown;
                    else if (state.Gamepad.sThumbLX < -AxisDeadzone) dir = InputAction.MoveLeft;
                    else if (state.Gamepad.sThumbLX > AxisDeadzone) dir = InputAction.MoveRight;
                }

                long now = Environment.TickCount64;
                if (dir != InputAction.None)
                {
                    if (dir != _lastDir)
                    {
                        // New direction pressed: immediate move and reset repeat timers
                        _lastDir = dir;
                        _lastDirTimestamp = now;
                        _didInitialRepeat = false;
                        _prevButtons = buttons; // update previous buttons too
                        return dir;
                    }
                    else
                    {
                        int wait = _didInitialRepeat ? RepeatIntervalMs : InitialRepeatDelayMs;
                        if ((now - _lastDirTimestamp) >= wait)
                        {
                            _lastDirTimestamp = now;
                            _didInitialRepeat = true;
                            _prevButtons = buttons;
                            return dir;
                        }
                    }
                }
                else
                {
                    // No direction held: reset
                    _lastDir = InputAction.None;
                    _didInitialRepeat = false;
                }

                // Non-directional button actions: fire on rising edge only (no repeats)
                InputAction edgeAction = EdgeButtonToAction(_prevButtons, buttons);
                _prevButtons = buttons;
                if (edgeAction != InputAction.None) return edgeAction;
            }
            Thread.Sleep(16); // ~60Hz
        }
    }

    private static InputAction EdgeButtonToAction(XINPUT_GAMEPAD_BUTTONS prev, XINPUT_GAMEPAD_BUTTONS curr)
    {
        bool Rising(XINPUT_GAMEPAD_BUTTONS f) => !Has(prev, f) && Has(curr, f);
        if (Rising(XINPUT_GAMEPAD_BUTTONS.A)) return InputAction.Interact;   // Confirm
        if (Rising(XINPUT_GAMEPAD_BUTTONS.X)) return InputAction.Inventory;
        if (Rising(XINPUT_GAMEPAD_BUTTONS.Y)) return InputAction.Party;
        if (Rising(XINPUT_GAMEPAD_BUTTONS.Back)) return InputAction.Menu;     // View
        if (Rising(XINPUT_GAMEPAD_BUTTONS.Start)) return InputAction.Help;    // Start
        if (Rising(XINPUT_GAMEPAD_BUTTONS.B)) return InputAction.Search;      // Use as Cancel/Search
        if (Rising(XINPUT_GAMEPAD_BUTTONS.RightShoulder)) return InputAction.Log; // Toggle log
        return InputAction.None;
    }

    private static bool Has(XINPUT_GAMEPAD_BUTTONS value, XINPUT_GAMEPAD_BUTTONS flag)
        => (value & flag) == flag;
}
