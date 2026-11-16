using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Video2Gif
{
    internal sealed partial class MainForm : Form
    {
        private bool ffmpegReady;
        private bool busy;
        private string sourcePath;
        private VideoInfo info;
        private string resultPath;
        private string tempRoot;
        private readonly System.Collections.Generic.List<string> tempDirs =
            new System.Collections.Generic.List<string>();
        private Converter converter;
        private MemoryStream previewStream;
        private int lastReportTick;
        private System.Windows.Forms.Timer themeTimer;
        private System.Windows.Forms.Timer shimmerTimer;
        private float shimmerPhase;

        public MainForm()
        {
            string setting = LoadThemeSetting();
            if (Ui.Gta6Window(DateTime.Now)) Ui.SetTheme(ThemeKind.Gta6);
            else if (setting.Length == 0 || setting == "auto") Ui.SetThemeAuto(DateTime.Now);
            else Ui.SetTheme(setting);
            BuildUi();
            EnableDrop(this);
            FormClosing += delegate { Shutdown(); };
            UpdateActions();

            // Если программа открыта в ночь смены сезона или в окне GTA 6,
            // тема переключится сама — без перезапуска.
            themeTimer = new System.Windows.Forms.Timer();
            themeTimer.Interval = 60000;
            themeTimer.Tick += delegate { RefreshScheduledTheme(); };
            themeTimer.Start();

            // Переливающаяся полоска в теме Gamesense.
            shimmerTimer = new System.Windows.Forms.Timer();
            shimmerTimer.Interval = 40;
            shimmerTimer.Tick += delegate { ShimmerTick(); };
            UpdateShimmer();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTitleBarTheme();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            Graphics g = e.Graphics;

            if (Ui.Gta6)
            {
                Rectangle glow = new Rectangle(0, 0, ClientSize.Width, Ui.P(66));
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    glow,
                    Color.FromArgb(0, 255, 79, 163),
                    Color.FromArgb(28, 255, 79, 163),
                    0f))
                {
                    ColorBlend blend = new ColorBlend(3);
                    blend.Colors = new Color[]
                    {
                        Color.FromArgb(0, 96, 92, 240),
                        Color.FromArgb(16, 255, 79, 163),
                        Color.FromArgb(30, 255, 158, 66)
                    };
                    blend.Positions = new float[] { 0f, 0.55f, 1f };
                    brush.InterpolationColors = blend;
                    g.FillRectangle(brush, glow);
                }

                Rectangle line = new Rectangle(0, 0, ClientSize.Width, Math.Max(2, Ui.P(2)));
                using (LinearGradientBrush brush = Ui.GtaGradient(line, 0f))
                {
                    g.FillRectangle(brush, line);
                }
            }

            if (Ui.Gamesense)
            {
                Rectangle strip = new Rectangle(0, 0, ClientSize.Width,
                    Math.Max(3, Ui.P(3)));
                using (LinearGradientBrush brush = Ui.RainbowBrush(strip, shimmerPhase, 10))
                {
                    g.FillRectangle(brush, strip);
                }
            }

            if (Ui.Holiday) DrawHolidayDecorations(g);
        }

        private void ShimmerTick()
        {
            if (!Ui.Gamesense)
            {
                shimmerTimer.Stop();
                return;
            }
            shimmerPhase += 0.015f;
            if (shimmerPhase >= 1f) shimmerPhase -= 1f;
            Invalidate(new Rectangle(0, 0, ClientSize.Width,
                Math.Max(6, Ui.P(8))));
        }

        private void UpdateShimmer()
        {
            if (shimmerTimer == null) return;
            if (Ui.Gamesense)
            {
                if (!shimmerTimer.Enabled) shimmerTimer.Start();
            }
            else
            {
                shimmerTimer.Stop();
            }
            Invalidate(new Rectangle(0, 0, ClientSize.Width,
                Math.Max(6, Ui.P(8))));
        }

        /// <summary>Сезонные украшения в свободной части шапки.</summary>
        private void DrawHolidayDecorations(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (Ui.NewYear)
            {
                Ui.DrawSnowflake(g, Ui.P(386), Ui.P(20), Ui.P(12),
                    Color.FromArgb(150, Ui.Accent), Math.Max(1.3f, Ui.P(1.6)));
                Ui.DrawSnowflake(g, Ui.P(432), Ui.P(50), Ui.P(16),
                    Color.FromArgb(160, Ui.Accent), Math.Max(1.4f, Ui.P(1.7)));
                Ui.DrawSnowflake(g, Ui.P(480), Ui.P(16), Ui.P(9),
                    Color.FromArgb(130, Ui.Accent), Math.Max(1.1f, Ui.P(1.3)));
                Ui.DrawSnowflake(g, Ui.P(530), Ui.P(54), Ui.P(12),
                    Color.FromArgb(140, Ui.Accent), Math.Max(1.2f, Ui.P(1.4)));
                Ui.DrawSnowflake(g, Ui.P(580), Ui.P(20), Ui.P(10),
                    Color.FromArgb(125, Ui.Accent), Math.Max(1.1f, Ui.P(1.3)));
                Ui.DrawSnowflake(g, Ui.P(630), Ui.P(50), Ui.P(13),
                    Color.FromArgb(145, Ui.Accent), Math.Max(1.2f, Ui.P(1.5)));
                Ui.DrawSnowflake(g, Ui.P(672), Ui.P(14), Ui.P(8),
                    Color.FromArgb(115, Ui.Accent), Math.Max(1f, Ui.P(1.2)));
                return;
            }

            Rectangle pumpkin = new Rectangle(
                Ui.P(392), Ui.P(6), Ui.P(34), Ui.P(30));
            Ui.DrawPumpkin(g, pumpkin,
                Color.FromArgb(170, Ui.Accent), Ui.Bg);
            Ui.DrawLeaf(g, Ui.P(442), Ui.P(46), Ui.P(18),
                Color.FromArgb(165, Ui.Accent), -28f);
            Ui.DrawLeaf(g, Ui.P(492), Ui.P(14), Ui.P(13),
                Color.FromArgb(145, Ui.Accent), 40f);
            Ui.DrawLeaf(g, Ui.P(542), Ui.P(48), Ui.P(15),
                Color.FromArgb(155, Ui.Accent), 18f);
            Ui.DrawLeaf(g, Ui.P(592), Ui.P(18), Ui.P(11),
                Color.FromArgb(135, Ui.Accent), 56f);
            Ui.DrawLeaf(g, Ui.P(642), Ui.P(46), Ui.P(13),
                Color.FromArgb(150, Ui.Accent), -64f);
            Ui.DrawLeaf(g, Ui.P(678), Ui.P(16), Ui.P(9),
                Color.FromArgb(120, Ui.Accent), 20f);
            Ui.DrawLeaf(g, Ui.P(366), Ui.P(52), Ui.P(10),
                Color.FromArgb(120, Ui.Accent), 60f);
        }

        /* --------------------------------------------------------------- Тема */

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Video2Gif", "theme.txt");
            }
        }

        private static string LoadThemeSetting()
        {
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path)) return "auto";
                string setting = File.ReadAllText(path).Trim().ToLowerInvariant();
                return setting.Length == 0 ? "auto" : setting;
            }
            catch { return "auto"; }
        }

        private void SaveTheme()
        {
            try
            {
                string path = SettingsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Ui.AutoTheme ? "auto" : Ui.ThemeName);
            }
            catch { }
        }

        private void RefreshScheduledTheme()
        {
            DateTime now = DateTime.Now;
            string setting = LoadThemeSetting();
            bool savedGta6 =
                setting == "gta6" || setting == "gta" || setting == "gta-6";

            if (Ui.Gta6Window(now))
            {
                if (!Ui.Gta6)
                {
                    Ui.SetTheme(ThemeKind.Gta6);
                    ApplyThemeNow();
                }
                return;
            }

            if (Ui.Gta6 && !savedGta6)
            {
                // Праздничное окно закончилось — возвращаем сохранённую тему.
                if (setting.Length == 0 || setting == "auto") Ui.SetThemeAuto(now);
                else Ui.SetTheme(setting);
                ApplyThemeNow();
                return;
            }

            if (Ui.RefreshAutoTheme(now)) ApplyThemeNow();
        }

        private void ApplyThemeNow()
        {
            Ui.ApplyTheme(this);
            ApplyTitleBarTheme();
            UpdateShimmer();
            Invalidate(true);
        }

        private void ApplyThemeChoice(string name)
        {
            if (name == "auto") Ui.SetThemeAuto(DateTime.Now);
            else Ui.SetTheme(name);
            SaveTheme();
            ApplyThemeNow();
        }

        private void OpenSettings()
        {
            using (SettingsForm dialog = new SettingsForm(ApplyThemeChoice))
            {
                dialog.ShowDialog(this);
            }
            ApplyThemeNow();
        }

        private void ApplyTitleBarTheme()
        {
            if (!IsHandleCreated) return;
            Ui.ApplyTitleBar(Handle);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            string[] args = Environment.GetCommandLineArgs();
            PrepareFfmpeg();
            if (args.Length > 1 && File.Exists(args[1]))
            {
                LoadVideo(args[1]);
            }
            if (!Program.SelfTestMode) CheckUpdatesOnStart();
        }

        /* ------------------------------------------------------------ Обновления */

        private static string SeenUpdatePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Video2Gif", "update.txt");
            }
        }

        private void CheckUpdatesOnStart()
        {
            Task.Factory.StartNew(delegate
            {
                try { return Updater.Check(); }
                catch { return null; }
            }).ContinueWith(delegate(Task<UpdateInfo> task)
            {
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate { OfferUpdate(task.Result); });
                }
                catch { }
            });
        }

        private void OfferUpdate(UpdateInfo info)
        {
            if (info == null || !info.HasUpdate) return;

            try
            {
                string seen = SeenUpdatePath;
                if (File.Exists(seen) && File.ReadAllText(seen).Trim() == info.Version) return;
                Directory.CreateDirectory(Path.GetDirectoryName(seen));
                File.WriteAllText(seen, info.Version);
            }
            catch { }

            DialogResult answer = MessageBox.Show(this,
                "Доступна новая версия " + info.Version + " (у вас " + Program.Version + ").\n" +
                "Скачать и установить сейчас?",
                Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;

            using (UpdateForm dialog = new UpdateForm(info))
            {
                dialog.ShowDialog(this);
            }
        }

        /* ------------------------------------------------------------ FFmpeg */

        private void PrepareFfmpeg()
        {
            convertButton.Text = "Подготовка…";
            convertButton.Enabled = false;
            Task.Factory.StartNew(delegate { FfmpegBundle.Ensure(); })
                .ContinueWith(delegate(Task t)
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            convertButton.Text = "Сделать GIF";
                            if (t.IsFaulted)
                            {
                                string msg = t.Exception != null && t.Exception.InnerException != null
                                    ? t.Exception.InnerException.Message
                                    : "неизвестная ошибка";
                                MessageBox.Show(this, "Не удалось подготовить FFmpeg.\n\n" + msg,
                                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                            {
                                ffmpegReady = true;
                            }
                            UpdateActions();
                        });
                    }
                    catch { }
                });
        }

        /* -------------------------------------------------------------- Файл */

        private void PickFile()
        {
            if (busy) return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите видео или картинку";
                dialog.Filter = "Видео, картинки и GIF|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.wmv;*.flv;*.mpg;*.mpeg;*.ts;*.mts;*.m2ts;*.3gp;*.ogv;*.gif;*.png;*.jpg;*.jpeg;*.jfif;*.webp;*.bmp;*.tif;*.tiff;*.tga;*.avif|Все файлы|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadVideo(dialog.FileName);
                }
            }
        }

        private void OpenGame()
        {
            if (busy) return;
            using (SnakeForm game = new SnakeForm())
            {
                game.ShowDialog(this);
            }
        }

        private void OpenEditor()
        {
            if (busy) return;
            using (EditorForm editor = new EditorForm())
            {
                editor.ShowDialog(this);
            }
        }

        private void LoadVideo(string path)
        {
            if (busy) return;
            FileInfo fi = new FileInfo(path);
            if (!fi.Exists)
            {
                MessageBox.Show(this, "Файл не найден.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            sourcePath = path;
            info = null;
            drop.Selected = true;
            drop.FileName = fi.Name;
            drop.FileMeta = "Читаем файл…  " + Fmt.Size(fi.Length);
            drop.BusyText = "";
            drop.Invalidate();
            UpdateActions();

            string captured = path;
            Task.Factory.StartNew(delegate { return Ffmpeg.Probe(captured); })
                .ContinueWith(delegate(Task<VideoInfo> t)
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { FinishProbe(captured, t); });
                    }
                    catch { }
                });
        }

        private void FinishProbe(string path, Task<VideoInfo> task)
        {
            if (path != sourcePath) return;
            if (task.IsFaulted)
            {
                info = null;
                startInput.Enabled = true;
                durationInput.Enabled = true;
                fpsInput.Enabled = true;
                startInput.Visible = true;
                durationInput.Visible = true;
                fpsInput.Visible = true;
                startLabel.Visible = true;
                durationLabel.Visible = true;
                fpsLabel.Visible = true;
                imageNote.Visible = false;
                widthLabel.SetBounds(Ui.P(282), Ui.P(54), Ui.P(128), Ui.P(18));
                widthInput.SetBounds(Ui.P(282), Ui.P(72), Ui.P(128), Ui.P(34));
                durationInput.Placeholder = "0 — весь клип";
                drop.FileMeta = "Не удалось прочитать файл";
                drop.Invalidate();
                UpdateActions();
                Exception inner = task.Exception != null ? task.Exception.InnerException : null;
                MessageBox.Show(this,
                    inner != null ? inner.Message : "Не удалось прочитать файл.",
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            info = task.Result;
            UpdateFileMeta();
            bool image = info.IsImage;
            startInput.Enabled = !image;
            durationInput.Enabled = !image;
            fpsInput.Enabled = !image;
            startInput.Visible = !image;
            durationInput.Visible = !image;
            fpsInput.Visible = !image;
            startLabel.Visible = !image;
            durationLabel.Visible = !image;
            fpsLabel.Visible = !image;
            imageNote.Visible = image;
            widthLabel.SetBounds(Ui.P(282), Ui.P(54), Ui.P(image ? 260 : 128), Ui.P(18));
            widthInput.SetBounds(Ui.P(282), Ui.P(72), Ui.P(image ? 260 : 128), Ui.P(34));
            startInput.Value = image ? "" : "0";
            durationInput.Placeholder = image ? "не используется" : "0 — весь клип";
            durationInput.Value = image ? "" : InputNumber(Math.Min(8.0, info.Duration > 0 ? info.Duration : 8.0));
            UpdateActions();
        }

        private void UpdateFileMeta()
        {
            if (info == null || sourcePath == null) return;
            long bytes = 0;
            try { bytes = new FileInfo(sourcePath).Length; } catch { }
            if (info.IsImage)
            {
                drop.FileMeta = "КАРТИНКА · " + info.Width + "×" + info.Height +
                    (bytes > 0 ? " · " + Fmt.Size(bytes) : "");
            }
            else
            {
                drop.FileMeta = info.Codec.ToUpperInvariant() + " · " + info.Describe() +
                    (bytes > 0 ? " · " + Fmt.Size(bytes) : "");
            }
            drop.Invalidate();
        }

        private static string InputNumber(double value)
        {
            return value.ToString("0.#").Replace(',', '.');
        }

        private void UpdateActions()
        {
            convertButton.Enabled = ffmpegReady && info != null && !busy;
            cancelButton.Visible = busy;
            ditherBayer.Enabled = !busy;
            ditherSierra.Enabled = !busy;
            ditherNone.Enabled = !busy;
            downloadsButton.Enabled = !busy;
            if (gameButton != null) gameButton.Enabled = !busy;
            if (editorButton != null) editorButton.Enabled = !busy;
        }

        /* ------------------------------------------------------- Конвертация */

        private void StartConversion()
        {
            if (busy) return;
            if (!ffmpegReady)
            {
                MessageBox.Show(this, "FFmpeg ещё готовится, подождите пару секунд.",
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (info == null || sourcePath == null)
            {
                MessageBox.Show(this, "Сначала выберите видео или картинку.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool imageInput = info.IsImage;

            double start = 0;
            double duration = 0;
            double effective = 0;
            if (!imageInput)
            {
                string startText = startInput.Value.Trim();
                if (startText.Length == 0) start = 0;
                else if (!Fmt.TryNum(startText, out start) || start < 0)
                {
                    ShowFieldError("Начало должно быть числом от 0 секунд.", startInput);
                    return;
                }

                string durationText = durationInput.Value.Trim();
                if (!Fmt.TryDuration(durationText, out duration))
                {
                    ShowFieldError("Длительность — число секунд от 0. 0 или пусто — весь клип.",
                        durationInput);
                    return;
                }

                if (info.Duration > 0 && start >= info.Duration - 0.05)
                {
                    ShowFieldError("Начало совпадает с концом видео.", startInput);
                    return;
                }

                double available = info.Duration > 0 ? Math.Max(0, info.Duration - start) : -1;
                effective = duration > 0
                    ? (available > 0 ? Math.Min(duration, available) : duration)
                    : (available > 0 ? available : 0);
                if (effective > 0 && effective < 0.1)
                {
                    MessageBox.Show(this, "Слишком короткий фрагмент — выберите другую длительность.",
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            double widthValue;
            string widthText = widthInput.Value.Trim();
            if (widthText.Length == 0) widthValue = 0;
            else if (!Fmt.TryNum(widthText, out widthValue) || widthValue < 0 || widthValue > 1920)
            {
                ShowFieldError("Ширина должна быть от 0 до 1920 пикселей.", widthInput);
                return;
            }

            double fpsValue = 30;
            if (!imageInput)
            {
                if (!Fmt.TryNum(fpsInput.Value.Trim(), out fpsValue) || fpsValue < 1 || fpsValue > 60)
                {
                    ShowFieldError("Кадров в секунду — от 1 до 60.", fpsInput);
                    return;
                }
            }

            tempRoot = Path.Combine(Path.GetTempPath(),
                "video2gif-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(tempRoot);
                tempDirs.Add(tempRoot);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось создать временную папку.\n\n" + ex.Message,
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ConversionOptions options = new ConversionOptions();
            options.Input = sourcePath;
            options.Output = Path.Combine(tempRoot, "result.gif");
            options.Palette = Path.Combine(tempRoot, "palette.png");
            options.TempDir = tempRoot;
            options.Start = imageInput ? 0 : start;
            options.Duration = imageInput ? 0 : (duration > 0 ? effective : 0);
            options.Width = (int)Math.Round(widthValue);
            options.Fps = (int)Math.Round(fpsValue);
            options.Colors = 256;
            options.Dither = DitherName(ditherIndex);
            options.Loop = loopToggle.Checked;
            options.Fast = fastToggle.Checked;
            options.Info = info;

            busy = true;
            converter = new Converter();
            UpdateActions();
            string detail;
            if (imageInput)
            {
                int shownWidth = options.Width > 0 && info.Width > 0
                    ? Math.Min(options.Width, info.Width)
                    : (info.Width > 0 ? info.Width : options.Width);
                detail = "картинка · " + (shownWidth > 0 ? shownWidth + " px" : "оригинал");
            }
            else
            {
                detail = (effective > 0 ? Fmt.Time(effective) : "весь клип") + " · " +
                    options.Fps + " fps · " +
                    (options.Width > 0 ? options.Width.ToString() + " px" : "оригинал");
            }
            ShowProgress("Готовимся…", detail);

            Task.Factory.StartNew(delegate
            {
                try
                {
                    converter.Run(options, ReportProgress);
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { FinishSuccess(options.Output); });
                    }
                    catch { }
                }
                catch (ConversionCancelledException)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { FinishFailure("Конвертация отменена.", false); });
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    string message = ex.Message;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { FinishFailure(message, true); });
                    }
                    catch { }
                }
            });
        }

        private void ReportProgress(int percent, string stage)
        {
            int now = Environment.TickCount;
            if (percent < 100 && now - lastReportTick < 110) return;
            if (percent < 100) lastReportTick = now;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!busy) return;
                    progressBar.Value = percent;
                    percentLabel.Text = percent + "%";
                    stageLabel.Text = stage;
                });
            }
            catch { }
        }

        private void ShowProgress(string stage, string sub)
        {
            lastReportTick = 0;
            progressBar.Value = 0;
            percentLabel.Text = "0%";
            stageLabel.Text = stage;
            progressSub.Text = sub;
            emptyState.Visible = false;
            resultView.Visible = false;
            progressView.Visible = true;
            cancelButton.Enabled = true;
        }

        private void FinishSuccess(string output)
        {
            busy = false;
            converter = null;
            UpdateActions();

            Image image;
            try
            {
                preview.Image = null;
                if (previewStream != null) previewStream.Dispose();
                previewStream = new MemoryStream(File.ReadAllBytes(output));
                image = Image.FromStream(previewStream);
            }
            catch (Exception ex)
            {
                FinishFailure("Не удалось открыть готовый GIF: " + ex.Message, true);
                return;
            }

            preview.Image = image;
            resultPath = output;
            resultMeta.Text = Fmt.Size(new FileInfo(output).Length) + "   ·   " +
                image.Width + "×" + image.Height;
            progressView.Visible = false;
            emptyState.Visible = false;
            resultView.Visible = true;
            saveButton.Text = "Сохранить GIF";
        }

        private void FinishFailure(string message, bool showDialog)
        {
            busy = false;
            converter = null;
            UpdateActions();
            progressView.Visible = false;
            bool hasResult = resultPath != null && File.Exists(resultPath);
            resultView.Visible = hasResult;
            emptyState.Visible = !hasResult;
            DropCurrentTemp();

            if (showDialog)
            {
                MessageBox.Show(this, message, Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CancelConversion()
        {
            if (!busy || converter == null) return;
            cancelButton.Enabled = false;
            stageLabel.Text = "Останавливаем…";
            progressSub.Text = "";
            converter.Cancel();
        }

        private void ShowFieldError(string message, Control field)
        {
            MessageBox.Show(this, message, Program.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (field != null) field.Focus();
        }

        private static string DitherName(int index)
        {
            if (index == 1) return "sierra2_4a";
            if (index == 2) return "none";
            return "bayer";
        }

        private void SetDither(int index)
        {
            ditherIndex = index;
            ditherBayer.Active = index == 0;
            ditherSierra.Active = index == 1;
            ditherNone.Active = index == 2;
            ditherBayer.Invalidate();
            ditherSierra.Invalidate();
            ditherNone.Invalidate();
        }

        /* ---------------------------------------------------------- Результат */

        private void SaveResult()
        {
            if (resultPath == null || !File.Exists(resultPath)) return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Сохранить GIF";
                dialog.Filter = "GIF-файл|*.gif";
                dialog.DefaultExt = "gif";
                dialog.AddExtension = true;
                dialog.FileName = SafeName(sourcePath) + ".gif";
                try
                {
                    string dir = SavePaths.Desktop();
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dialog.InitialDirectory = dir;
                }
                catch { }

                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.Copy(resultPath, dialog.FileName, true);
                    saveButton.Text = "Сохранено";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось сохранить файл.\n\n" + ex.Message,
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string SafeName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "video";
            string name = Path.GetFileNameWithoutExtension(path);
            return string.IsNullOrEmpty(name) ? "video" : name;
        }

        private void OpenResult()
        {
            if (resultPath == null || !File.Exists(resultPath)) return;
            try
            {
                Process.Start(resultPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось открыть файл.\n\n" + ex.Message,
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /* ------------------------------------------------------- Перетаскивание */

        private void EnableDrop(Control root)
        {
            root.AllowDrop = true;
            root.DragEnter += OnDragEnter;
            root.DragOver += OnDragOver;
            root.DragLeave += OnDragLeave;
            root.DragDrop += OnDragDrop;

            foreach (Control child in root.Controls)
            {
                if (child is TextBoxBase || child is ComboBox) continue;
                EnableDrop(child);
            }
        }

        private static string FirstFile(IDataObject data)
        {
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop)) return null;
            string[] files = data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return null;
            return files[0];
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            string file = busy ? null : FirstFile(e.Data);
            if (file != null)
            {
                e.Effect = DragDropEffects.Copy;
                drop.Highlight = true;
                drop.Invalidate();
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effect = !busy && FirstFile(e.Data) != null
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnDragLeave(object sender, EventArgs e)
        {
            drop.Highlight = false;
            drop.Invalidate();
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            drop.Highlight = false;
            drop.Invalidate();
            if (busy) return;
            string file = FirstFile(e.Data);
            if (file != null) LoadVideo(file);
        }

        /* ------------------------------------------------------------- Очистка */

        private void Shutdown()
        {
            if (shimmerTimer != null)
            {
                shimmerTimer.Stop();
                shimmerTimer.Dispose();
                shimmerTimer = null;
            }
            if (themeTimer != null)
            {
                themeTimer.Stop();
                themeTimer.Dispose();
                themeTimer = null;
            }
            if (converter != null) converter.Cancel();
            if (preview != null)
            {
                preview.Image = null;
            }
            if (previewStream != null)
            {
                previewStream.Dispose();
                previewStream = null;
            }
            CleanupTemp();
        }

        private void CleanupTemp()
        {
            for (int i = 0; i < tempDirs.Count; i++)
            {
                try
                {
                    if (Directory.Exists(tempDirs[i])) Directory.Delete(tempDirs[i], true);
                }
                catch { }
            }
            tempDirs.Clear();
            tempRoot = null;
        }

        private void DropCurrentTemp()
        {
            string dir = tempRoot;
            tempRoot = null;
            if (string.IsNullOrEmpty(dir)) return;
            tempDirs.Remove(dir);
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch { }
        }
    }
}
