using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using SmoothTabTransition.Services;
using SmoothTabTransition.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using DrawingColor = System.Drawing.Color;
using DrawingPen = System.Drawing.Pen;
using DrawingSolidBrush = System.Drawing.SolidBrush;

namespace SmoothTabTransition
{
    public partial class MainWindow : Window
    {
        private KeyboardHook? _keyboardHook;
        private SwitcherWindow? _switcherWindow;
        private NotifyIcon? _notifyIcon;
        private bool _isClosing = false;
        private CancellationTokenSource? _preloadCancellationToken;
        private Task? _preloadTask;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            InitializeHook();
            StartPreloadingIfEnabled();
        }
        
        public void OnPreloadSettingChanged()
        {
            StopPreloading();
            
            if (AppSettings.Instance.PreloadThumbnails)
            {
                StartPreloadingIfEnabled();
            }
        }
        
        private void StartPreloadingIfEnabled()
        {
            if (!AppSettings.Instance.PreloadThumbnails)
                return;
                
            StopPreloading();
            
            _preloadCancellationToken = new CancellationTokenSource();
            _preloadTask = Task.Run(() => PreloadThumbnails(_preloadCancellationToken.Token));
        }
        
        private void StopPreloading()
        {
            if (_preloadCancellationToken != null)
            {
                _preloadCancellationToken.Cancel();
                _preloadCancellationToken.Dispose();
                _preloadCancellationToken = null;
            }
            
            if (_preloadTask != null)
            {
                try
                {
                    _preloadTask.Wait(1000);
                }
                catch { }
                _preloadTask = null;
            }
        }
        
        private async Task PreloadThumbnails(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(2000, cancellationToken);
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var windows = WindowEnumerator.GetOpenWindows()
                            .Where(w => w.ProcessName != "SmoothTabTransition")
                            .Take(AppSettings.Instance.MaxWindows)
                            .ToList();
                        
                        if (windows.Count == 0)
                        {
                            await Task.Delay(5000, cancellationToken);
                            continue;
                        }
                        
                        var handles = windows.Select(w => w.Handle).ToArray();
                        await WindowThumbnailService.CaptureMultipleThumbnailsAsync(handles, 1400, 800);
                        
                        await Task.Delay(3000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in preload cycle: {ex}");
                        await Task.Delay(5000, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in PreloadThumbnails: {ex}");
            }
        }

        private void InitializeHook()
        {
            try
            {
                _keyboardHook = new KeyboardHook();
                _keyboardHook.AltTabPressed += OnAltTabPressed;
                _keyboardHook.AltTabReleased += OnAltTabReleased;
                _keyboardHook.TabPressedWhileOpen += OnTabPressedWhileOpen;
                _keyboardHook.ShiftTabPressedWhileOpen += OnShiftTabPressedWhileOpen;
                _keyboardHook.EscapePressed += OnEscapePressed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize keyboard hook: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnAltTabPressed(object? sender, EventArgs e)
        {
            try
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_switcherWindow == null || !_switcherWindow.IsVisible)
                        {
                            _switcherWindow = new SwitcherWindow();
                            _switcherWindow.Closed += (s, args) => 
                            { 
                                _switcherWindow = null;
                                _keyboardHook?.ResetState();
                            };
                            _switcherWindow.Show();
                            _switcherWindow.Activate();
                            _switcherWindow.Focus();
                            _keyboardHook?.SetSwitcherOpen(true);
                        }
                        else
                        {
                            _switcherWindow.Activate();
                            _switcherWindow.Focus();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in OnAltTabPressed dispatcher: {ex}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAltTabPressed: {ex}");
            }
        }

        private void OnTabPressedWhileOpen(object? sender, EventArgs e)
        {
            try
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_switcherWindow != null && _switcherWindow.IsVisible)
                        {
                            _switcherWindow.NavigateNext();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in OnTabPressedWhileOpen dispatcher: {ex}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnTabPressedWhileOpen: {ex}");
            }
        }

        private void OnShiftTabPressedWhileOpen(object? sender, EventArgs e)
        {
            try
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_switcherWindow != null && _switcherWindow.IsVisible)
                        {
                            _switcherWindow.NavigatePrevious();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in OnShiftTabPressedWhileOpen dispatcher: {ex}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnShiftTabPressedWhileOpen: {ex}");
            }
        }

        private void OnAltTabReleased(object? sender, EventArgs e)
        {
            try
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_switcherWindow != null && _switcherWindow.IsVisible)
                        {
                            _switcherWindow.SwitchToSelectedWindow();
                            _switcherWindow.Close();
                            _switcherWindow = null;
                        }
                        _keyboardHook?.ResetState();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in OnAltTabReleased dispatcher: {ex}");
                        _keyboardHook?.ResetState();
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAltTabReleased: {ex}");
            }
        }

        private void OnEscapePressed(object? sender, EventArgs e)
        {
            try
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_switcherWindow != null && _switcherWindow.IsVisible)
                        {
                            _switcherWindow.CancelAndClose();
                            _switcherWindow = null;
                        }
                        _keyboardHook?.ResetState();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in OnEscapePressed dispatcher: {ex}");
                        _keyboardHook?.ResetState();
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnEscapePressed: {ex}");
            }
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            var icon = CreateDefaultIcon();
            _notifyIcon.Icon = icon;
            _notifyIcon.Text = "Smooth Tab Transition";
            _notifyIcon.Visible = true;

            var contextMenu = new ContextMenuStrip();
            
            var settingsMenuItem = new ToolStripMenuItem("Settings");
            settingsMenuItem.Click += (s, e) => OpenSettings();
            contextMenu.Items.Add(settingsMenuItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            var exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => OpenSettings();

            Application.Current.Exit += (s, e) => CleanupTrayIcon();
        }

        private SettingsWindow? _settingsWindow;
        
        private void OpenSettings()
        {
            if (_settingsWindow == null || !_settingsWindow.IsVisible)
            {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
                _settingsWindow.Show();
            }
            else
            {
                _settingsWindow.Activate();
            }
        }

        private System.Drawing.Icon CreateDefaultIcon()
        {
            try
            {
                using (var bitmap = new Bitmap(16, 16))
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.Clear(DrawingColor.FromArgb(74, 158, 255));
                        g.FillEllipse(new DrawingSolidBrush(DrawingColor.White), 3, 3, 10, 10);
                        g.DrawEllipse(new DrawingPen(DrawingColor.White, 1), 3, 3, 10, 10);
                    }
                    IntPtr hIcon = bitmap.GetHicon();
                    return System.Drawing.Icon.FromHandle(hIcon);
                }
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private void CleanupTrayIcon()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }

        private void ExitApplication()
        {
            _isClosing = true;
            StopPreloading();
            CleanupTrayIcon();
            _keyboardHook?.Dispose();
            _switcherWindow?.Close();
            Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosing)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            StopPreloading();
            CleanupTrayIcon();
            _keyboardHook?.Dispose();
            _switcherWindow?.Close();
            base.OnClosed(e);
        }
    }
}
