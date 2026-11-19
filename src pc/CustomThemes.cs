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
                int count = AppSettings.GetInt("custom.count", 0);
                for (int i = 0; i < count; i++)
                {
                    string name = AppSettings.Get("custom." + i + ".name", "").Trim();
                    if (name.Length == 0) continue;
                    int argb;
                    if (!int.TryParse(AppSettings.Get("custom." + i + ".accent", ""),
                        NumberStyles.HexNumber, CultureInfo.InvariantCulture, out argb))
                    {
                        argb = Color.FromArgb(122, 167, 255).ToArgb();
                    }
                    list.Add(new CustomTheme
                    {
                        Name = name,
                        Dark = AppSettings.Get("custom." + i + ".dark", "1") == "1",
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
            AppSettings.RemovePrefix("custom.");
            string[] pairs = new string[2 + list.Count * 6];
            int index = 0;
            pairs[index++] = "custom.count";
            pairs[index++] = list.Count.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < list.Count; i++)
            {
                CustomTheme theme = list[i];
                pairs[index++] = "custom." + i + ".name";
                pairs[index++] = theme.Name;
                pairs[index++] = "custom." + i + ".dark";
                pairs[index++] = theme.Dark ? "1" : "0";
                pairs[index++] = "custom." + i + ".accent";
                pairs[index++] = theme.Accent.ToArgb().ToString("X8",
                    CultureInfo.InvariantCulture);
            }
            AppSettings.SetMany(pairs);
        }
    }
}
