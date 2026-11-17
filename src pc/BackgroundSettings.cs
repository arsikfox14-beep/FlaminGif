using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace Video2Gif
{
    /// <summary>Свой фон приложения: цвет или картинка/GIF.</summary>
    internal static class BackgroundSettings
    {
        public static Color? FillColor;
        public static string ImagePath = "";
        public static double SurfaceOpacity = 1.0;

        public static event Action Changed;

        private static bool loaded;

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Video2Gif", "background.txt");
            }
        }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                string[] parts = File.ReadAllText(FilePath).Trim().Split('\t');
                if (parts.Length < 2) return;
                if (parts[0] == "image")
                {
                    if (File.Exists(parts[1])) ImagePath = parts[1];
                    ReadOpacity(parts, 2);
                }
                else if (parts[0] == "color")
                {
                    int argb;
                    if (int.TryParse(parts[1], NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out argb))
                    {
                        FillColor = Color.FromArgb(argb);
                    }
                    ReadOpacity(parts, 2);
                }
                else if (parts[0] == "opacity")
                {
                    ReadOpacity(parts, 1);
                }
                Ui.SurfaceAlpha = SurfaceOpacity;
            }
            catch
            {
            }
        }

        private static void ReadOpacity(string[] parts, int index)
        {
            if (parts.Length <= index) return;
            double value;
            if (double.TryParse(parts[index], NumberStyles.Float,
                CultureInfo.InvariantCulture, out value))
            {
                SurfaceOpacity = Clamp(value);
            }
        }

        public static void SetSurfaceOpacity(double value)
        {
            SurfaceOpacity = Clamp(value);
            Ui.SurfaceAlpha = SurfaceOpacity;
            Flush();
            Raise();
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
            try
            {
                string path = FilePath;
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string opacity = SurfaceOpacity.ToString("0.###", CultureInfo.InvariantCulture);
                string line;
                if (!string.IsNullOrEmpty(ImagePath))
                {
                    line = "image\t" + ImagePath + "\t" + opacity;
                }
                else if (FillColor.HasValue)
                {
                    line = "color\t" + FillColor.Value.ToArgb().ToString("X8",
                        CultureInfo.InvariantCulture) + "\t" + opacity;
                }
                else
                {
                    line = "opacity\t" + opacity;
                }
                File.WriteAllText(path, line);
            }
            catch
            {
            }
        }

        private static void Raise()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }
    }
}
