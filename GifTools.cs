using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Video2Gif
{
    /// <summary>Разбор GIF на кадры и сборка GIF из папки с картинками.</summary>
    internal sealed class GifToolsForm : Form
    {
        private FlatInput gifInput;
        private FlatInput folderInput;
        private FlatInput fpsInput;
        private FlatInput widthInput;
        private Label splitStatus;
        private Label buildStatus;
        private PillButton splitButton;
        private PillButton buildButton;
        private PillButton openFramesButton;
        private PillButton chooseGifButton;
        private PillButton chooseFolderButton;
        private PillButton closeButton;

        private string gifPath = "";
        private string framesFolder = "";
        private string sourceFolder = "";
        private bool working;

        public GifToolsForm()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = "GIF-инструменты — " + Program.AppName;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.P(640), Ui.P(476));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Controls.Add(MakeLabel("GIF-инструменты", Ui.H1, Ui.Text, 24, 16, 420, 30));
            Controls.Add(MakeLabel("Разбить GIF на кадры или собрать его обратно",
                Ui.Small, Ui.Muted, 24, 48, 560, 18));

            Card splitCard = new Card();
            splitCard.SetBounds(Ui.P(24), Ui.P(76), Ui.P(592), Ui.P(160));
            Controls.Add(splitCard);
            splitCard.Controls.Add(MakeLabel("Разбить GIF на кадры", Ui.H2, Ui.Text, 18, 12, 400, 24));

            gifInput = new FlatInput();
            gifInput.SetBounds(Ui.P(18), Ui.P(44), Ui.P(400), Ui.P(36));
            gifInput.Box.ReadOnly = true;
            gifInput.Placeholder = "GIF-файл";
            splitCard.Controls.Add(gifInput);

            chooseGifButton = new PillButton();
            chooseGifButton.Ghost = true;
            chooseGifButton.Text = "Выбрать GIF";
            chooseGifButton.Font = Ui.Small;
            chooseGifButton.SetBounds(Ui.P(426), Ui.P(44), Ui.P(148), Ui.P(36));
            chooseGifButton.Click += delegate { ChooseGif(); };
            splitCard.Controls.Add(chooseGifButton);

            splitButton = new PillButton();
            splitButton.Accent = true;
            splitButton.Text = "Разбить на кадры";
            splitButton.Font = Ui.Small;
            splitButton.SetBounds(Ui.P(18), Ui.P(92), Ui.P(180), Ui.P(36));
            splitButton.Click += delegate { SplitGif(); };
            splitCard.Controls.Add(splitButton);

            openFramesButton = new PillButton();
            openFramesButton.Ghost = true;
            openFramesButton.Text = "Открыть папку";
            openFramesButton.Font = Ui.Small;
            openFramesButton.SetBounds(Ui.P(206), Ui.P(92), Ui.P(150), Ui.P(36));
            openFramesButton.Enabled = false;
            openFramesButton.Click += delegate { OpenFolder(framesFolder); };
            splitCard.Controls.Add(openFramesButton);

            splitStatus = MakeLabel("", Ui.Tiny, Ui.Faint, 366, 92, 208, 36);
            splitCard.Controls.Add(splitStatus);

            Card buildCard = new Card();
            buildCard.SetBounds(Ui.P(24), Ui.P(248), Ui.P(592), Ui.P(170));
            Controls.Add(buildCard);
            buildCard.Controls.Add(MakeLabel("Собрать GIF из кадров", Ui.H2, Ui.Text, 18, 12, 400, 24));

            folderInput = new FlatInput();
            folderInput.SetBounds(Ui.P(18), Ui.P(44), Ui.P(400), Ui.P(36));
            folderInput.Box.ReadOnly = true;
            folderInput.Placeholder = "Папка с картинками";
            buildCard.Controls.Add(folderInput);

            chooseFolderButton = new PillButton();
            chooseFolderButton.Ghost = true;
            chooseFolderButton.Text = "Выбрать папку";
            chooseFolderButton.Font = Ui.Small;
            chooseFolderButton.SetBounds(Ui.P(426), Ui.P(44), Ui.P(148), Ui.P(36));
            chooseFolderButton.Click += delegate { ChooseFolder(); };
            buildCard.Controls.Add(chooseFolderButton);

            buildCard.Controls.Add(MakeLabel("Кадров/с", Ui.Small, Ui.Muted, 18, 86, 100, 18));
            buildCard.Controls.Add(MakeLabel("Ширина, px (0=ориг.)", Ui.Small, Ui.Muted, 126, 86, 140, 18));

            fpsInput = new FlatInput();
            fpsInput.SetBounds(Ui.P(18), Ui.P(104), Ui.P(100), Ui.P(34));
            fpsInput.Value = "30";
            buildCard.Controls.Add(fpsInput);

            widthInput = new FlatInput();
            widthInput.SetBounds(Ui.P(126), Ui.P(104), Ui.P(120), Ui.P(34));
            widthInput.Value = "480";
            buildCard.Controls.Add(widthInput);

            buildButton = new PillButton();
            buildButton.Accent = true;
            buildButton.Text = "Собрать GIF";
            buildButton.Font = Ui.Small;
            buildButton.SetBounds(Ui.P(262), Ui.P(100), Ui.P(180), Ui.P(38));
            buildButton.Click += delegate { BuildGif(); };
            buildCard.Controls.Add(buildButton);

            buildStatus = MakeLabel("PNG/JPG/BMP, по порядку имён", Ui.Tiny, Ui.Faint,
                452, 100, 122, 38);
            buildCard.Controls.Add(buildStatus);

            closeButton = new PillButton();
            closeButton.Ghost = true;
            closeButton.Text = "Закрыть";
            closeButton.SetBounds(Ui.P(516), Ui.P(432), Ui.P(100), Ui.P(36));
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);

            ResumeLayout(false);
        }

        private static Label MakeLabel(string text, Font font, Color color,
            int x, int y, int w, int h)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.UseMnemonic = false;
            label.Tag = Ui.RoleOf(color);
            label.SetBounds(Ui.P(x), Ui.P(y), Ui.P(w), Ui.P(h));
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.ApplyTitleBar(Handle);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Ui.Gamesense) Ui.DrawCarbon(e.Graphics, ClientRectangle);
        }

        private void ChooseGif()
        {
            if (working) return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите GIF";
                dialog.Filter = "GIF|*.gif|Все файлы|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    gifPath = dialog.FileName;
                    gifInput.Value = Path.GetFileName(gifPath);
                    splitStatus.Text = "";
                }
            }
        }

        private void ChooseFolder()
        {
            if (working) return;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Папка с кадрами (PNG/JPG)";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    sourceFolder = dialog.SelectedPath;
                    folderInput.Value = sourceFolder;
                    buildStatus.Text = "";
                }
            }
        }

        private void SplitGif()
        {
            if (working) return;
            if (gifPath.Length == 0 || !File.Exists(gifPath))
            {
                splitStatus.Text = "Сначала выберите GIF.";
                return;
            }

            string target = Path.Combine(Path.GetDirectoryName(gifPath),
                Path.GetFileNameWithoutExtension(gifPath) + "_frames");
            int suffix = 2;
            while (Directory.Exists(target))
            {
                target = Path.Combine(Path.GetDirectoryName(gifPath),
                    Path.GetFileNameWithoutExtension(gifPath) + "_frames_" + suffix);
                suffix++;
            }

            SetBusy(true, "Разбиваем GIF на кадры…");
            string source = gifPath;
            string folder = target;
            Task.Factory.StartNew(delegate
            {
                return EditorCore.ExtractGifFrames(source, folder);
            }).ContinueWith(delegate(Task<int> task)
            {
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        SetBusy(false, "");
                        if (task.IsFaulted)
                        {
                            string message = task.Exception != null && task.Exception.InnerException != null
                                ? task.Exception.InnerException.Message
                                : "Не удалось разбить GIF.";
                            splitStatus.Text = message;
                            return;
                        }
                        framesFolder = folder;
                        openFramesButton.Enabled = true;
                        splitStatus.Text = "Готово: " + task.Result + " кадров";
                    });
                }
                catch { }
            });
        }

        private void BuildGif()
        {
            if (working) return;
            if (sourceFolder.Length == 0 || !Directory.Exists(sourceFolder))
            {
                buildStatus.Text = "Выберите папку с кадрами.";
                return;
            }

            double fpsValue;
            if (!Fmt.TryNum(fpsInput.Value.Trim(), out fpsValue) ||
                fpsValue < 1 || fpsValue > 60)
            {
                buildStatus.Text = "Кадров/с — от 1 до 60.";
                return;
            }
            int fps = (int)Math.Round(fpsValue);

            double widthValue;
            string widthText = widthInput.Value.Trim();
            if (widthText.Length == 0) widthValue = 0;
            else if (!Fmt.TryNum(widthText, out widthValue) || widthValue < 0 || widthValue > 1920)
            {
                buildStatus.Text = "Ширина — от 0 до 1920.";
                return;
            }

            List<string> images = new List<string>();
            string[] patterns = new string[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" };
            foreach (string pattern in patterns)
            {
                images.AddRange(Directory.GetFiles(sourceFolder, pattern));
            }
            images.Sort(StringComparer.OrdinalIgnoreCase);
            if (images.Count == 0)
            {
                buildStatus.Text = "В папке нет PNG/JPG/BMP.";
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Сохранить GIF";
                dialog.Filter = "GIF|*.gif";
                dialog.InitialDirectory = SavePaths.Desktop();
                dialog.FileName = new DirectoryInfo(sourceFolder).Name + ".gif";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string output = dialog.FileName;
                if (!output.ToLowerInvariant().EndsWith(".gif")) output += ".gif";
                string source = sourceFolder;
                string temp = Path.Combine(Path.GetTempPath(), "Video2Gif",
                    "gif-build-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                int targetWidth = (int)Math.Round(widthValue);

                SetBusy(true, "Собираем GIF…");
                Task.Factory.StartNew(delegate
                {
                    Directory.CreateDirectory(temp);
                    int index = 0;
                    for (int i = 0; i < images.Count; i++)
                    {
                        using (Bitmap bitmap = EditorCore.LoadBitmap(images[i]))
                        {
                            index++;
                            bitmap.Save(Path.Combine(temp,
                                "f" + index.ToString("0000") + ".png"), ImageFormat.Png);
                        }
                    }
                    EditorCore.AssembleGif(temp, fps, targetWidth, output);
                    try { Directory.Delete(temp, true); } catch { }
                }).ContinueWith(delegate(Task task)
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            SetBusy(false, "");
                            if (task.IsFaulted)
                            {
                                string message = task.Exception != null && task.Exception.InnerException != null
                                    ? task.Exception.InnerException.Message
                                    : "Не удалось собрать GIF.";
                                buildStatus.Text = message;
                                return;
                            }
                            buildStatus.Text = "Готово: " + Path.GetFileName(output);
                            try
                            {
                                Directory.Delete(temp, true);
                            }
                            catch { }
                        });
                    }
                    catch { }
                });
            }
        }

        private void SetBusy(bool busy, string status)
        {
            working = busy;
            chooseGifButton.Enabled = !busy;
            chooseFolderButton.Enabled = !busy;
            splitButton.Enabled = !busy;
            buildButton.Enabled = !busy;
            openFramesButton.Enabled = !busy && framesFolder.Length > 0;
            fpsInput.Enabled = !busy;
            widthInput.Enabled = !busy;
            UseWaitCursor = busy;
            if (status.Length > 0) buildStatus.Text = status;
        }

        private void OpenFolder(string folder)
        {
            if (folder.Length == 0 || !Directory.Exists(folder)) return;
            try { System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\""); }
            catch { }
        }
    }
}
