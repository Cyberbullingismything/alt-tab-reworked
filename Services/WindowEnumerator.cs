using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace SmoothTabTransition.Services
{
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public bool IsVisible { get; set; }
        public Rect Bounds { get; set; }
        public ImageSource? Thumbnail { get; set; }
    }

    public class WindowEnumerator
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetLastActivePopup(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        private const uint GW_OWNER = 4;
        private const uint GW_HWNDNEXT = 2;
        private const uint GA_ROOTOWNER = 3;
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_MINIMIZE = 0x20000000;
        private const int DWMWA_CLOAKED = 14;

        private static readonly HashSet<string> SystemProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "SmoothTabTransition",
            "TextInputHost",
            "ShellExperienceHost",
            "SearchHost", 
            "SearchApp",
            "StartMenuExperienceHost",
            "LockApp",
            "SystemSettings",
            "ShellHost",
            "ApplicationFrameHost",
            "Video.UI",
            "PeopleExperienceHost",
            "MicrosoftEdgeUpdate",
            "CredentialUIBroker"
        };

        private static readonly HashSet<string> ExcludedTitles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Program Manager",
            "Windows Input Experience",
            "Microsoft Text Input Application",
            "PopupHost",
            "Setup"
        };

        public static IntPtr GetActiveWindow()
        {
            return GetForegroundWindow();
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }



        private static bool IsAltTabWindow(IntPtr hWnd)
        {

            if (!IsWindowVisible(hWnd))
                return false;

            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0)
            {
                if (cloaked != 0)
                    return false;
            }

            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return false;

            if ((exStyle & WS_EX_NOACTIVATE) != 0)
                return false;

            IntPtr rootOwner = GetAncestor(hWnd, GA_ROOTOWNER);

            if (rootOwner == hWnd)
            {

                if ((exStyle & WS_EX_APPWINDOW) != 0)
                    return true;

                return true;
            }

            IntPtr lastPopup = GetLastActivePopup(rootOwner);
            if (lastPopup == hWnd)
            {

                if ((exStyle & WS_EX_APPWINDOW) != 0)
                    return true;

                if (IsWindowVisible(rootOwner))
                    return true;
            }

            if ((exStyle & WS_EX_APPWINDOW) != 0)
                return true;

            return false;
        }

        public static List<WindowInfo> GetOpenWindows()
        {
            var windows = new List<WindowInfo>();
            IntPtr shellWindow = GetShellWindow();
            IntPtr desktopWindow = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                try
                {

                    if (hWnd == shellWindow || hWnd == desktopWindow)
                        return true;

                    if (!IsAltTabWindow(hWnd))
                        return true;

                    int length = GetWindowTextLength(hWnd);
                    if (length == 0)
                        return true;

                    var titleBuilder = new StringBuilder(length + 1);
                    GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                    string title = titleBuilder.ToString();

                    if (string.IsNullOrWhiteSpace(title))
                        return true;

                    if (ExcludedTitles.Contains(title))
                        return true;

                    GetWindowThreadProcessId(hWnd, out uint processId);
                    string processName = string.Empty;
                    
                    try
                    {
                        using var process = Process.GetProcessById((int)processId);
                        processName = process.ProcessName;
                    }
                    catch { }

                    if (SystemProcesses.Contains(processName))
                        return true;

                    if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                    {

                        if (title.StartsWith("Task") || 
                            title == "Desktop" ||
                            string.IsNullOrEmpty(title))
                            return true;
                    }

                    GetWindowRect(hWnd, out RECT rect);
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    bool isMinimized = IsIconic(hWnd);
                    if (!isMinimized && (width < 50 || height < 50))
                        return true;

                    if (!isMinimized && rect.Left < -10000)
                        return true;

                    var bounds = new Rect(rect.Left, rect.Top, width, height);

                    windows.Add(new WindowInfo
                    {
                        Handle = hWnd,
                        Title = title,
                        ProcessName = processName,
                        IsVisible = true,
                        Bounds = bounds
                    });
                }
                catch
                {

                }
                
                return true;
            }, IntPtr.Zero);

            return SortByZOrder(windows);
        }

        private static List<WindowInfo> SortByZOrder(List<WindowInfo> windows)
        {
            if (windows.Count <= 1)
                return windows;

            var windowDict = windows.ToDictionary(w => w.Handle, w => w);
            var zOrderedWindows = new List<WindowInfo>();

            IntPtr current = GetTopWindow(IntPtr.Zero);
            while (current != IntPtr.Zero && windowDict.Count > 0)
            {
                if (windowDict.TryGetValue(current, out var windowInfo))
                {
                    zOrderedWindows.Add(windowInfo);
                    windowDict.Remove(current);
                }
                current = GetWindow(current, GW_HWNDNEXT);
            }

            zOrderedWindows.AddRange(windowDict.Values);

            IntPtr foregroundWindow = GetForegroundWindow();
            var foreground = zOrderedWindows.FirstOrDefault(w => w.Handle == foregroundWindow);
            if (foreground != null)
            {
                zOrderedWindows.Remove(foreground);
                zOrderedWindows.Insert(0, foreground);
            }
            
            return zOrderedWindows;
        }
    }
}
