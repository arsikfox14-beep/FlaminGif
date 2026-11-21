using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Video2Gif
{
    internal sealed class UpdateInfo
    {
        public bool HasUpdate;
        public string Version = "";
        public string AssetUrl = "";
        public string PageUrl = "";
        public string Notes = "";
        public long Size;
    }

    internal sealed class UpdateException : Exception
    {
        public UpdateException(string message) : base(message) { }
    }

    /// <summary>
    /// Проверка и установка обновлений через GitHub Releases.
    /// Релиз должен содержать файл FlaminGif.exe.
    /// </summary>
    internal static class Updater
    {
        public const string Repo = "arsikfox14-beep/video2gif";

        private const string Api = "https://api.github.com/repos/" + Repo + "/releases";
        private static readonly string UserAgent =
            "FlaminGif/" + Program.Version.Replace(" ", "-");

        static Updater()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            }
            catch { }
        }

        /// <summary>Возвращает сведения о последнем релизе. Бросает UpdateException.</summary>
        public static UpdateInfo Check()
        {
            try
            {
                return CheckApi();
            }
            catch (UpdateException)
            {
                // Если api.github.com недоступен (блокировка, лимит запросов),
                // пробуем обычную страницу релизов на github.com.
                return CheckWeb();
            }
        }

        private static UpdateInfo CheckApi()
        {
            string json = Get(Api + "?per_page=10");
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            object[] releases = serializer.DeserializeObject(json) as object[];
            if (releases == null) throw new UpdateException("Не удалось разобрать ответ GitHub.");

            UpdateInfo newest = null;
            foreach (object item in releases)
            {
                Dictionary<string, object> release = item as Dictionary<string, object>;
                if (release == null || Bool(release, "draft")) continue;

                string tag = Str(release, "tag_name");
                string name = Str(release, "name");
                string version = NormalizeVersion(tag.Length > 0 ? tag : name);
                if (version.Length == 0 || !IsNewer(version, Program.Version)) continue;

                string assetUrl = "";
                long size = 0;
                string zipUrl = "";
                long zipSize = 0;
                object[] assets = release.ContainsKey("assets") ? release["assets"] as object[] : null;
                if (assets != null)
                {
                    foreach (object entry in assets)
                    {
                        Dictionary<string, object> asset = entry as Dictionary<string, object>;
                        if (asset == null) continue;
                        string fileName = Str(asset, "name");

                        if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && assetUrl.Length == 0)
                        {
                            assetUrl = Str(asset, "browser_download_url");
                            size = Long(asset, "size");
                            if (string.Equals(fileName, "FlaminGif.exe", StringComparison.OrdinalIgnoreCase))
                                break;
                        }
                        else if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && zipUrl.Length == 0)
                        {
                            zipUrl = Str(asset, "browser_download_url");
                            zipSize = Long(asset, "size");
                        }
                    }
                }
                if (assetUrl.Length == 0 && zipUrl.Length > 0)
                {
                    assetUrl = zipUrl;
                    size = zipSize;
                }
                if (assetUrl.Length == 0) continue;

                if (newest == null || IsNewer(version, newest.Version))
                {
                    newest = new UpdateInfo();
                    newest.HasUpdate = true;
                    newest.Version = version;
                    newest.AssetUrl = assetUrl;
                    newest.PageUrl = Str(release, "html_url");
                    newest.Notes = Str(release, "body");
                    newest.Size = size;
                }
            }

            if (newest == null) newest = new UpdateInfo { Version = Program.Version };
            return newest;
        }

        /// <summary>
        /// Резервная проверка без api.github.com: читает страницу последнего
        /// релиза и список его файлов на обычном github.com.
        /// </summary>
        private static UpdateInfo CheckWeb()
        {
            try
            {
                string latest = "https://github.com/" + Repo + "/releases/latest";
                Uri finalUri;
                GetHtml(latest, out finalUri);

                string path = finalUri.AbsolutePath;
                int tagIndex = path.IndexOf("/tag/", StringComparison.OrdinalIgnoreCase);
                if (tagIndex < 0) throw new UpdateException("Не удалось определить последнюю версию на GitHub.");
                string tag = path.Substring(tagIndex + 5);
                string version = NormalizeVersion(tag);
                if (version.Length == 0) throw new UpdateException("Не удалось определить последнюю версию на GitHub.");

                if (!IsNewer(version, Program.Version))
                {
                    return new UpdateInfo { Version = Program.Version };
                }

                string assetsUrl = "https://github.com/" + Repo +
                    "/releases/expanded_assets/" + Uri.EscapeDataString(tag);
                Uri assetsUri;
                string html = GetHtml(assetsUrl, out assetsUri);
                string pattern = "/releases/download/" + Regex.Escape(tag) + "/[^\"'<> ]+";
                MatchCollection matches = Regex.Matches(html, pattern);

                string exe = null;
                string zip = null;
                foreach (Match match in matches)
                {
                    string url = "https://github.com" + match.Value;
                    string clean = match.Value.ToLowerInvariant();
                    if (clean.EndsWith(".exe"))
                    {
                        if (exe == null ||
                            clean.EndsWith("/video2gif.exe", StringComparison.Ordinal))
                        {
                            exe = url;
                        }
                    }
                    else if (clean.EndsWith(".zip") && zip == null)
                    {
                        zip = url;
                    }
                }

                string asset = exe != null ? exe : zip;
                if (asset == null)
                {
                    throw new UpdateException(
                        "На GitHub есть версия " + version +
                        ", но в релизе нет файла Video2Gif.exe.");
                }

                return new UpdateInfo
                {
                    HasUpdate = true,
                    Version = version,
                    AssetUrl = asset,
                    PageUrl = "https://github.com/" + Repo + "/releases/tag/" + tag
                };
            }
            catch (UpdateException)
            {
                throw;
            }
            catch (WebException)
            {
                throw new UpdateException("Не удалось связаться с GitHub. Проверьте интернет.");
            }
            catch (Exception ex)
            {
                throw new UpdateException("Не удалось проверить обновления: " + ex.Message);
            }
        }

        private static string GetHtml(string url, out Uri finalUri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = UserAgent;
            request.Accept = "text/html";
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                finalUri = response.ResponseUri;
                return reader.ReadToEnd();
            }
        }

        /// <summary>Скачивает exe релиза во временную папку, возвращает путь.</summary>
        public static string Download(UpdateInfo info, Action<int> report)
        {
            if (info == null || info.AssetUrl.Length == 0)
                throw new UpdateException("В релизе нет файла FlaminGif.exe.");

            string folder = Path.Combine(Path.GetTempPath(), "FlaminGif-update");
            Directory.CreateDirectory(folder);
            string extension = Path.GetExtension(new Uri(info.AssetUrl).AbsolutePath).ToLowerInvariant();
            if (extension != ".exe" && extension != ".zip") extension = ".exe";
            string path = Path.Combine(folder, "FlaminGif-" + info.Version + extension);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(info.AssetUrl);
            request.UserAgent = UserAgent;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (FileStream output = File.Create(path))
            {
                long total = response.ContentLength > 0 ? response.ContentLength : info.Size;
                byte[] buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    done += read;
                    if (report != null && total > 0)
                    {
                        int percent = (int)Math.Min(100, done * 100L / total);
                        report(percent);
                    }
                }
            }

            FileInfo file = new FileInfo(path);
            if (!file.Exists || file.Length < 1024 * 1024)
                throw new UpdateException("Скачанный файл повреждён. Обновление отменено.");

            if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return ExtractExe(path);

            return path;
        }

        /// <summary>Достаёт Video2Gif.exe из архива релиза.</summary>
        private static string ExtractExe(string zipPath)
        {
            string folder = Path.Combine(Path.GetTempPath(), "Video2Gif-update", "unpacked");
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch { }
            Directory.CreateDirectory(folder);

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (!entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        string target = Path.Combine(folder, "Video2Gif.exe");
                        entry.ExtractToFile(target, true);

                        FileInfo extracted = new FileInfo(target);
                        if (extracted.Length < 1024 * 1024)
                            throw new UpdateException("Файл в архиве повреждён. Обновление отменено.");
                        return target;
                    }
                }
            }
            catch (UpdateException) { throw; }
            catch (Exception ex)
            {
                throw new UpdateException("Не удалось распаковать архив обновления: " + ex.Message);
            }

            throw new UpdateException("В архиве обновления нет Video2Gif.exe.");
        }

        /// <summary>Заменяет текущий exe скачанным и перезапускает программу.</summary>
        public static void ApplyAndRestart(string downloaded)
        {
            string target = Application.ExecutablePath;
            string script = Path.Combine(Path.GetTempPath(),
                "flamingif-update-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".cmd");

            string[] lines =
            {
                "@echo off",
                "setlocal",
                "set tries=0",
                "ping 127.0.0.1 -n 2 >nul",
                "taskkill /f /im \"FlaminGif.exe\" >nul 2>&1",
                "taskkill /f /im \"Video2Gif.exe\" >nul 2>&1",
                ":wait",
                "del /f /q \"" + target + "\" >nul 2>&1",
                "if not exist \"" + target + "\" goto swap",
                "set /a tries+=1",
                "if %tries% GEQ 30 goto failed",
                "ping 127.0.0.1 -n 2 >nul",
                "goto wait",
                ":swap",
                "move /y \"" + downloaded + "\" \"" + target + "\" >nul 2>&1",
                "if not exist \"" + target + "\" copy /y \"" + downloaded + "\" \"" + target + "\" >nul 2>&1",
                "start \"\" \"" + target + "\"",
                "exit /b 0",
                ":failed",
                "start \"\" \"" + target + "\"",
                "exit /b 1"
            };
            File.WriteAllText(script, string.Join("\r\n", lines), Encoding.Default);

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"" + script + "\"",
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                throw new UpdateException("Не удалось запустить установку обновления: " + ex.Message);
            }

            // Завершаем процесс жёстко, чтобы установщик смог удалить и заменить exe.
            Environment.Exit(0);
        }

        /* -------------------------------------------------------------- JSON */

        private static string Get(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = UserAgent;
            request.Accept = "application/vnd.github+json";
            request.Timeout = 8000;
            request.ReadWriteTimeout = 8000;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Headers["X-GitHub-Api-Version"] = "2022-11-28";

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new UpdateException(
                        "На GitHub пока нет релизов. Автообновление заработает после того, " +
                        "как вы выложите релиз с файлом Video2Gif.exe.");
                }
                throw new UpdateException("Не удалось связаться с GitHub. Проверьте интернет.");
            }
        }

        private static string Str(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) return "";
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }

        private static long Long(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) return 0;
            long result;
            return long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : 0;
        }

        private static bool Bool(Dictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) return false;
            return value is bool && (bool)value;
        }

        /* ----------------------------------------------------------- Версии */

        internal static string NormalizeVersion(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string text = raw.Trim();
            if (text.Length > 0 && (text[0] == 'v' || text[0] == 'V'))
                text = text.Substring(1);

            StringBuilder builder = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsDigit(c) || c == '.') builder.Append(c);
                else if (builder.Length > 0) break;
            }

            string value = builder.ToString().Trim('.');
            return value.Length > 0 && value.IndexOf('.') >= 0 ? value : "";
        }

        internal static bool IsNewer(string candidate, string current)
        {
            int[] left = ParseVersion(candidate);
            int[] right = ParseVersion(current);
            int count = Math.Max(left.Length, right.Length);
            for (int i = 0; i < count; i++)
            {
                int a = i < left.Length ? left[i] : 0;
                int b = i < right.Length ? right[i] : 0;
                if (a > b) return true;
                if (a < b) return false;
            }
            return false;
        }

        private static int[] ParseVersion(string value)
        {
            string[] parts = (value ?? "").Split('.');
            int[] result = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                int end = 0;
                while (end < part.Length && char.IsDigit(part[end])) end++;
                int number = 0;
                if (end > 0)
                {
                    int.TryParse(part.Substring(0, end), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out number);
                }
                result[i] = number;
            }
            return result;
        }
    }
}
