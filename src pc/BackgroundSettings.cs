using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace FlaminGif
{
    /// <summary>Свой фон приложения: цвет или картинка/GIF.</summary>
    internal static class BackgroundSettings
    {
        public static Color? FillColor;
        public static string ImagePath = "";
        public static double SurfaceOpacity = 1.0;
        public static double BackgroundOpacity = 1.0;

        public static event Action Changed;

        private static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                string type = AppSettings.Get("background.type", "");
                if (type == "image")
                {
                    string path = AppSettings.Get("background.path", "");
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        ImagePath = path;
                }
                else if (type == "color")
                {
                    int argb;
                    if (int.TryParse(AppSettings.Get("background.color", ""),
                        NumberStyles.HexNumber, CultureInfo.InvariantCulture, out argb))
                    {
                        FillColor = Color.FromArgb(argb);
                    }
                }
                SurfaceOpacity = AppSettings.GetDouble(
                    "background.card", 1.0, 0.3, 1.0);
                BackgroundOpacity = AppSettings.GetDouble(
                    "background.opacity", 1.0, 0.0, 1.0);
                Ui.SurfaceAlpha = SurfaceOpacity;
                Ui.BackgroundOpacity = BackgroundOpacity;
            }
            catch
            {
            }
        }

        public static void SetSurfaceOpacity(double value)
        {
            SurfaceOpacity = Clamp(value);
            Ui.SurfaceAlpha = SurfaceOpacity;
            Flush();
            Raise();
        }

        public static void SetBackgroundOpacity(double value)
        {
            BackgroundOpacity = ClampAlpha(value);
            Ui.BackgroundOpacity = BackgroundOpacity;
            Flush();
            Raise();
            
        }

        private static double ClampAlpha(double value)
        {
            if (value < 0.0) return 0.0;
            if (value > 1.0) return 1.0;
            return value;
        }

        private static double Clamp(double value)
        {
            if (value < 0.3) return 0.3;
            if (value > 1.0) return 1.0;
            return value;
        }

        public static void SetColor(Color color)
        {
            FillColor = color;
            ImagePath = "";
            Flush();
            Raise();
        }

        public static void SetImage(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            ImagePath = path;
            FillColor = null;
            Flush();
            Raise();
        }

        public static void Clear()
        {
            FillColor = null;
            ImagePath = "";
            Flush();
            Raise();
        }

        private static void Flush()
        {
            string type;
            string path = "";
            string color = "";
            if (!string.IsNullOrEmpty(ImagePath))
            {
                type = "image";
                path = ImagePath;
            }
            else if (FillColor.HasValue)
            {
                type = "color";
                color = FillColor.Value.ToArgb().ToString("X8", CultureInfo.InvariantCulture);
            }
            else
            {
                type = "none";
            }
            string opacity = SurfaceOpacity.ToString("0.###", CultureInfo.InvariantCulture);
            string background = BackgroundOpacity.ToString("0.###", CultureInfo.InvariantCulture);

            AppSettings.SetMany(
                "background.type", type,
                "background.path", path,
                "background.color", color,
                "background.card", opacity,
                "background.opacity", background);
        }

        private static void Raise()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }
    }
}
