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
                }
                else if (parts[0] == "color")
                {
                    int argb;
                    if (int.TryParse(parts[1], NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out argb))
                    {
                        FillColor = Color.FromArgb(argb);
                    }
                }
            }
            catch
            {
            }
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
                string line = "";
                if (!string.IsNullOrEmpty(ImagePath))
                {
                    line = "image\t" + ImagePath;
                }
                else if (FillColor.HasValue)
                {
                    line = "color\t" + FillColor.Value.ToArgb().ToString("X8",
                        CultureInfo.InvariantCulture);
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
