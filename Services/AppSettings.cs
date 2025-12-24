using System;
using System.Windows.Media;

namespace SmoothTabTransition
{
    public class AppSettings
    {
        private static AppSettings? _instance;
        public static AppSettings Instance => _instance ??= new AppSettings();

        public string BackgroundColor { get; set; } = "#40000000";
        public string AccentColor { get; set; } = "#6C5CE7";
        public string CardBackgroundColor { get; set; } = "#E6181820";

        public bool EnableAnimations { get; set; } = true;
        public double AnimationSpeed { get; set; } = 0.2;
        public double ScaleAmount { get; set; } = 1.08;

        public int MaxWindows { get; set; } = 20;

        public bool PreloadThumbnails { get; set; } = true;

        public Color GetBackgroundColorValue()
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(BackgroundColor);
            }
            catch
            {
                return Color.FromArgb(230, 0, 0, 0);
            }
        }

        public Color GetAccentColorValue()
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(AccentColor);
            }
            catch
            {
                return Color.FromRgb(108, 92, 231);
            }
        }

        public Color GetCardBackgroundValue()
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(CardBackgroundColor);
            }
            catch
            {
                return Color.FromArgb(230, 24, 24, 32);
            }
        }
        
        public Color GetAccentGlowColor()
        {
            var accent = GetAccentColorValue();

            return Color.FromArgb(
                180,
                (byte)Math.Min(255, accent.R + 30),
                (byte)Math.Min(255, accent.G + 30),
                (byte)Math.Min(255, accent.B + 30)
            );
        }
    }
}
