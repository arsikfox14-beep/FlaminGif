using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace Video2Gif
{
    internal sealed class CustomTheme
    {
        public string Name = "";
        public bool Dark = true;
        public Color Accent = Color.FromArgb(122, 167, 255);
    }

    internal static class CustomThemes
    {
        private static List<CustomTheme> list;

        public static IList<CustomTheme> All
        {
            get
            {
                Load();
                return list;
            }
        }

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Video2Gif", "custom-themes.txt");
            }
        }

        public static CustomTheme Find(string name)
        {
            Load();
            foreach (CustomTheme theme in list)
            {
                if (string.Equals(theme.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return theme;
                }
            }
            return null;
        }

        public static void Save(CustomTheme theme)
        {
            Load();
            CustomTheme existing = Find(theme.Name);
            if (existing != null)
            {
                existing.Dark = theme.Dark;
                existing.Accent = theme.Accent;
            }
            else
            {
                list.Add(theme);
            }
            Flush();
        }

        public static void Remove(string name)
        {
            Load();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (string.Equals(list[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    list.RemoveAt(i);
                }
            }
            Flush();
        }

        private static void Load()
        {
            if (list != null) return;
            list = new List<CustomTheme>();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length < 3) continue;
                    string name = parts[0].Trim();
                    if (name.Length == 0) continue;
                    int argb;
                    if (!int.TryParse(parts[2], NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out argb)) continue;
                    list.Add(new CustomTheme
                    {
                        Name = name,
                        Dark = parts[1] == "1",
                        Accent = Color.FromArgb(argb)
                    });
                }
            }
            catch
            {
            }
        }

        private static void Flush()
        {
            try
            {
                string path = FilePath;
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var lines = new List<string>();
                foreach (CustomTheme theme in list)
                {
                    lines.Add(theme.Name + "\t" + (theme.Dark ? "1" : "0") + "\t" +
                        theme.Accent.ToArgb().ToString("X8", CultureInfo.InvariantCulture));
                }
                File.WriteAllLines(path, lines.ToArray());
            }
            catch
            {
            }
        }
    }
}
