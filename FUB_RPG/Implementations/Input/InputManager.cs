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

    // NEW: Wait for the first input source and return the chosen mode.
    public static InputMode WaitForFirstInput(out ControllerType controllerType)
    {
        controllerType = ControllerType.Unknown;
        while (true)
        {
            // Keyboard first: if any key available, consume and return
            if (Console.KeyAvailable)
            {
                _ = Console.ReadKey(true);
                controllerType = ControllerType.Unknown;
                return InputMode.Keyboard;
            }

            // Controller: scan connected gamepads and check for any active input
            for (int i = 0; i < 4; i++)
            {
                if (!TryGetState(i, out var state)) continue;
                if (IsControllerInputActive(state))
                {
                    controllerType = DetectControllerType();
                    return InputMode.Controller;
                }
            }

            Thread.Sleep(10);
        }
    }

    private static bool IsControllerInputActive(XINPUT_STATE state)
    {
        var buttons = (XINPUT_GAMEPAD_BUTTONS)state.Gamepad.wButtons;
        if (buttons != 0) return true;
        if (state.Gamepad.bLeftTrigger > 0 || state.Gamepad.bRightTrigger > 0) return true;
        if (Math.Abs(state.Gamepad.sThumbLX) > AxisDeadzone) return true;
        if (Math.Abs(state.Gamepad.sThumbLY) > AxisDeadzone) return true;
        if (Math.Abs(state.Gamepad.sThumbRX) > AxisDeadzone) return true;
        if (Math.Abs(state.Gamepad.sThumbRY) > AxisDeadzone) return true;
        return false;
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

                // Edge-detect confirm/cancel/etc.
                if (Edge(buttons, XINPUT_GAMEPAD_BUTTONS.A)) return InputAction.Interact;
                if (Edge(buttons, XINPUT_GAMEPAD_BUTTONS.B)) return InputAction.Menu;   // treat as cancel
                if (Edge(buttons, XINPUT_GAMEPAD_BUTTONS.X)) return InputAction.Inventory;
                if (Edge(buttons, XINPUT_GAMEPAD_BUTTONS.Y)) return InputAction.Party;
                if (Edge(buttons, XINPUT_GAMEPAD_BUTTONS.LeftShoulder)) return InputAction.Help;
                if (Edge(buttons, XINPUT_GAMEPAD_BUTTONS.RightShoulder)) return InputAction.Log;

                _prevButtons = buttons;
            }

            Thread.Sleep(16);
        }
    }

    private static bool Has(XINPUT_GAMEPAD_BUTTONS buttons, XINPUT_GAMEPAD_BUTTONS flag)
        => (buttons & flag) == flag;

    private static bool Edge(XINPUT_GAMEPAD_BUTTONS buttons, XINPUT_GAMEPAD_BUTTONS flag)
        => Has(buttons, flag) && !Has(_prevButtons, flag);
}
