using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SmoothTabTransition.Services
{
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int VK_TAB = 0x09;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private bool _altPressed = false;
        private bool _switcherOpen = false;
        private bool _disposed = false;

        public event EventHandler? AltTabPressed;
        public event EventHandler? AltTabReleased;
        public event EventHandler? TabPressedWhileOpen;
        public event EventHandler? ShiftTabPressedWhileOpen;
        public event EventHandler? EscapePressed;

        public void SetSwitcherOpen(bool isOpen)
        {
            _switcherOpen = isOpen;
            if (!isOpen)
            {
                _altPressed = false;
            }
        }

        public bool IsSwitcherOpen => _switcherOpen;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public KeyboardHook()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
            
            if (_hookID == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to set keyboard hook. Error code: {errorCode}");
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule?.ModuleName), 0);
        }

        private bool IsAltPressed()
        {
            return (GetAsyncKeyState(VK_LMENU) & 0x8000) != 0 || 
                   (GetAsyncKeyState(VK_RMENU) & 0x8000) != 0;
        }

        private bool IsShiftPressed()
        {
            return (GetAsyncKeyState(0x10) & 0x8000) != 0;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vkCode = (int)hookStruct.vkCode;

                bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

                if (vkCode == VK_LMENU || vkCode == VK_RMENU)
                {
                    if (isKeyDown)
                    {
                        _altPressed = true;
                    }
                    else if (isKeyUp)
                    {
                        bool wasOpen = _switcherOpen;
                        _altPressed = false;

                        if (wasOpen)
                        {
                            _switcherOpen = false;
                            try
                            {
                                AltTabReleased?.Invoke(this, EventArgs.Empty);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error in AltTabReleased handler: {ex.Message}");
                            }
                        }
                    }

                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                if (vkCode == VK_TAB && isKeyDown)
                {

                    if (_altPressed || IsAltPressed())
                    {
                        _altPressed = true;

                        if (!_switcherOpen)
                        {

                            _switcherOpen = true;
                            try
                            {
                                AltTabPressed?.Invoke(this, EventArgs.Empty);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error in AltTabPressed handler: {ex.Message}");
                            }
                        }
                        else
                        {

                            try
                            {
                                if (IsShiftPressed())
                                {
                                    ShiftTabPressedWhileOpen?.Invoke(this, EventArgs.Empty);
                                }
                                else
                                {
                                    TabPressedWhileOpen?.Invoke(this, EventArgs.Empty);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error in Tab handler: {ex.Message}");
                            }
                        }

                        return (IntPtr)1;
                    }
                }

                if (vkCode == VK_ESCAPE && isKeyDown && _switcherOpen)
                {
                    _switcherOpen = false;
                    _altPressed = false;
                    try
                    {
                        EscapePressed?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in EscapePressed handler: {ex.Message}");
                    }

                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void ResetState()
        {
            _altPressed = false;
            _switcherOpen = false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                ResetState();
                if (_hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                }
            }
        }
    }
}
