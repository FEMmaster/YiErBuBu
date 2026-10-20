using System;
using System.Runtime.InteropServices;
namespace YiErBuBu;
internal enum MouseAction { Move, LeftDown, LeftUp, RightDown, RightUp, MiddleDown, Wheel }
internal readonly record struct KeyEvent(int ScanCode, int VkCode, bool Extended, bool Down);
internal sealed class InputListener : IDisposable
{
    private const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_MBUTTONDOWN = 0x0207, WM_MOUSEWHEEL = 0x020A;
    private delegate nint HookProc(int nCode, nint wParam, nint lParam);
    private readonly HookProc _keyboardProc, _mouseProc;
    private nint _keyboard, _mouse;
    public event Action<KeyEvent>? KeyboardChanged;
    public event Action<MouseAction>? MouseChanged;
    public bool IsActive => _keyboard != 0 && _mouse != 0;
    public InputListener()
    {
        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;
        _keyboard = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(null), 0);
        _mouse = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(null), 0);
    }
    private nint KeyboardCallback(int code, nint msg, nint data)
    {
        if (code >= 0)
        {
            int m = unchecked((int)msg);
            if (m == WM_KEYDOWN || m == WM_SYSKEYDOWN || m == WM_KEYUP || m == WM_SYSKEYUP)
            {
                var input = Marshal.PtrToStructure<KbdData>(data);
                KeyboardChanged?.Invoke(new KeyEvent((int)input.scanCode, (int)input.vkCode,
                    (input.flags & 1) != 0, m == WM_KEYDOWN || m == WM_SYSKEYDOWN));
            }
        }
        return CallNextHookEx(_keyboard, code, msg, data);
    }
    private nint MouseCallback(int code, nint msg, nint data)
    {
        if (code >= 0)
        {
            MouseAction? action = (int)msg switch
            {
                WM_MOUSEMOVE => MouseAction.Move,
                WM_LBUTTONDOWN => MouseAction.LeftDown, WM_LBUTTONUP => MouseAction.LeftUp,
                WM_RBUTTONDOWN => MouseAction.RightDown, WM_RBUTTONUP => MouseAction.RightUp,
                WM_MBUTTONDOWN => MouseAction.MiddleDown, WM_MOUSEWHEEL => MouseAction.Wheel,
                _ => null
            };
            if (action.HasValue) MouseChanged?.Invoke(action.Value);
        }
        return CallNextHookEx(_mouse, code, msg, data);
    }
    public void Dispose()
    {
        if (_keyboard != 0) { UnhookWindowsHookEx(_keyboard); _keyboard = 0; }
        if (_mouse != 0) { UnhookWindowsHookEx(_mouse); _mouse = 0; }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KbdData { public uint vkCode, scanCode, flags, time; public nint dwExtraInfo; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, HookProc cb, nint module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint msg, nint data);
}
