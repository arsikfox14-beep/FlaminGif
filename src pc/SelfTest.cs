using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Video2Gif
{
    internal static class SelfTest
    {
        private static readonly StringBuilder Log = new StringBuilder();
        private static int failures;

        public static int Run()
        {
            string logPath = Path.Combine(Path.GetTempPath(), "flamingif-selftest.log");
            string work = Path.Combine(Path.GetTempPath(), "flamingif-selftest");
            int code = 0;

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Line(Program.AppName + " selftest, version " + Program.Version);
                Line("os: " + Environment.OSVersion + ", 64-bit: " + Environment.Is64BitProcess);
                Line("temp: " + work);

                if (Directory.Exists(work)) Directory.Delete(work, true);
                Directory.CreateDirectory(work);

                string ffmpeg = FfmpegBundle.Path;
                Line("ffmpeg path: " + ffmpeg);
                Check("ffmpeg file", File.Exists(ffmpeg) && new FileInfo(ffmpeg).Length > 10L * 1024L * 1024L);

                string versionOut;
                int versionCode = RunProcess(ffmpeg, "-hide_banner -version", out versionOut);
                Check("ffmpeg -version", versionCode == 0 && versionOut.IndexOf("ffmpeg version", StringComparison.OrdinalIgnoreCase) >= 0);
                Line("  " + FirstLine(versionOut));

                string ytdlp = YtDlpBundle.Path;
                Line("yt-dlp path: " + ytdlp);
                Check("yt-dlp file", File.Exists(ytdlp) && new FileInfo(ytdlp).Length > 15L * 1024L * 1024L);
                string ytVersion;
                int ytCode = RunProcess(ytdlp, "--version", out ytVersion);
                Check("yt-dlp version", ytCode == 0 && ytVersion.Trim().Length > 0);
                Line("  " + FirstLine(ytVersion));

                string mirrorVideo = "{\"code\":0,\"msg\":\"success\",\"data\":{\"id\":\"7320244041688419590\"," +
                    "\"title\":\"Clip\",\"author\":{\"unique_id\":\"ayrastarr\"}," +
                    "\"hdplay\":\"https://cdn.test/video.mp4\",\"play\":\"https://cdn.test/sd.mp4\"}}";
                TikTokInfo mirror = TikTok.ParseMirrorJson(mirrorVideo, "7320244041688419590");
                Check("tiktok mirror video",
                    !mirror.IsPhotoPost &&
                    mirror.Author == "ayrastarr" &&
                    mirror.VideoUrl == "https://cdn.test/video.mp4" &&
                    mirror.SuggestedName == "tiktok_ayrastarr_7320244041688419590.mp4");

                string mirrorPhoto = "{\"code\":0,\"msg\":\"success\",\"data\":{\"id\":\"7000000000000000001\"," +
                    "\"title\":\"Photos\",\"author\":{\"unique_id\":\"cat\"}," +
                    "\"images\":[\"https://cdn.test/1.jpeg\",\"//cdn.test/2.jpeg\"]}}";
                TikTokInfo mirrorPhotos = TikTok.ParseMirrorJson(mirrorPhoto, "7000000000000000001");
                Check("tiktok mirror photos",
                    mirrorPhotos.IsPhotoPost &&
                    mirrorPhotos.PhotoUrls.Count == 2 &&
                    mirrorPhotos.PhotoUrls[1] == "https://cdn.test/2.jpeg" &&
                    mirrorPhotos.SuggestedName == "tiktok_cat_7000000000000000001");

                bool mirrorError = false;
                try { TikTok.ParseMirrorJson("{\"code\":-1,\"msg\":\"Url parsing is failed!\"}", "1"); }
                catch (TikTokException) { mirrorError = true; }
                Check("tiktok mirror error", mirrorError);

                string spotifyJson = "{\"props\":{\"pageProps\":{\"state\":{\"data\":{\"entity\":{\"type\":\"playlist\",\"name\":\"Road Trip\",\"trackList\":[" +
                    "{\"title\":\"Song One\",\"subtitle\":\"Artist A\",\"duration\":1000,\"isPlayable\":true}," +
                    "{\"title\":\"Song Two\",\"subtitle\":\"Artist B\",\"duration\":2000,\"isPlayable\":false}]}}}}}}";
                string spotifyHtml = "<script id=\"__NEXT_DATA__\" type=\"application/json\">" +
                    spotifyJson + "</script>";
                List<MediaTrack> spotifyTracks = SpotifyLinks.ParseHtml(spotifyHtml);
                Check("spotify link parse", spotifyTracks.Count == 1 &&
                    spotifyTracks[0].Title == "Song One" &&
                    spotifyTracks[0].Query == "Artist A Song One");

                SpotifySelection spotifySelection = SpotifyLinks.ParseSelection(spotifyHtml);
                Check("spotify playlist folder",
                    spotifySelection.CollectionName == "Road Trip" &&
                    spotifySelection.Tracks.Count == 1);

                string spotifyTrackJson = "{\"props\":{\"pageProps\":{\"state\":{\"data\":{\"entity\":{\"type\":\"track\",\"name\":\"Song\",\"duration\":1000,\"isPlayable\":true,\"artists\":[{\"name\":\"Artist\"}]}}}}}}";
                string spotifyTrackHtml = "<script id=\"__NEXT_DATA__\" type=\"application/json\">" +
                    spotifyTrackJson + "</script>";
                SpotifySelection spotifySingle = SpotifyLinks.ParseSelection(spotifyTrackHtml);
                Check("spotify track no folder",
                    spotifySingle.CollectionName.Length == 0 &&
                    spotifySingle.Tracks.Count == 1 &&
                    spotifySingle.Tracks[0].Query == "Artist Song");

                MediaTrack timed = new MediaTrack();
                timed.DurationMs = 213000;
                Check("spotify duration filter",
                    MediaDownloader.DurationFilter(timed) == "duration >= 203 & duration <= 223");
                Check("spotify duration filter off",
                    MediaDownloader.DurationFilter(new MediaTrack()) == null);
                Check("default folder desktop", Directory.Exists(SavePaths.Desktop()));

                SnakeGame snake = new SnakeGame(20, 15);
                Check("snake initial",
                    snake.Snake.Count == 3 && snake.Alive &&
                    snake.Direction.X == 1 && snake.Direction.Y == 0);
                Point snakeHead = snake.Snake[0];
                Check("snake step",
                    snake.Step() == SnakeStep.Moved &&
                    snake.Snake[0].X == snakeHead.X + 1 && snake.Snake.Count == 3);
                snake.Turn(-1, 0);
                snake.Step();
                Check("snake no reverse", snake.Snake[0].X == snakeHead.X + 2);
                snake.Food = new Point(snake.Snake[0].X + 1, snake.Snake[0].Y);
                Check("snake eat",
                    snake.Step() == SnakeStep.Ate && snake.Score == 1 && snake.Snake.Count == 4);

                using (Bitmap cutSource = new Bitmap(60, 60, PixelFormat.Format32bppArgb))
                {
                    using (Graphics cutGraphics = Graphics.FromImage(cutSource))
                    {
                        cutGraphics.Clear(Color.White);
                        using (SolidBrush subject = new SolidBrush(
                            Color.FromArgb(210, 70, 90)))
                        {
                            cutGraphics.FillEllipse(subject, 10, 10, 40, 40);
                        }
                    }
                    Color cutKey = CutoutCore.AutoColor(cutSource);
                    Check("cutout auto color",
                        cutKey.R > 240 && cutKey.G > 240 && cutKey.B > 240);
                    using (Bitmap cut = CutoutCore.RemoveBackground(
                        cutSource, Color.White, 20, 0, true))
                    {
                        Check("cutout background",
                            cut.GetPixel(1, 1).A == 0 && cut.GetPixel(30, 30).A == 255);
                    }
                    using (Bitmap cut = CutoutCore.RemoveBackground(
                        cutSource, Color.White, 20, 5, true))
                    {
                        int edge = cut.GetPixel(11, 30).A;
                        Check("cutout soft edge", edge > 0 && edge < 255);
                    }
                }

                using (Bitmap smartSource = new Bitmap(200, 160,
                    PixelFormat.Format32bppArgb))
                {
                    using (Graphics smartGraphics = Graphics.FromImage(smartSource))
                    {
                        smartGraphics.Clear(Color.FromArgb(236, 232, 228));
                        using (SolidBrush skin = new SolidBrush(
                            Color.FromArgb(222, 182, 162)))
                        {
                            smartGraphics.FillEllipse(skin, 30, 20, 140, 150);
                        }
                        using (SolidBrush light = new SolidBrush(
                            Color.FromArgb(240, 214, 202)))
                        {
                            smartGraphics.FillEllipse(light, 75, 35, 34, 24);
                            smartGraphics.FillEllipse(light, 60, 80, 20, 14);
                        }
                    }
                    Color smartKey = CutoutCore.AutoColor(smartSource);
                    int smartTolerance = CutoutCore.SuggestTolerance(smartSource, smartKey);
                    Check("cutout smart key",
                        smartKey.R > 230 && smartKey.G > 225 && smartKey.B > 220 &&
                        smartTolerance <= 16);
                    using (Bitmap cut = CutoutCore.RemoveBackground(smartSource,
                        smartKey, smartTolerance, 2, true))
                    {
                        Check("cutout keeps highlights",
                            cut.GetPixel(2, 2).A == 0 &&
                            cut.GetPixel(50, 90).A == 255 &&
                            cut.GetPixel(90, 45).A == 255 &&
                            cut.GetPixel(68, 86).A == 255);
                    }
                }

                bool foodOk = true;
                for (int i = 0; i < 40; i++)
                {
                    snake.PlaceFood();
                    if (snake.Food.X < 0 || snake.Food.Y < 0 ||
                        snake.Food.X >= snake.Cols || snake.Food.Y >= snake.Rows ||
                        snake.Snake.Contains(snake.Food))
                    {
                        foodOk = false;
                        break;
                    }
                }
                Check("snake food free cell", foodOk);

                SnakeGame wall = new SnakeGame(20, 15);
                for (int i = 0; i < 20 && wall.Alive; i++) wall.Step();
                Check("snake wall", !wall.Alive);

                SnakeGame self = new SnakeGame(20, 15);
                self.Food = new Point(self.Snake[0].X + 1, self.Snake[0].Y);
                self.Step();
                self.Food = new Point(self.Snake[0].X + 1, self.Snake[0].Y);
                self.Step();
                self.Turn(0, -1);
                self.Step();
                self.Turn(-1, 0);
                self.Step();
                self.Turn(0, 1);
                Check("snake self collision", self.Step() == SnakeStep.HitSelf && !self.Alive);

                string probe = Path.Combine(work, "probe song.mp3");
                File.WriteAllText(probe, "x");
                Check("download path resolve",
                    MediaDownloader.ResolvePath(@"C:\NO-SUCH~1\probe song.mp3", work) == probe);

                Check("playlist folder sanitize",
                    MediaDownloader.SanitizeFolderName("My: Playlist / 2026?") == "My Playlist 2026");
                Check("playlist folder fallback",
                    MediaDownloader.SanitizeFolderName(" . ") == "Плейлист");
                Check("playlist folder reserved",
                    MediaDownloader.SanitizeFolderName("CON") == "_CON");

                string playlists = Path.Combine(work, "playlists");
                Directory.CreateDirectory(playlists);
                string firstFolder = MediaDownloader.MakeCollectionFolder(playlists, "My: Mix");
                Check("playlist folder name", Path.GetFileName(firstFolder) == "My Mix");
                Directory.CreateDirectory(firstFolder);
                File.WriteAllText(Path.Combine(firstFolder, "track.mp3"), "x");
                string secondFolder = MediaDownloader.MakeCollectionFolder(playlists, "My: Mix");
                Check("playlist folder unique",
                    secondFolder != firstFolder &&
                    Path.GetFileName(secondFolder) == "My Mix (2)");
                Directory.CreateDirectory(secondFolder);
                Check("playlist folder reuse empty",
                    MediaDownloader.MakeCollectionFolder(playlists, "My: Mix") == secondFolder);

                MethodInfo qualityMethod = typeof(MediaDownloader).GetMethod(
                    "QualityVideoFormat", BindingFlags.Static | BindingFlags.NonPublic);
                string quality720 = qualityMethod != null
                    ? (string)qualityMethod.Invoke(null, new object[] { "720" })
                    : "";
                Check("download quality format",
                    quality720.Length > 0 && quality720.IndexOf("height<=720") >= 0);

                Check("theme seasonal halloween",
                    Ui.SeasonalTheme(new DateTime(2026, 10, 1)) == ThemeKind.Halloween &&
                    Ui.SeasonalTheme(new DateTime(2026, 10, 31)) == ThemeKind.Halloween &&
                    Ui.SeasonalTheme(new DateTime(2026, 11, 1)) == ThemeKind.Halloween &&
                    Ui.SeasonalTheme(new DateTime(2026, 11, 2)) == ThemeKind.Dark);
                Check("theme seasonal new year",
                    Ui.SeasonalTheme(new DateTime(2026, 12, 1)) == ThemeKind.NewYear &&
                    Ui.SeasonalTheme(new DateTime(2026, 12, 31)) == ThemeKind.NewYear &&
                    Ui.SeasonalTheme(new DateTime(2027, 1, 1)) == ThemeKind.NewYear &&
                    Ui.SeasonalTheme(new DateTime(2027, 1, 2)) == ThemeKind.Dark);

                Ui.SetThemeAuto(new DateTime(2026, 10, 15));
                Check("theme auto halloween",
                    Ui.AutoTheme && Ui.ThemeName == "halloween");
                Check("theme auto switches season",
                    Ui.RefreshAutoTheme(new DateTime(2026, 12, 5)) &&
                    Ui.ThemeName == "newyear");
                Ui.SetTheme("light");
                Check("theme manual overrides auto",
                    !Ui.AutoTheme && Ui.ThemeName == "light");

                Ui.SetTheme("light");
                Color lightBg = Ui.Bg;
                Check("theme light", !Ui.Dark && !Ui.Holiday && Ui.ThemeName == "light");
                Ui.SetTheme("dark");
                Check("theme dark",
                    Ui.Dark && !Ui.Holiday && Ui.ThemeName == "dark" && Ui.Bg != lightBg);
                Ui.SetTheme("newyear");
                Color newYearAccent = Ui.Accent;
                Check("theme new year",
                    Ui.Dark && Ui.NewYear && Ui.ThemeName == "newyear" && newYearAccent != Ui.Bg);
                Ui.SetTheme("halloween");
                Check("theme halloween",
                    Ui.Dark && Ui.Halloween && Ui.ThemeName == "halloween" && Ui.Accent != newYearAccent);
                Ui.SetTheme("gta6");
                Check("theme gta 6",
                    Ui.Dark && Ui.Gta6 && Ui.ThemeName == "gta6" &&
                    Ui.Accent == Color.FromArgb(255, 79, 163) && !Ui.Holiday);

                Ui.SetTheme("gamesense");
                Check("theme gamesense",
                    Ui.Dark && Ui.Gamesense && Ui.ThemeName == "gamesense" &&
                    Ui.Accent == Color.FromArgb(88, 178, 124) && !Ui.Holiday);

                Ui.SetTheme("sakura");
                Check("theme sakura",
                    Ui.Dark && Ui.Sakura && Ui.ThemeName == "sakura" &&
                    Ui.Accent == Color.FromArgb(238, 138, 163) && !Ui.Holiday);
                Ui.SetTheme("summer");
                Check("theme summer",
                    Ui.Dark && Ui.Summer && Ui.ThemeName == "summer" &&
                    Ui.Accent == Color.FromArgb(255, 154, 74) && !Ui.Holiday);
                Ui.SetTheme("flamingif");
                Check("theme flamingif",
                    Ui.Dark && Ui.Backrooms && Ui.ThemeName == "flamingif" &&
                    Ui.Accent == Color.FromArgb(232, 201, 83) && !Ui.Holiday);

                Check("theme seasonal sakura",
                    Ui.SeasonalTheme(new DateTime(2026, 3, 1)) == ThemeKind.Sakura &&
                    Ui.SeasonalTheme(new DateTime(2026, 5, 31)) == ThemeKind.Sakura);
                Check("theme seasonal summer",
                    Ui.SeasonalTheme(new DateTime(2026, 6, 1)) == ThemeKind.Summer &&
                    Ui.SeasonalTheme(new DateTime(2026, 8, 31)) == ThemeKind.Summer &&
                    Ui.SeasonalTheme(new DateTime(2026, 9, 1)) == ThemeKind.Dark);
                Check("gamesense rainbow",
                    Ui.Rainbow(0.0, 0.8, 1.0).R > 200 &&
                    Ui.Rainbow(1.0 / 3.0, 0.8, 1.0).G > 200 &&
                    Ui.Rainbow(0.0, 0.8, 1.0) != Ui.Rainbow(0.5, 0.8, 1.0));

                ColorBlend gtaBlend = Ui.GtaBlend();
                Check("theme gta 6 gradient",
                    gtaBlend.Colors.Length == 5 &&
                    gtaBlend.Colors[0].B > gtaBlend.Colors[0].R &&
                    gtaBlend.Colors[4].R > gtaBlend.Colors[4].B);

                Check("theme gta 6 window",
                    Ui.Gta6Window(new DateTime(2026, 11, 18, 15, 0, 0)) &&
                    Ui.Gta6Window(new DateTime(2026, 11, 19, 12, 0, 0)) &&
                    !Ui.Gta6Window(new DateTime(2026, 11, 18, 14, 59, 0)) &&
                    !Ui.Gta6Window(new DateTime(2026, 11, 20, 0, 0, 0)));

                Check("theme gta 6 auto window",
                    Ui.SeasonalTheme(new DateTime(2026, 11, 18, 15, 0, 0)) == ThemeKind.Gta6 &&
                    Ui.SeasonalTheme(new DateTime(2026, 11, 20, 0, 0, 0)) == ThemeKind.Dark);

                using (Bitmap mark = new Bitmap(32, 32))
                using (Graphics markGraphics = Graphics.FromImage(mark))
                {
                    DiscordMark.Draw(markGraphics,
                        new RectangleF(4, 4, 24, 24), Color.White);
                    int lit = 0;
                    for (int y = 0; y < mark.Height; y++)
                    {
                        for (int x = 0; x < mark.Width; x++)
                        {
                            if (mark.GetPixel(x, y).A > 0) lit++;
                        }
                    }
                    Check("discord logo draws", DiscordMark.Available && lit > 120);
                }

                Check("updater version parse",
                    Updater.NormalizeVersion("v3.8") == "3.8" &&
                    Updater.NormalizeVersion("release 3.9") == "3.9" &&
                    Updater.IsNewer("3.8", "3.7") &&
                    !Updater.IsNewer("3.7", "3.7") &&
                    Updater.IsNewer("3.10", "3.9") &&
                    !Updater.IsNewer("3.7", "4.0"));

                bool gridVisible = true;
                string[] gridThemes = new string[] { "light", "dark", "newyear", "halloween", "gta6", "flamingif" };
                for (int i = 0; i < gridThemes.Length; i++)
                {
                    Ui.SetTheme(gridThemes[i]);
                    Color checker = SnakeForm.CheckerColor;
                    int delta = Math.Abs(checker.R - Ui.Card.R) +
                        Math.Abs(checker.G - Ui.Card.G) +
                        Math.Abs(checker.B - Ui.Card.B);
                    if (delta < 40) gridVisible = false;
                }
                Check("snake grid visible", gridVisible);

                Ui.SetTheme("unknown");
                Check("theme unknown falls back to dark", Ui.ThemeName == "dark");
                Check("theme title bar colorref",
                    Ui.ColorRef(Color.FromArgb(1, 2, 3)) == 0x030201);
                Ui.SetDark(true);

                string sample = Path.Combine(work, "sample.mp4");
                string genOut;
                int genCode = RunProcess(ffmpeg,
                    "-hide_banner -loglevel error -f lavfi -i testsrc2=size=320x240:rate=25 -t 2 " +
                    "-pix_fmt yuv420p -y " + Fmt.Quote(sample), out genOut);
                Check("generate sample", genCode == 0 && File.Exists(sample) && new FileInfo(sample).Length > 1000);

                VideoInfo info = Ffmpeg.Probe(sample);
                Line("probe: " + info.Describe() + ", codec " + info.Codec);
                Check("probe size", info.Width == 320 && info.Height == 240);
                Check("probe duration", info.Duration > 1.5 && info.Duration < 3.0);

                double durationValue;
                Check("duration empty means whole clip",
                    Fmt.TryDuration("", out durationValue) && durationValue == 0);
                Check("duration zero means whole clip",
                    Fmt.TryDuration("0", out durationValue) && durationValue == 0 &&
                    Fmt.TryDuration("0,0", out durationValue) && durationValue == 0);
                Check("duration normal value",
                    Fmt.TryDuration("2,5", out durationValue) && Math.Abs(durationValue - 2.5) < 0.001);
                Check("duration negative rejected", !Fmt.TryDuration("-1", out durationValue));
                Check("duration text rejected", !Fmt.TryDuration("abc", out durationValue));

                string fast = Path.Combine(work, "fast.gif");
                int fastMax = RunConversion(info, sample, fast, true);
                Check("fast gif", IsGif(fast) && new FileInfo(fast).Length > 1000);
                Check("fast progress", fastMax >= 90);
                Line("  fast.gif: " + Fmt.Size(new FileInfo(fast).Length) + ", progress max " + fastMax + "%");

                string slow = Path.Combine(work, "two-pass.gif");
                int slowMax = RunConversion(info, sample, slow, false);
                Check("two-pass gif", IsGif(slow) && new FileInfo(slow).Length > 1000);
                Check("two-pass progress", slowMax >= 90);
                Line("  two-pass.gif: " + Fmt.Size(new FileInfo(slow).Length) + ", progress max " + slowMax + "%");

                string still = Path.Combine(work, "still.png");
                string stillOut;
                int stillCode = RunProcess(ffmpeg,
                    "-hide_banner -loglevel error -f lavfi -i testsrc2=size=400x300 -frames:v 1 -y " +
                    Fmt.Quote(still), out stillOut);
                Check("generate image", stillCode == 0 && File.Exists(still));

                string gifFrames = Path.Combine(work, "gif-frames");
                int gifFrameCount = EditorCore.ExtractGifFrames(fast, gifFrames);
                Check("gif split frames", gifFrameCount >= 10);
                string gifBuild = Path.Combine(work, "gif-build");
                Directory.CreateDirectory(gifBuild);
                string[] frameFiles = Directory.GetFiles(gifFrames, "frame-*.png");
                Array.Sort(frameFiles, StringComparer.Ordinal);
                for (int i = 0; i < frameFiles.Length; i++)
                {
                    File.Copy(frameFiles[i], Path.Combine(gifBuild,
                        "f" + (i + 1).ToString("0000") + ".png"), true);
                }
                string gifRebuilt = Path.Combine(work, "gif-rebuilt.gif");
                EditorCore.AssembleGif(gifBuild, 20, 160, gifRebuilt);
                Check("gif assemble", IsGif(gifRebuilt) &&
                    new FileInfo(gifRebuilt).Length > 1000);

                string decorated = Path.Combine(work, "editor-decorated.mp4");
                EditorCore.TrimVideo(sample, decorated, 0.4, 1.4,
                    still, 3, 20, "Test caption", false, 320, 240);
                VideoInfo decoratedInfo = Ffmpeg.Probe(decorated);
                Check("editor video overlay",
                    File.Exists(decorated) &&
                    new FileInfo(decorated).Length > 1000 &&
                    decoratedInfo.Duration > 0.6 && decoratedInfo.Duration < 1.8);

                string decoratedPhoto = Path.Combine(work, "editor-decorated.png");
                using (Bitmap basePhoto = new Bitmap(240, 160))
                {
                    using (Graphics graphics = Graphics.FromImage(basePhoto))
                    {
                        graphics.Clear(Color.FromArgb(255, 40, 80, 120));
                    }
                    EditorCore.DecoratePhoto(basePhoto, still, 3, 20,
                        "Test caption", false);
                    EditorCore.SaveBitmap(basePhoto, decoratedPhoto);
                }
                Check("editor photo overlay",
                    File.Exists(decoratedPhoto) &&
                    new FileInfo(decoratedPhoto).Length > 1000);

                string trimmed = Path.Combine(work, "editor-trim.mp4");
                EditorCore.TrimVideo(sample, trimmed, 0.4, 1.4);
                VideoInfo trimmedInfo = Ffmpeg.Probe(trimmed);
                Check("editor video trim",
                    File.Exists(trimmed) && new FileInfo(trimmed).Length > 1000 &&
                    trimmedInfo.Duration > 0.6 && trimmedInfo.Duration < 1.8);

                string frameShot = Path.Combine(work, "editor-frame.png");
                EditorCore.ExtractFrame(sample, 0.5, frameShot);
                bool frameSize = false;
                if (File.Exists(frameShot))
                {
                    using (Bitmap frame = EditorCore.LoadBitmap(frameShot))
                    {
                        frameSize = frame.Width == 320 && frame.Height == 240;
                    }
                }
                Check("editor preview frame", frameSize);

                using (Bitmap source = new Bitmap(40, 30))
                {
                    using (Graphics graphics = Graphics.FromImage(source))
                    {
                        graphics.Clear(Color.FromArgb(255, 30, 30, 30));
                    }
                    using (Bitmap cropped = EditorCore.CropBitmap(
                        source, new Rectangle(10, 5, 20, 15)))
                    {
                        Check("editor photo crop",
                            cropped.Width == 20 && cropped.Height == 15);
                    }
                }

                RangeBar rangeBar = new RangeBar();
                rangeBar.MinGap = 0.05;
                rangeBar.SetRange(-1, 2);
                Check("editor range clamp", rangeBar.Start == 0 && rangeBar.End == 1);
                rangeBar.SetRange(0.8, 0.2);
                Check("editor range order",
                    rangeBar.Start <= rangeBar.End &&
                    rangeBar.End - rangeBar.Start >= 0.05);

                string previewDir = Path.Combine(work, "editor-preview");
                EditorCore.ExtractPreviewFrames(sample, previewDir, 0.4, 1.0, 8);
                Check("editor preview frames",
                    Directory.Exists(previewDir) &&
                    Directory.GetFiles(previewDir, "*.jpg").Length >= 5);

                VideoInfo imageInfo = Ffmpeg.Probe(still);
                Line("image probe: " + imageInfo.Describe());
                Check("probe image", imageInfo.IsImage && imageInfo.Width == 400 && imageInfo.Height == 300);

                string imageFast = Path.Combine(work, "image-fast.gif");
                int imageFastMax = RunConversion(imageInfo, still, imageFast, true);
                Check("image fast gif", IsGif(imageFast) && new FileInfo(imageFast).Length > 1000);
                Check("image fast progress", imageFastMax >= 90);

                string imageSlow = Path.Combine(work, "image-two-pass.gif");
                int imageSlowMax = RunConversion(imageInfo, still, imageSlow, false);
                Check("image two-pass gif", IsGif(imageSlow) && new FileInfo(imageSlow).Length > 1000);
                Check("image two-pass progress", imageSlowMax >= 90);
                Line("  image gif: " + Fmt.Size(new FileInfo(imageSlow).Length) + ", progress max " + imageSlowMax + "%");

                string imageOrigFast = Path.Combine(work, "image-orig-fast.gif");
                int imageOrigFastMax = RunConversion(imageInfo, still, imageOrigFast, true, 0);
                Check("image original width fast gif", IsGif(imageOrigFast) && new FileInfo(imageOrigFast).Length > 1000);
                Check("image original width fast progress", imageOrigFastMax >= 90);

                string imageOrigSlow = Path.Combine(work, "image-orig-two-pass.gif");
                int imageOrigSlowMax = RunConversion(imageInfo, still, imageOrigSlow, false, 0);
                Check("image original width two-pass gif", IsGif(imageOrigSlow) && new FileInfo(imageOrigSlow).Length > 1000);
                Check("image original width two-pass progress", imageOrigSlowMax >= 90);

                string longInput = Path.Combine(work, "long.mp4");
                string longOut;
                int longCode = RunProcess(ffmpeg,
                    "-hide_banner -loglevel error -f lavfi -i testsrc2=size=640x480:rate=30 -t 6 " +
                    "-pix_fmt yuv420p -y " + Fmt.Quote(longInput), out longOut);
                Check("generate long sample", longCode == 0 && File.Exists(longInput));
                if (longCode == 0)
                {
                    ConversionOptions cancelOptions = new ConversionOptions();
                    cancelOptions.Input = longInput;
                    cancelOptions.Output = Path.Combine(work, "cancel.gif");
                    cancelOptions.Palette = Path.Combine(work, "cancel-palette.png");
                    cancelOptions.Width = 640;
                    cancelOptions.Fps = 15;
                    cancelOptions.Colors = 256;
                    cancelOptions.Dither = "bayer";
                    cancelOptions.Loop = true;
                    cancelOptions.Info = Ffmpeg.Probe(longInput);

                    Converter cancelConverter = new Converter();
                    System.Threading.Thread killer = new System.Threading.Thread(delegate()
                    {
                        System.Threading.Thread.Sleep(450);
                        cancelConverter.Cancel();
                    });
                    killer.Start();
                    bool cancelled = false;
                    try
                    {
                        cancelConverter.Run(cancelOptions, delegate(int p, string s) { });
                    }
                    catch (ConversionCancelledException)
                    {
                        cancelled = true;
                    }
                    catch (Exception ex)
                    {
                        Line("  cancel error: " + ex.Message);
                    }
                    killer.Join();
                    Check("cancel conversion", cancelled);
                }

                string ui = Path.Combine(work, "ui.png");
                try
                {
                    using (MainForm form = new MainForm())
                    {
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-4000, -4000);
                        form.Show();
                        Ui.SetTheme("light");
                        Invoke(form, "ApplyThemeNow");
                        WaitUntil(delegate { return GetField(form, "ffmpegReady") is bool && (bool)GetField(form, "ffmpegReady"); }, 6000);

                        Ui.SetTheme("newyear");
                        Invoke(form, "ApplyThemeNow");
                        Application.DoEvents();
                        string emptyNewYear = Path.Combine(work, "ui-empty-newyear.png");
                        SaveShot(form, emptyNewYear);
                        Check("ui empty new year screenshot",
                            File.Exists(emptyNewYear) && new FileInfo(emptyNewYear).Length > 2000);

                        Ui.SetTheme("halloween");
                        Invoke(form, "ApplyThemeNow");
                        Application.DoEvents();
                        string emptyHalloween = Path.Combine(work, "ui-empty-halloween.png");
                        SaveShot(form, emptyHalloween);
                        Check("ui empty halloween screenshot",
                            File.Exists(emptyHalloween) && new FileInfo(emptyHalloween).Length > 2000);

                        Ui.SetTheme("light");
                        Invoke(form, "ApplyThemeNow");
                        Application.DoEvents();
                        Invoke(form, "LoadVideo", sample);
                        WaitUntil(delegate { return GetField(form, "info") != null; }, 10000);
                        SaveShot(form, ui);
                        Check("ui loaded screenshot", File.Exists(ui) && new FileInfo(ui).Length > 2000);
                        PillButton uiConvert = GetField(form, "convertButton") as PillButton;
                        Check("ui convert enabled", uiConvert != null && uiConvert.Enabled);

                        Check("ui presets removed",
                            GetField(form, "presetCompact") == null &&
                            GetField(form, "presetBalanced") == null &&
                            GetField(form, "presetQuality") == null);
                        FlatInput fpsBox = GetField(form, "fpsInput") as FlatInput;
                        Check("ui fps default 30", fpsBox != null && fpsBox.Value == "30");
                        Check("ui colors slider removed", GetField(form, "colorsSlider") == null);
                        IconButton game = GetField(form, "gameButton") as IconButton;
                        IconButton editor = GetField(form, "editorButton") as IconButton;
                        IconButton gifTools = GetField(form, "gifToolsButton") as IconButton;
                        IconButton download = GetField(form, "downloadsButton") as IconButton;
                        IconButton settings = GetField(form, "settingsButton") as IconButton;
                        Check("ui mouse button",
                            game != null && game.Kind == IconKind.Mouse);
                        Check("ui editor icon button",
                            editor != null && editor.Kind == IconKind.Editor);
                        Check("ui gif tools icon button",
                            gifTools != null && gifTools.Kind == IconKind.Frames);
                        Check("ui download icon button",
                            download != null && download.Kind == IconKind.Download);
                        Check("ui settings icon button",
                            settings != null && settings.Kind == IconKind.Settings);
                        Check("ui header order",
                            game != null && editor != null && gifTools != null && download != null &&
                            editor.Left > game.Left && gifTools.Left > editor.Left &&
                            download.Left > gifTools.Left);
                        Check("ui header buttons compact",
                            game != null && editor != null && gifTools != null &&
                            download != null && settings != null &&
                            game.Width <= Ui.P(34) && editor.Width <= Ui.P(34) &&
                            gifTools.Width <= Ui.P(34) &&
                            download.Width <= Ui.P(34) && settings.Width <= Ui.P(34));

                        IconButton discord = GetField(form, "discordButton") as IconButton;
                        IconButton donate = GetField(form, "donateButton") as IconButton;
                        Check("ui social icon buttons",
                            discord != null && discord.Kind == IconKind.Discord &&
                            donate != null && donate.Kind == IconKind.Donate);

                        IconButton cutout = GetField(form, "cutoutButton") as IconButton;
                        Check("ui cutout icon button",
                            cutout != null && cutout.Kind == IconKind.Cutout &&
                            cutout.Left + cutout.Width <= form.ClientSize.Width);

                        InfoBadge infoBadge = GetField(form, "infoBadge") as InfoBadge;
                        ToolTip creditsTip = GetField(form, "headerTip") as ToolTip;
                        Check("ui credits info badge",
                            infoBadge != null &&
                            infoBadge.InfoText.IndexOf("Coders") >= 0 &&
                            infoBadge.InfoText.IndexOf("beta-testers") >= 0 &&
                            infoBadge.InfoText.IndexOf("donaters") >= 0);
                        Check("ui credits tooltip",
                            creditsTip != null && infoBadge != null &&
                            creditsTip.GetToolTip(infoBadge).IndexOf("Coders") >= 0);
                        Check("ui credits badge size",
                            infoBadge != null &&
                            infoBadge.Width >= Ui.P(20) && infoBadge.Width <= Ui.P(26));
                        Check("ui credits badge place",
                            infoBadge != null &&
                            infoBadge.Top >= Ui.P(550) &&
                            infoBadge.Bottom <= form.ClientSize.Height - Ui.P(2));

                        Invoke(form, "ShowCredits");
                        Application.DoEvents();
                        CreditsForm creditsForm = GetField(form, "credits") as CreditsForm;
                        Check("ui credits opens", creditsForm != null && creditsForm.Visible);
                        if (creditsForm != null) creditsForm.Close();
                        Check("ui social buttons in header",
                            discord != null && donate != null && settings != null &&
                            discord.Left > settings.Left && donate.Left > discord.Left &&
                            donate.Right <= form.ClientSize.Width - Ui.P(16));

                        Invoke(form, "FinishSuccess", fast);
                        Application.DoEvents();
                        string resultShot = Path.Combine(work, "ui-result.png");
                        SaveShot(form, resultShot);
                        Check("ui result screenshot", File.Exists(resultShot) && new FileInfo(resultShot).Length > 2000);

                        Ui.SetTheme("dark");
                        Invoke(form, "ApplyThemeNow");
                        Application.DoEvents();
                        string darkShot = Path.Combine(work, "ui-dark.png");
                        SaveShot(form, darkShot);
                        Check("ui dark screenshot", File.Exists(darkShot) && new FileInfo(darkShot).Length > 2000);

                        Ui.SetTheme("newyear");
                        Invoke(form, "ApplyThemeNow");
                        Application.DoEvents();
                        string newYearShot = Path.Combine(work, "ui-newyear.png");
                        SaveShot(form, newYearShot);
                        Check("ui new year screenshot",
                            File.Exists(newYearShot) && new FileInfo(newYearShot).Length > 2000);

                        Ui.SetTheme("halloween");
                        Invoke(form, "ApplyThemeNow");
                        Application.DoEvents();
                        string halloweenShot = Path.Combine(work, "ui-halloween.png");
                        SaveShot(form, halloweenShot);
                        Check("ui halloween screenshot",
                            File.Exists(halloweenShot) && new FileInfo(halloweenShot).Length > 2000);

                        Ui.SetTheme("dark");
                        Invoke(form, "ApplyThemeNow");
                        Application.DoEvents();

                        Invoke(form, "ShowProgress", "Строим палитру", "2 с · 30 fps · 320 px");
                        SetField(form, "busy", true);
                        Invoke(form, "UpdateActions");
                        SlimBar bar = GetField(form, "progressBar") as SlimBar;
                        if (bar != null) bar.Value = 42;
                        Label pct = GetField(form, "percentLabel") as Label;
                        if (pct != null) pct.Text = "42%";
                        Application.DoEvents();
                        string progressShot = Path.Combine(work, "ui-progress.png");
                        SaveShot(form, progressShot);
                        Check("ui progress screenshot", File.Exists(progressShot) && new FileInfo(progressShot).Length > 2000);

                        SetField(form, "busy", false);
                        Invoke(form, "UpdateActions");
                        Invoke(form, "LoadVideo", still);
                        WaitUntil(delegate
                        {
                            VideoInfo vi = GetField(form, "info") as VideoInfo;
                            return vi != null && vi.IsImage;
                        }, 10000);
                        FlatInput uiDuration = GetField(form, "durationInput") as FlatInput;
                        FlatInput uiFps = GetField(form, "fpsInput") as FlatInput;
                        Check("ui image fields disabled",
                            uiDuration != null && !uiDuration.Enabled && uiFps != null && !uiFps.Enabled);
                        Application.DoEvents();
                        string imageShot = Path.Combine(work, "ui-image.png");
                        SaveShot(form, imageShot);
                        Check("ui image screenshot", File.Exists(imageShot) && new FileInfo(imageShot).Length > 2000);

                        string previousResult = GetField(form, "resultPath") as string;
                        Invoke(form, "StartConversion");
                        WaitUntil(delegate
                        {
                            string rp = GetField(form, "resultPath") as string;
                            return !(bool)GetField(form, "busy") && rp != null && rp != previousResult;
                        }, 30000);
                        string uiImageGif = GetField(form, "resultPath") as string;
                        Check("ui image conversion",
                            uiImageGif != null && uiImageGif != previousResult && IsGif(uiImageGif));

                        Invoke(form, "LoadVideo", sample);
                        WaitUntil(delegate
                        {
                            VideoInfo vi = GetField(form, "info") as VideoInfo;
                            return vi != null && !vi.IsImage;
                        }, 10000);
                        Check("ui video fields restored",
                            uiDuration != null && uiFps != null && uiDuration.Enabled && uiFps.Enabled);

                        if (!Ui.Dark) Ui.SetTheme("dark");
                        Invoke(form, "ApplyThemeNow");
                        form.Close();
                    }
                    Line("  ui.png: " + Fmt.Size(new FileInfo(ui).Length));
                }
                catch (Exception ex)
                {
                    Check("ui screenshot", false);
                    Line("  ui error: " + ex.Message);
                }

                string settingsShot = Path.Combine(work, "ui-settings.png");
                try
                {
                    Ui.SetTheme("dark");
                    using (SettingsForm dialog = new SettingsForm(
                        delegate(string name)
                        {
                            if (name == "auto") Ui.SetThemeAuto(new DateTime(2026, 10, 15));
                            else Ui.SetTheme(name);
                        }))
                    {
                        dialog.StartPosition = FormStartPosition.Manual;
                        dialog.Location = new Point(-4000, -4000);
                        dialog.Show();
                        Application.DoEvents();
                        SaveShot(dialog, settingsShot);
                        Check("settings screenshot",
                            File.Exists(settingsShot) && new FileInfo(settingsShot).Length > 2000);

                        ThemeTile autoTile = GetField(dialog, "autoTile") as ThemeTile;
                        ThemeTile newYearTile = GetField(dialog, "newYearTile") as ThemeTile;
                        ThemeTile halloweenTile = GetField(dialog, "halloweenTile") as ThemeTile;
                        ThemeTile gtaTile = GetField(dialog, "gtaTile") as ThemeTile;
                        ThemeTile gamesenseTile = GetField(dialog, "gamesenseTile") as ThemeTile;
                        ThemeTile darkTile = GetField(dialog, "darkTile") as ThemeTile;
                        Check("settings theme tiles",
                            autoTile != null && autoTile.ThemeKey == "auto" &&
                            newYearTile != null && newYearTile.ThemeKey == "newyear" &&
                            halloweenTile != null && halloweenTile.ThemeKey == "halloween" &&
                            gtaTile != null && gtaTile.ThemeKey == "gta6" &&
                            gamesenseTile != null && gamesenseTile.ThemeKey == "gamesense" &&
                            darkTile != null && darkTile.Active);

                        newYearTile.Pick();
                        Application.DoEvents();
                        Check("settings choose new year",
                            Ui.ThemeName == "newyear" && newYearTile.Active && !darkTile.Active);

                        string newYearSettings = Path.Combine(work, "ui-settings-newyear.png");
                        SaveShot(dialog, newYearSettings);
                        Check("settings new year screenshot",
                            File.Exists(newYearSettings) && new FileInfo(newYearSettings).Length > 2000);

                        halloweenTile.Pick();
                        Application.DoEvents();
                        string halloweenSettings = Path.Combine(work, "ui-settings-halloween.png");
                        SaveShot(dialog, halloweenSettings);
                        Check("settings halloween screenshot",
                            File.Exists(halloweenSettings) && new FileInfo(halloweenSettings).Length > 2000);

                        gtaTile.Pick();
                        Application.DoEvents();
                        Check("settings choose gta 6",
                            Ui.ThemeName == "gta6" && gtaTile.Active && !halloweenTile.Active);
                        string gtaSettings = Path.Combine(work, "ui-settings-gta6.png");
                        SaveShot(dialog, gtaSettings);
                        Check("settings gta 6 screenshot",
                            File.Exists(gtaSettings) && new FileInfo(gtaSettings).Length > 2000);

                        gamesenseTile.Pick();
                        Application.DoEvents();
                        Check("settings choose gamesense",
                            Ui.ThemeName == "gamesense" && gamesenseTile.Active && !gtaTile.Active);
                        string gsSettings = Path.Combine(work, "ui-settings-gamesense.png");
                        SaveShot(dialog, gsSettings);
                        Check("settings gamesense screenshot",
                            File.Exists(gsSettings) && new FileInfo(gsSettings).Length > 2000);

                        autoTile.Pick();
                        Application.DoEvents();
                        Check("settings choose auto",
                            Ui.AutoTheme && Ui.ThemeName == "halloween" &&
                            autoTile.Active && !halloweenTile.Active);

                        darkTile.Pick();
                        Application.DoEvents();
                        dialog.Close();
                    }
                    Ui.SetDark(true);
                }
                catch (Exception ex)
                {
                    Check("settings screenshot", false);
                    Line("  settings error: " + ex.Message);
                }

                Check("tiktok url detect",
                    TikTok.LooksLikeUrl("https://www.tiktok.com/@user/video/1234567890123456789") &&
                    TikTok.LooksLikeUrl("vt.tiktok.com/ZPabc123/") &&
                    !TikTok.LooksLikeUrl("https://example.com/video/1"));

                try
                {
                    Ui.SetDark(true);
                    using (SnakeForm game = new SnakeForm())
                    {
                        game.StartPosition = FormStartPosition.Manual;
                        game.Location = new Point(-4000, -4000);
                        game.Show();
                        Application.DoEvents();
                        string shot = Path.Combine(work, "ui-snake.png");
                        SaveShot(game, shot);
                        Check("ui snake screenshot", File.Exists(shot) && new FileInfo(shot).Length > 2000);
                        game.Close();
                    }
                }
                catch (Exception ex)
                {
                    Check("ui snake screenshot", false);
                    Line("  snake ui error: " + ex.Message);
                }

                try
                {
                    Ui.SetDark(true);
                    using (EditorForm editor = new EditorForm())
                    {
                        editor.StartPosition = FormStartPosition.Manual;
                        editor.Location = new Point(-4000, -4000);
                        editor.Show();
                        Application.DoEvents();
                        Invoke(editor, "LoadFile", still);
                        Application.DoEvents();
                        Check("editor range slider",
                            GetField(editor, "range") as RangeBar != null);
                        Check("editor trim buttons removed",
                            GetField(editor, "startHereButton") == null &&
                            GetField(editor, "endHereButton") == null &&
                            GetField(editor, "startInput") == null &&
                            GetField(editor, "endInput") == null &&
                            GetField(editor, "seek") == null);
                        string shot = Path.Combine(work, "ui-editor.png");
                        SaveShot(editor, shot);
                        Check("ui editor screenshot",
                            File.Exists(shot) && new FileInfo(shot).Length > 2000);
                        editor.Close();
                    }
                }
                catch (Exception ex)
                {
                    Check("ui editor screenshot", false);
                    Line("  editor ui error: " + ex.Message);
                }

                try
                {
                    Ui.SetDark(true);
                    using (GifToolsForm tools = new GifToolsForm())
                    {
                        tools.StartPosition = FormStartPosition.Manual;
                        tools.Location = new Point(-4000, -4000);
                        tools.Show();
                        Application.DoEvents();
                        string shot = Path.Combine(work, "ui-gif-tools.png");
                        SaveShot(tools, shot);
                        Check("ui gif tools screenshot",
                            File.Exists(shot) && new FileInfo(shot).Length > 2000);
                        tools.Close();
                    }

                    using (OverlayForm overlay = new OverlayForm())
                    {
                        overlay.StartPosition = FormStartPosition.Manual;
                        overlay.Location = new Point(-4000, -4000);
                        overlay.Show();
                        Application.DoEvents();
                        string shot = Path.Combine(work, "ui-overlay.png");
                        SaveShot(overlay, shot);
                        Check("ui overlay screenshot",
                            File.Exists(shot) && new FileInfo(shot).Length > 2000);
                        overlay.Close();
                    }
                }
                catch (Exception ex)
                {
                    Check("ui gif tools screenshot", false);
                    Line("  gif tools ui error: " + ex.Message);
                }

                try
                {
                    Ui.SetDark(true);
                    using (TikTokForm dialog = new TikTokForm())
                    {
                        dialog.StartPosition = FormStartPosition.Manual;
                        dialog.Location = new Point(-4000, -4000);
                        dialog.Show();
                        Application.DoEvents();
                        string shot = Path.Combine(work, "ui-tiktok.png");
                        SaveShot(dialog, shot);
                        Check("ui tiktok screenshot", File.Exists(shot) && new FileInfo(shot).Length > 2000);
                        dialog.Close();
                    }
                }
                catch (Exception ex)
                {
                    Check("ui tiktok screenshot", false);
                    Line("  tiktok ui error: " + ex.Message);
                }

                try
                {
                    Ui.SetDark(true);
                    using (MediaDownloadForm dialog = new MediaDownloadForm(false))
                    {
                        dialog.StartPosition = FormStartPosition.Manual;
                        dialog.Location = new Point(-4000, -4000);
                        dialog.Show();
                        Application.DoEvents();
                        string shot = Path.Combine(work, "ui-download.png");
                        SaveShot(dialog, shot);
                        Check("ui download screenshot", File.Exists(shot) && new FileInfo(shot).Length > 2000);

                        PillButton video = GetField(dialog, "videoPill") as PillButton;
                        PillButton audio = GetField(dialog, "audioPill") as PillButton;
                        if (audio != null) Click(audio);
                        Application.DoEvents();
                        Check("ui download mp3 selected", video != null && !video.Active
                            && audio != null && audio.Active);
                        if (video != null) Click(video);
                        Application.DoEvents();
                        Check("ui download video selected", video != null && video.Active
                            && audio != null && !audio.Active);

                        SlimBar bar = GetField(dialog, "progressBar") as SlimBar;
                        Label stage = GetField(dialog, "stageLabel") as Label;
                        Label percent = GetField(dialog, "percentLabel") as Label;
                        if (bar != null) bar.Visible = true;
                        if (stage != null) stage.Visible = true;
                        if (percent != null) percent.Visible = true;
                        Invoke(dialog, "ReportProgress", 42, "Скачиваем видео…");
                        Application.DoEvents();
                        Check("ui download progress text",
                            bar != null && bar.Value == 42 &&
                            stage != null && stage.Text.Contains("Скачиваем") &&
                            percent != null && percent.Text == "42%");

                        string progressShot = Path.Combine(work, "ui-download-progress.png");
                        SaveShot(dialog, progressShot);
                        Check("ui download progress screenshot",
                            File.Exists(progressShot) && new FileInfo(progressShot).Length > 2000);

                        Label status = GetField(dialog, "statusLabel") as Label;
                        Check("ui download single status", status == null && stage != null);
                        dialog.Close();
                    }

                    using (MediaDownloadForm dialog = new MediaDownloadForm(true))
                    {
                        dialog.StartPosition = FormStartPosition.Manual;
                        dialog.Location = new Point(-4000, -4000);
                        dialog.Show();
                        Application.DoEvents();
                        string shot = Path.Combine(work, "ui-spotify.png");
                        SaveShot(dialog, shot);
                        Check("ui spotify screenshot", File.Exists(shot) && new FileInfo(shot).Length > 2000);

                        PillButton wav = GetField(dialog, "wavPill") as PillButton;
                        PillButton mp3 = GetField(dialog, "audioPill") as PillButton;
                        if (wav != null) Click(wav);
                        Application.DoEvents();
                        string wavShot = Path.Combine(work, "ui-spotify-wav.png");
                        SaveShot(dialog, wavShot);
                        Check("ui spotify wav selected", wav != null && wav.Active
                            && mp3 != null && !mp3.Active
                            && File.Exists(wavShot) && new FileInfo(wavShot).Length > 2000);

                        if (mp3 != null) Click(mp3);
                        Application.DoEvents();
                        Check("ui spotify mp3 selected", wav != null && !wav.Active
                            && mp3 != null && mp3.Active);

                        dialog.Close();
                    }
                }
                catch (Exception ex)
                {
                    Check("ui download screenshot", false);
                    Line("  download ui error: " + ex.Message);
                }

                try
                {
                    string uniqueDir = Path.Combine(work, "unique-check");
                    Directory.CreateDirectory(uniqueDir);
                    File.WriteAllText(Path.Combine(uniqueDir, "clip.mp4"), "x");
                    MethodInfo uniqueFile = typeof(MediaDownloadForm).GetMethod("UniqueFile",
                        BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo uniqueFolder = typeof(MediaDownloadForm).GetMethod("UniqueFolder",
                        BindingFlags.Static | BindingFlags.NonPublic);
                    string fileNext = uniqueFile != null
                        ? (string)uniqueFile.Invoke(null, new object[] { uniqueDir, "clip.mp4" })
                        : null;
                    string folderPath = Path.Combine(uniqueDir, "photos");
                    Directory.CreateDirectory(folderPath);
                    string folderNext = uniqueFolder != null
                        ? (string)uniqueFolder.Invoke(null, new object[] { folderPath })
                        : null;
                    Check("tiktok unique names",
                        fileNext != null && Path.GetFileName(fileNext) == "clip (2).mp4" &&
                        folderNext != null && Path.GetFileName(folderNext) == "photos (2)");
                }
                catch (Exception ex)
                {
                    Check("tiktok unique names", false);
                    Line("  unique name error: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                failures++;
                Line("EXCEPTION: " + ex);
            }

            Line(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");
            try
            {
                File.WriteAllText(logPath, Log.ToString(), Encoding.UTF8);
            }
            catch { }
            code = failures == 0 ? 0 : 2;
            return code;
        }

        private static int RunConversion(VideoInfo info, string input, string output, bool fast, int width = 320)
        {
            ConversionOptions options = new ConversionOptions();
            options.Input = input;
            options.Output = output;
            options.Palette = Path.Combine(Path.GetDirectoryName(output), "palette-" + (fast ? "f" : "s") + ".png");
            options.TempDir = Path.GetDirectoryName(output);
            options.Width = width;
            options.Fps = 30;
            options.Colors = 256;
            options.Dither = "bayer";
            options.Loop = true;
            options.Fast = fast;
            options.Info = info;

            int max = 0;
            Converter converter = new Converter();
            converter.Run(options, delegate(int percent, string stage)
            {
                if (percent > max) max = percent;
            });
            return max;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(target);
        }

        private static void Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null) method.Invoke(target, arguments);
        }

        private static void Click(Control control)
        {
            MethodInfo method = typeof(Control).GetMethod("OnClick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null) method.Invoke(control, new object[] { EventArgs.Empty });
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(target, value);
        }

        private static void WaitUntil(Func<bool> predicate, int timeoutMs)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                Application.DoEvents();
                if (predicate()) return;
                System.Threading.Thread.Sleep(20);
            }
        }

        private static void SaveShot(Form form, string path)
        {
            using (Bitmap bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static bool IsGif(string path)        {
            if (!File.Exists(path)) return false;
            byte[] head = new byte[6];
            using (FileStream fs = File.OpenRead(path))
            {
                if (fs.Read(head, 0, 6) != 6) return false;
            }
            string sig = Encoding.ASCII.GetString(head);
            return sig == "GIF89a" || sig == "GIF87a";
        }

        private static int RunProcess(string exe, string args, out string output)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            StringBuilder sb = new StringBuilder();
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) sb.AppendLine(e.Data);
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) sb.AppendLine(e.Data);
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                output = sb.ToString();
                return p.ExitCode;
            }
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0 ? lines[0].Trim() : "";
        }

        private static void Check(string name, bool ok)
        {
            if (!ok) failures++;
            Line((ok ? "OK   " : "FAIL ") + name);
        }

        private static void Line(string text)
        {
            Log.AppendLine(text);
        }
    }
}
