using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using SmoothTabTransition.Services;

namespace SmoothTabTransition.Windows
{
    public partial class SwitcherWindow : Window
    {
        private List<WindowInfo> _windows = new();
        private int _selectedIndex = 0;
        private bool _windowSwitched = false;
        private bool _cancelled = false;
        private IntPtr _originalActiveWindow = IntPtr.Zero;
        private bool _isClosing = false;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_SHOWNA = 8;
        private const int SW_SHOWNORMAL = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }

        private AppSettings _settings = AppSettings.Instance;

        public SwitcherWindow()
        {
            try
            {
                InitializeComponent();
                _settings = AppSettings.Instance;
                ApplySettings();
                _originalActiveWindow = WindowEnumerator.GetActiveWindow();

                _selectedIndex = 1;
                
                LoadWindows();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SwitcherWindow constructor: {ex}");
            }
        }

        private void ApplySettings()
        {
            try
            {

                Background = new SolidColorBrush(_settings.GetBackgroundColorValue());

                var accentColor = _settings.GetAccentColorValue();
                var glowColor = _settings.GetAccentGlowColor();
                
                Resources["AccentBrush"] = new SolidColorBrush(accentColor);
                Resources["AccentGlowBrush"] = new SolidColorBrush(glowColor);
                Resources["CardBackgroundBrush"] = new SolidColorBrush(_settings.GetCardBackgroundValue());

                if (Resources["SelectedGlow"] is DropShadowEffect glowEffect)
                {
                    glowEffect.Color = accentColor;
                }
                if (Resources["CardShadowHover"] is DropShadowEffect hoverEffect)
                {
                    hoverEffect.Color = accentColor;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ApplySettings: {ex}");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {

                Activate();
                Focus();

                MainGrid.Focusable = true;
                MainGrid.Focus();
                Keyboard.Focus(MainGrid);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_settings.EnableAnimations)
                        {
                            var fadeIn = FindResource("FadeInAnimation") as Storyboard;
                            if (fadeIn != null)
                            {
                                fadeIn.Begin();
                            }
                            else
                            {
                                MainGrid.Opacity = 1.0;
                            }
                        }
                        else
                        {
                            MainGrid.Opacity = 1.0;
                            if (HintText != null) HintText.Opacity = 1.0;
                        }

                        Activate();
                        Focus();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in animation: {ex}");
                        MainGrid.Opacity = 1.0;
                    }
                }), DispatcherPriority.Loaded);

                Dispatcher.BeginInvoke(new Action(() => 
                {
                    try
                    {
                        UpdateSelection();
                        Focus();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in UpdateSelection: {ex}");
                    }
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in Window_Loaded: {ex}");
            }
        }

        private async void LoadWindows()
        {
            try
            {
                int maxWindows = _settings.MaxWindows;

                var windows = await Task.Run(() =>
                {
                    try
                    {
                        return WindowEnumerator.GetOpenWindows()
                            .Where(w => w.ProcessName != "SmoothTabTransition")
                            .Take(maxWindows)
                            .ToList();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting windows: {ex}");
                        return new List<WindowInfo>();
                    }
                });

                _windows = windows;

                if (_windows.Count > 1)
                {
                    _selectedIndex = 1;
                }
                else if (_windows.Count >= 1)
                {
                    _selectedIndex = 0;
                }

                WindowsContainer.ItemsSource = _windows;

                SetGridColumns(_windows.Count);

                _ = LoadThumbnailsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadWindows: {ex}");
            }
        }

        private async Task LoadThumbnailsAsync()
        {
            try
            {
                if (_windows.Count == 0) return;

                var handles = _windows.Select(w => w.Handle).ToArray();

                var thumbnails = await Task.Run(() => 
                    WindowThumbnailService.CaptureMultipleThumbnailsAsync(handles, 560, 320));

                if (_isClosing) return;

                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (_isClosing) return;

                        for (int i = 0; i < _windows.Count && i < thumbnails.Length; i++)
                        {
                            _windows[i].Thumbnail = thumbnails[i];
                        }

                        var currentWindows = _windows;
                        WindowsContainer.ItemsSource = null;
                        WindowsContainer.ItemsSource = currentWindows;

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateSelection();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating thumbnails UI: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadThumbnailsAsync: {ex}");
            }
        }

        private void SetGridColumns(int windowCount)
        {
            try
            {

                int columns;
                if (windowCount <= 3) columns = windowCount;
                else if (windowCount <= 6) columns = 3;
                else if (windowCount <= 12) columns = 4;
                else if (windowCount <= 20) columns = 5;
                else columns = 6;

                var itemsPanel = WindowsContainer.ItemsPanel;
                if (itemsPanel != null)
                {

                    var template = new ItemsPanelTemplate();
                    var factory = new FrameworkElementFactory(typeof(UniformGrid));
                    factory.SetValue(UniformGrid.ColumnsProperty, columns);
                    factory.SetValue(UniformGrid.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    template.VisualTree = factory;
                    WindowsContainer.ItemsPanel = template;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SetGridColumns: {ex}");
            }
        }

        public void NavigateNext()
        {
            try
            {
                if (_windows.Count > 0)
                {
                    _selectedIndex = (_selectedIndex + 1) % _windows.Count;
                    UpdateSelection();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NavigateNext: {ex}");
            }
        }

        public void NavigatePrevious()
        {
            try
            {
                if (_windows.Count > 0)
                {
                    _selectedIndex = (_selectedIndex - 1 + _windows.Count) % _windows.Count;
                    UpdateSelection();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NavigatePrevious: {ex}");
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {

                if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    e.Handled = true;
                    NavigateNext();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in Window_PreviewKeyDown: {ex}");
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    e.Handled = true;
                    if (_windows.Count > 0)
                    {
                        _selectedIndex = (_selectedIndex + 1) % _windows.Count;
                        UpdateSelection();
                    }
                }
                else if (e.Key == Key.Left || e.Key == Key.Up)
                {
                    e.Handled = true;
                    if (_windows.Count > 0)
                    {
                        _selectedIndex = (_selectedIndex - 1 + _windows.Count) % _windows.Count;
                        UpdateSelection();
                    }
                }
                else if (e.Key == Key.Right || e.Key == Key.Down)
                {
                    e.Handled = true;
                    if (_windows.Count > 0)
                    {
                        _selectedIndex = (_selectedIndex + 1) % _windows.Count;
                        UpdateSelection();
                    }
                }
                else if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    if (_windows.Count > 0 && _selectedIndex < _windows.Count)
                    {
                        _windowSwitched = true;
                        SwitchToWindow(_windows[_selectedIndex]);
                        CloseWindow();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in Window_KeyDown: {ex}");
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {

        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {

                if (e.OriginalSource == MainGrid || e.OriginalSource == this)
                {
                    if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
                    {
                        CloseWindow();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in Window_MouseDown: {ex}");
            }
        }

        private void WindowItem_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                if (sender is Border border)
                {
                    var index = GetBorderIndex(border);

                    AnimateBorder(border, _settings.ScaleAmount, -8);

                    var accentColor = _settings.GetAccentColorValue();
                    border.Effect = new DropShadowEffect
                    {
                        BlurRadius = 35,
                        ShadowDepth = 10,
                        Direction = 270,
                        Color = accentColor,
                        Opacity = 0.5
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in WindowItem_MouseEnter: {ex}");
            }
        }

        private void WindowItem_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                if (sender is Border border)
                {
                    var index = GetBorderIndex(border);
                    
                    if (index != _selectedIndex)
                    {

                        AnimateBorder(border, 1.0, 0);
                        border.Effect = new DropShadowEffect
                        {
                            BlurRadius = 25,
                            ShadowDepth = 8,
                            Direction = 270,
                            Color = Colors.Black,
                            Opacity = 0.5
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in WindowItem_MouseLeave: {ex}");
            }
        }

        private int GetBorderIndex(Border border)
        {
            try
            {
                for (int i = 0; i < WindowsContainer.Items.Count; i++)
                {
                    var container = WindowsContainer.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                    if (container != null)
                    {
                        var itemBorder = FindVisualChild<Border>(container);
                        if (itemBorder == border) return i;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetBorderIndex: {ex}");
            }
            return -1;
        }

        private void AnimateBorder(Border border, double targetScale, double targetY)
        {
            try
            {
                var duration = TimeSpan.FromSeconds(_settings.AnimationSpeed);
                var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };

                if (!_settings.EnableAnimations)
                {
                    duration = TimeSpan.Zero;
                }

                TransformGroup? transformGroup = border.RenderTransform as TransformGroup;
                ScaleTransform? scaleTransform = null;
                TranslateTransform? translateTransform = null;

                if (transformGroup != null && transformGroup.Children.Count >= 2)
                {
                    scaleTransform = transformGroup.Children[0] as ScaleTransform;
                    translateTransform = transformGroup.Children[1] as TranslateTransform;
                }
                else
                {

                    scaleTransform = border.RenderTransform as ScaleTransform;
                }

                if (scaleTransform != null)
                {
                    var scaleXAnim = new DoubleAnimation(targetScale, duration) { EasingFunction = ease };
                    var scaleYAnim = new DoubleAnimation(targetScale, duration) { EasingFunction = ease };
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
                }

                if (translateTransform != null)
                {
                    var translateYAnim = new DoubleAnimation(targetY, duration) { EasingFunction = ease };
                    translateTransform.BeginAnimation(TranslateTransform.YProperty, translateYAnim);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AnimateBorder: {ex}");
            }
        }

        private void WindowItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Border border && border.DataContext is WindowInfo windowInfo)
                {
                    e.Handled = true;
                    _windowSwitched = true;
                    _selectedIndex = _windows.IndexOf(windowInfo);
                    SwitchToWindow(windowInfo);
                    CloseWindow();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in WindowItem_MouseLeftButtonUp: {ex}");
            }
        }

        private void UpdateSingleItemSelection(int index, bool isSelected)
        {
            try
            {
                var container = WindowsContainer.ItemContainerGenerator.ContainerFromIndex(index) as ContentPresenter;
                if (container == null) return;
                
                var border = FindVisualChild<Border>(container);
                if (border == null) return;

                var accentColor = _settings.GetAccentColorValue();
                
                if (isSelected)
                {
                    border.BorderBrush = new SolidColorBrush(accentColor);
                    border.BorderThickness = new Thickness(3);
                    AnimateBorder(border, _settings.ScaleAmount, -8);

                    border.Effect = new DropShadowEffect
                    {
                        BlurRadius = 45,
                        ShadowDepth = 0,
                        Direction = 0,
                        Color = accentColor,
                        Opacity = 0.6
                    };
                }
                else
                {
                    border.BorderBrush = Brushes.Transparent;
                    border.BorderThickness = new Thickness(3);
                    AnimateBorder(border, 1.0, 0);

                    border.Effect = new DropShadowEffect
                    {
                        BlurRadius = 25,
                        ShadowDepth = 8,
                        Direction = 270,
                        Color = Colors.Black,
                        Opacity = 0.5
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateSingleItemSelection: {ex}");
            }
        }

        private void UpdateSelection()
        {
            try
            {
                if (_windows.Count == 0) return;

                for (int i = 0; i < WindowsContainer.Items.Count; i++)
                {
                    UpdateSingleItemSelection(i, i == _selectedIndex);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateSelection: {ex}");
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            try
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is T result)
                        return result;
                    var childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindVisualChild: {ex}");
            }
            return null;
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private IntPtr _pendingWindowHandle = IntPtr.Zero;
        private bool _pendingMinimized = false;

        private void SwitchToWindow(WindowInfo windowInfo)
        {
            try
            {

                _pendingWindowHandle = windowInfo.Handle;
                _pendingMinimized = IsIconic(windowInfo.Handle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SwitchToWindow: {ex}");
            }
        }

        private void ActivatePendingWindow()
        {
            if (_pendingWindowHandle == IntPtr.Zero)
                return;

            var handle = _pendingWindowHandle;
            var isMinimized = _pendingMinimized;
            _pendingWindowHandle = IntPtr.Zero;

            try
            {

                
                if (isMinimized)
                {

                    ShowWindow(handle, SW_RESTORE);
                }
                else
                {

                    ShowWindow(handle, SW_SHOW);
                }

                BringWindowToTop(handle);
                SetForegroundWindow(handle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to switch window: {ex.Message}");
            }
        }

        private void CloseWindow()
        {
            try
            {
                _isClosing = true;
                
                if (_settings.EnableAnimations)
                {
                    var fadeOut = FindResource("FadeOutAnimation") as Storyboard;
                    if (fadeOut != null)
                    {
                        fadeOut.Completed += (s, e) => 
                        {
                            try { Close(); } catch { }
                        };
                        fadeOut.Begin();
                        return;
                    }
                }
                
                Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CloseWindow: {ex}");
                try { Close(); } catch { }
            }
        }

        public void SwitchToSelectedWindow()
        {
            try
            {
                if (_selectedIndex >= 0 && _selectedIndex < _windows.Count && _windows.Count > 0)
                {
                    _windowSwitched = true;
                    SwitchToWindow(_windows[_selectedIndex]);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SwitchToSelectedWindow: {ex}");
            }
        }

        public void CancelAndClose()
        {
            try
            {
                _cancelled = true;
                _windowSwitched = true;
                _pendingWindowHandle = IntPtr.Zero;
                _isClosing = true;
                Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CancelAndClose: {ex}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _isClosing = true;

                if (!_windowSwitched && !_cancelled && _selectedIndex >= 0 && _selectedIndex < _windows.Count && _windows.Count > 0)
                {
                    SwitchToWindow(_windows[_selectedIndex]);
                }

                ActivatePendingWindow();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnClosed: {ex}");
            }
            
            base.OnClosed(e);
        }
    }
}
