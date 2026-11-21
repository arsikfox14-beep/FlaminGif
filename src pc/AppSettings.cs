using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FlaminGif
{
    /// <summary>
    /// Все настройки приложения в одном файле settings.txt.
    /// Старые отдельные файлы (theme.txt, background.txt и т.д.)
    /// импортируются при первом запуске и удаляются.
    /// </summary>
    internal static class AppSettings
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, string> Values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool loaded;

        public static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FlaminGif");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(Folder, "settings.txt"); }
        }

        public static void Load()
        {
            lock (Gate)
            {
                if (loaded) return;
                loaded = true;
                MigrateOldFolder();
                ReadFile();
                ImportLegacy();
            }
        }

        public static string Get(string key, string fallback)
        {
            lock (Gate)
            {
                Load();
                string value;
                if (key != null && Values.TryGetValue(key, out value)) return value;
                return fallback;
            }
        }

        public static int GetInt(string key, int fallback)
        {
            int value;
            if (!int.TryParse(Get(key, ""), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value)) return fallback;
            return value;
        }

        public static double GetDouble(string key, double fallback,
            double minimum, double maximum)
        {
            double value;
            if (!double.TryParse(Get(key, ""), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value)) return fallback;
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        public static void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (Gate)
            {
                Load();
                Values[key] = value ?? "";
                Save();
            }
        }

        /// <summary>Записывает несколько пар «ключ, значение» одним сохранением.</summary>
        public static void SetMany(params string[] pairs)
        {
            if (pairs == null || pairs.Length < 2) return;
            lock (Gate)
            {
                Load();
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    if (string.IsNullOrEmpty(pairs[i])) continue;
                    Values[pairs[i]] = pairs[i + 1] ?? "";
                }
                Save();
            }
        }

        public static void RemovePrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return;
            lock (Gate)
            {
                Load();
                List<string> remove = new List<string>();
                foreach (string key in Values.Keys)
                {
                    if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        remove.Add(key);
                }
                if (remove.Count == 0) return;
                foreach (string key in remove) Values.Remove(key);
                Save();
            }
        }

        /// <summary>Переносит данные из старой папки в FlaminGif.</summary>
        private static void MigrateOldFolder()
        {
            try
            {
                // Старое имя папки нужно только для переноса настроек.
                string oldFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Video2Gif");
                if (!Directory.Exists(oldFolder)) return;
                if (!Directory.Exists(Folder))
                {
                    Directory.Move(oldFolder, Folder);
                    return;
                }
                foreach (string file in Directory.GetFiles(oldFolder))
                {
                    string target = Path.Combine(Folder, Path.GetFileName(file));
                    if (File.Exists(target)) continue;
                    try { File.Move(file, target); }
                    catch
                    {
                        try { File.Copy(file, target, false); } catch { }
                    }
                }
                try
                {
                    if (Directory.GetFiles(oldFolder).Length == 0)
                        Directory.Delete(oldFolder);
                }
                catch { }
            }
            catch
            {
            }
        }

        private static void ReadFile()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    string key = line.Substring(0, split).Trim();
                    if (key.Length == 0) continue;
                    Values[key] = line.Substring(split + 1);
                }
            }
            catch
            {
            }
        }

        private static bool Save()
        {
            try
            {
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                List<string> keys = new List<string>(Values.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                StringBuilder text = new StringBuilder();
                text.AppendLine("# FlaminGif settings");
                foreach (string key in keys)
                {
                    string value = Values[key] ?? "";
                    value = value.Replace("\r", " ").Replace("\n", " ");
                    text.Append(key).Append('=').AppendLine(value);
                }
                File.WriteAllText(FilePath, text.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /* ------------------------------------------------ Старые файлы → один */

        private static void ImportLegacy()
        {
            bool changed = false;
            changed |= ImportSimple("theme.txt", "theme");
            changed |= ImportSimple("language.txt", "language");
            changed |= ImportSimple("snake-best.txt", "snake.best");
            changed |= ImportSimple("update.txt", "update.seen");
            changed |= ImportSimple("version.txt", "ffmpeg.version");
            changed |= ImportSimple("yt-dlp-version.txt", "ytdlp.version");

            if (!Values.ContainsKey("background.type"))
            {
                string path = Path.Combine(Folder, "background.txt");
                if (File.Exists(path))
                {
                    try
                    {
                        string[] parts = File.ReadAllText(path).Trim().Split('\t');
                        if (parts.Length >= 2)
                        {
                            if (parts[0] == "image")
                            {
                                Values["background.type"] = "image";
                                Values["background.path"] = parts[1];
                            }
                            else if (parts[0] == "color")
                            {
                                Values["background.type"] = "color";
                                Values["background.color"] = parts[1];
                            }
                            else
                            {
                                Values["background.type"] = "none";
                            }
                            if (parts.Length > 2) Values["background.card"] = parts[2];
                            if (parts.Length > 3) Values["background.opacity"] = parts[3];
                            changed = true;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (!Values.ContainsKey("custom.count"))
            {
                string path = Path.Combine(Folder, "custom-themes.txt");
                if (File.Exists(path))
                {
                    try
                    {
                        int index = 0;
                        foreach (string line in File.ReadAllLines(path))
                        {
                            string[] parts = line.Split('\t');
                            if (parts.Length < 3) continue;
                            string name = parts[0].Trim();
                            if (name.Length == 0) continue;
                            Values["custom." + index + ".name"] = name;
                            Values["custom." + index + ".dark"] = parts[1] == "1" ? "1" : "0";
                            Values["custom." + index + ".accent"] = parts[2];
                            index++;
                        }
                        Values["custom.count"] = index.ToString(CultureInfo.InvariantCulture);
                        changed = true;
                    }
                    catch
                    {
                    }
                }
            }

            bool saved = changed ? Save() : File.Exists(FilePath);
            if (!saved) return;

            string[] legacy =
            {
                "theme.txt", "language.txt", "snake-best.txt", "update.txt",
                "version.txt", "yt-dlp-version.txt", "background.txt",
                "custom-themes.txt"
            };
            foreach (string name in legacy)
            {
                try
                {
                    string path = Path.Combine(Folder, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                catch
                {
                }
            }
        }

        private static bool ImportSimple(string fileName, string key)
        {
            if (Values.ContainsKey(key)) return false;
            try
            {
                string path = Path.Combine(Folder, fileName);
                if (!File.Exists(path)) return false;
                Values[key] = File.ReadAllText(path).Trim();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
