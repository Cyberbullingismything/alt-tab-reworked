using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SmoothTabTransition
{
    public partial class SettingsWindow : Window
    {
        private string _selectedColor = "#6C5CE7";
        
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();

            SpeedSlider.ValueChanged += SpeedSlider_ValueChanged;
            ScaleSlider.ValueChanged += ScaleSlider_ValueChanged;
            MaxWindowsSlider.ValueChanged += MaxWindowsSlider_ValueChanged;
        }

        private void LoadSettings()
        {
            var settings = AppSettings.Instance;
            
            AnimationsCheckBox.IsChecked = settings.EnableAnimations;
            SpeedSlider.Value = settings.AnimationSpeed;
            ScaleSlider.Value = settings.ScaleAmount;
            MaxWindowsSlider.Value = settings.MaxWindows;
            _selectedColor = settings.AccentColor;
            
            UpdateSliderLabels();
            UpdateColorSelection();
        }
        
        private void UpdateSliderLabels()
        {
            SpeedValue.Text = $"{SpeedSlider.Value:F2}s";
            ScaleValue.Text = $"{(int)(ScaleSlider.Value * 100)}%";
            MaxWindowsValue.Text = $"{(int)MaxWindowsSlider.Value}";
        }
        
        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SpeedValue != null)
                SpeedValue.Text = $"{SpeedSlider.Value:F2}s";
        }
        
        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ScaleValue != null)
                ScaleValue.Text = $"{(int)(ScaleSlider.Value * 100)}%";
        }
        
        private void MaxWindowsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MaxWindowsValue != null)
                MaxWindowsValue.Text = $"{(int)MaxWindowsSlider.Value}";
        }
        
        private void UpdateColorSelection()
        {

            Color1.BorderBrush = Brushes.Transparent;
            Color2.BorderBrush = Brushes.Transparent;
            Color3.BorderBrush = Brushes.Transparent;
            Color4.BorderBrush = Brushes.Transparent;
            Color5.BorderBrush = Brushes.Transparent;
            Color6.BorderBrush = Brushes.Transparent;

            var whiteBrush = new SolidColorBrush(Colors.White);
            switch (_selectedColor.ToUpper())
            {
                case "#6C5CE7":
                    Color1.BorderBrush = whiteBrush;
                    break;
                case "#4A9EFF":
                    Color2.BorderBrush = whiteBrush;
                    break;
                case "#00D9A5":
                    Color3.BorderBrush = whiteBrush;
                    break;
                case "#FF6B6B":
                    Color4.BorderBrush = whiteBrush;
                    break;
                case "#FFB347":
                    Color5.BorderBrush = whiteBrush;
                    break;
                case "#FF69B4":
                    Color6.BorderBrush = whiteBrush;
                    break;
            }
        }
        
        private void ColorPreset_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string color)
            {
                _selectedColor = color;
                UpdateColorSelection();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {

            AnimationsCheckBox.IsChecked = true;
            SpeedSlider.Value = 0.2;
            ScaleSlider.Value = 1.08;
            MaxWindowsSlider.Value = 12;
            _selectedColor = "#6C5CE7";
            
            UpdateSliderLabels();
            UpdateColorSelection();
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            Close();
        }
        
        private void SaveSettings()
        {
            var settings = AppSettings.Instance;
            
            settings.EnableAnimations = AnimationsCheckBox.IsChecked ?? true;
            settings.AnimationSpeed = SpeedSlider.Value;
            settings.ScaleAmount = ScaleSlider.Value;
            settings.MaxWindows = (int)MaxWindowsSlider.Value;
            settings.AccentColor = _selectedColor;
        }
        
        protected override void OnClosed(EventArgs e)
        {
            SaveSettings();
            base.OnClosed(e);
        }
    }
}
