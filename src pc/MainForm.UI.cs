using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Video2Gif
{
    internal sealed partial class MainForm
    {
        private DropCard drop;
        private FlatInput startInput;
        private FlatInput durationInput;
        private Label startLabel;
        private Label durationLabel;
        private Label widthLabel;
        private Label fpsLabel;
        private Label imageNote;
        private FlatInput widthInput;
        private FlatInput fpsInput;
        private FlatInput limitInput;
        private Label limitLabel;
        private PillButton ditherBayer;
        private PillButton ditherSierra;
        private PillButton ditherNone;
        private int ditherIndex;
        private ToggleRow loopToggle;
        private ToggleRow fastToggle;
        private PillButton convertButton;
        private PillButton cancelButton;
        private IconButton downloadsButton;
        private IconButton settingsButton;
        private IconButton gameButton;
        private IconButton editorButton;
        private IconButton gifToolsButton;
        private IconButton discordButton;
        private IconButton donateButton;
        private IconButton cutoutButton;
        private InfoBadge infoBadge;
        private ToolTip headerTip;

        private Panel progressView;
        private Label percentLabel;
        private Label stageLabel;
        private SlimBar progressBar;
        private Label progressSub;

        private Panel resultView;
        private Panel previewHost;
        private PictureBox preview;
        private Label resultMeta;
        private PillButton saveButton;
        private PillButton copyButton;
        private PillButton openButton;

        private EmptyState emptyState;

        private static Label Lbl(string text, Font font, Color color, int x, int y, int w, int h,
            ContentAlignment align)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            label.UseMnemonic = false;
            label.TextAlign = align;
            label.Tag = Ui.RoleOf(color);
            label.SetBounds(Ui.P(x), Ui.P(y), Ui.P(w), Ui.P(h));
            return label;
        }

        private static Label Lbl(string text, Font font, Color color, int x, int y, int w, int h)
        {
            return Lbl(text, font, color, x, y, w, h, ContentAlignment.MiddleLeft);
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = Program.AppName + " " + Program.Version;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.P(960), Ui.P(580));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            LogoBadge logo = new LogoBadge();
            logo.SetBounds(Ui.P(24), Ui.P(14), Ui.P(38), Ui.P(38));
            Controls.Add(logo);

            Controls.Add(Lbl(Program.AppName, Ui.Logo, Ui.Text, 70, 10, 240, 28));
            Controls.Add(Lbl("видео и картинки → GIF",
                Ui.Small, Ui.Muted, 71, 36, 300, 18));
            Controls.Add(Lbl("v" + Program.Version + " · локально", Ui.Tiny, Ui.Faint,
                480, 24, 168, 18, ContentAlignment.MiddleRight));

            gameButton = new IconButton(IconKind.Mouse);
            gameButton.SetBounds(Ui.P(652), Ui.P(16), Ui.P(32), Ui.P(32));
            gameButton.Click += delegate { OpenGame(); };
            Controls.Add(gameButton);

            editorButton = new IconButton(IconKind.Editor);
            editorButton.SetBounds(Ui.P(688), Ui.P(16), Ui.P(32), Ui.P(32));
            editorButton.Click += delegate { OpenEditor(); };
            Controls.Add(editorButton);

            gifToolsButton = new IconButton(IconKind.Frames);
            gifToolsButton.SetBounds(Ui.P(724), Ui.P(16), Ui.P(32), Ui.P(32));
            gifToolsButton.Click += delegate { OpenGifTools(); };
            Controls.Add(gifToolsButton);

            downloadsButton = new IconButton(IconKind.Download);
            downloadsButton.SetBounds(Ui.P(796), Ui.P(16), Ui.P(32), Ui.P(32));
            downloadsButton.Click += delegate { ShowDownloadMenu(); };
            Controls.Add(downloadsButton);

            settingsButton = new IconButton(IconKind.Settings);
            settingsButton.SetBounds(Ui.P(832), Ui.P(16), Ui.P(32), Ui.P(32));
            settingsButton.Click += delegate { OpenSettings(); };
            Controls.Add(settingsButton);

            discordButton = new IconButton(IconKind.Discord);
            discordButton.SetBounds(Ui.P(868), Ui.P(16), Ui.P(32), Ui.P(32));
            discordButton.Click += delegate { OpenLink("https://discord.gg/yMaS9HbjHV"); };
            Controls.Add(discordButton);

            donateButton = new IconButton(IconKind.Donate);
            donateButton.SetBounds(Ui.P(904), Ui.P(16), Ui.P(32), Ui.P(32));
            donateButton.Click += delegate { OpenLink("https://www.donationalerts.com/r/arsikfox16"); };
            Controls.Add(donateButton);

            cutoutButton = new IconButton(IconKind.Cutout);
            cutoutButton.SetBounds(Ui.P(760), Ui.P(16), Ui.P(32), Ui.P(32));
            cutoutButton.Click += delegate { OpenCutout(); };
            Controls.Add(cutoutButton);

            headerTip = new ToolTip();
            headerTip.SetToolTip(gameButton, Loc.T("Играть · Змейка"));
            headerTip.SetToolTip(editorButton, Loc.T("Видеоредактор · обрезка видео и фото"));
            headerTip.SetToolTip(gifToolsButton, Loc.T("GIF-инструменты · кадры и сборка"));
            headerTip.SetToolTip(downloadsButton, Loc.T("Скачать видео, музыку или TikTok"));
            headerTip.SetToolTip(settingsButton, Loc.T("Настройки · тема оформления"));
            headerTip.SetToolTip(discordButton, Loc.T("Discord — чат сообщества"));
            headerTip.SetToolTip(donateButton, Loc.T("Поддержать проект · Donate"));
            headerTip.SetToolTip(cutoutButton, Loc.T("Убрать фон · фото и GIF"));

            const string credits =
                "Coders - deepseek v4.1 flash 1017, arsikfox14\n" +
                "beta-testers - dismasdismavich, sosiska_0, d.i.0\n" +
                "donaters - none";
            infoBadge = new InfoBadge();
            infoBadge.InfoText = credits;
            infoBadge.SetBounds(Ui.P(914), Ui.P(552), Ui.P(22), Ui.P(22));
            infoBadge.Click += delegate { ShowCredits(); };
            Controls.Add(infoBadge);

            headerTip.AutoPopDelay = 15000;
            headerTip.InitialDelay = 250;
            headerTip.ReshowDelay = 100;
            headerTip.ShowAlways = true;
            headerTip.SetToolTip(infoBadge, credits);

            drop = new DropCard();
            drop.SetBounds(Ui.P(24), Ui.P(68), Ui.P(912), Ui.P(92));
            drop.PickFile += delegate { PickFile(); };
            ContextMenuStrip dropMenu = new ContextMenuStrip();
            dropMenu.ShowImageMargin = false;
            dropMenu.ShowCheckMargin = false;
            dropMenu.Font = Ui.Base;
            dropMenu.BackColor = Ui.Card;
            dropMenu.ForeColor = Ui.Text;
            dropMenu.Renderer = new MenuRenderer();
            AddMenuItem(dropMenu, "Вставить из буфера", delegate { PasteFromClipboard(); });
            AddMenuItem(dropMenu, "Выбрать файл…", delegate { PickFile(); });
            drop.ContextMenuStrip = dropMenu;
            Controls.Add(drop);

            Card settings = new Card();
            settings.SetBounds(Ui.P(24), Ui.P(172), Ui.P(560), Ui.P(376));
            Controls.Add(settings);

            settings.Controls.Add(Lbl("Настройки", Ui.H2, Ui.Text, 18, 12, 300, 26));

            startLabel = Lbl("Начало, с", Ui.Tiny, Ui.Muted, 18, 54, 96, 18);
            settings.Controls.Add(startLabel);
            durationLabel = Lbl("Длительность, с", Ui.Tiny, Ui.Muted, 124, 54, 96, 18);
            settings.Controls.Add(durationLabel);
            widthLabel = Lbl("Ширина, px", Ui.Tiny, Ui.Muted, 230, 54, 100, 18);
            settings.Controls.Add(widthLabel);
            fpsLabel = Lbl("Кадры/с", Ui.Tiny, Ui.Muted, 336, 54, 96, 18);
            settings.Controls.Add(fpsLabel);
            limitLabel = Lbl("Лимит, МБ", Ui.Tiny, Ui.Muted, 442, 54, 96, 18);
            settings.Controls.Add(limitLabel);

            imageNote = Lbl("Для картинки время и кадры\nне нужны: получится GIF\nиз одного кадра.",
                Ui.Small, Ui.Muted, 18, 52, 252, 52);
            imageNote.Visible = false;
            settings.Controls.Add(imageNote);

            startInput = new FlatInput();
            startInput.SetBounds(Ui.P(18), Ui.P(72), Ui.P(96), Ui.P(34));
            startInput.Value = "0";
            settings.Controls.Add(startInput);

            durationInput = new FlatInput();
            durationInput.SetBounds(Ui.P(124), Ui.P(72), Ui.P(96), Ui.P(34));
            durationInput.Placeholder = "весь клип";
            settings.Controls.Add(durationInput);

            widthInput = new FlatInput();
            widthInput.SetBounds(Ui.P(230), Ui.P(72), Ui.P(96), Ui.P(34));
            widthInput.Value = "480";
            settings.Controls.Add(widthInput);

            fpsInput = new FlatInput();
            fpsInput.SetBounds(Ui.P(336), Ui.P(72), Ui.P(96), Ui.P(34));
            fpsInput.Value = "30";
            settings.Controls.Add(fpsInput);
            headerTip.SetToolTip(widthInput, Loc.T("0 — оригинальная ширина"));
            headerTip.SetToolTip(fpsInput, Loc.T("От 1 до 60 кадров в секунду"));

            limitInput = new FlatInput();
            limitInput.SetBounds(Ui.P(442), Ui.P(72), Ui.P(96), Ui.P(34));
            limitInput.Placeholder = "без лимита";
            settings.Controls.Add(limitInput);

            settings.Controls.Add(Lbl("Сглаживание переходов", Ui.Small, Ui.Muted, 18, 126, 300, 18));

            ditherBayer = new PillButton();
            ditherBayer.Text = "Байер — компактнее";
            ditherBayer.Font = Ui.Small;
            ditherBayer.SetBounds(Ui.P(18), Ui.P(146), Ui.P(169), Ui.P(34));
            settings.Controls.Add(ditherBayer);

            ditherSierra = new PillButton();
            ditherSierra.Text = "Sierra — плавнее";
            ditherSierra.Font = Ui.Small;
            ditherSierra.SetBounds(Ui.P(195), Ui.P(146), Ui.P(169), Ui.P(34));
            settings.Controls.Add(ditherSierra);

            ditherNone = new PillButton();
            ditherNone.Text = "Без сглаживания";
            ditherNone.Font = Ui.Small;
            ditherNone.SetBounds(Ui.P(372), Ui.P(146), Ui.P(170), Ui.P(34));
            settings.Controls.Add(ditherNone);

            ditherBayer.Click += delegate { SetDither(0); };
            ditherSierra.Click += delegate { SetDither(1); };
            ditherNone.Click += delegate { SetDither(2); };
            SetDither(1);

            loopToggle = new ToggleRow();
            loopToggle.SetBounds(Ui.P(18), Ui.P(208), Ui.P(524), Ui.P(38));
            loopToggle.Title = "Бесконечный цикл";
            loopToggle.Subtitle = "GIF будет повторяться без остановки";
            loopToggle.Checked = true;
            settings.Controls.Add(loopToggle);

            fastToggle = new ToggleRow();
            fastToggle.SetBounds(Ui.P(18), Ui.P(246), Ui.P(524), Ui.P(38));
            fastToggle.Title = "Быстрый режим";
            fastToggle.Subtitle = "Один проход вместо двух: быстрее, но палитра чуть беднее";
            settings.Controls.Add(fastToggle);

            convertButton = new PillButton();
            convertButton.Accent = true;
            convertButton.Text = "Сделать GIF";
            convertButton.Font = Ui.Bold;
            convertButton.SetBounds(Ui.P(18), Ui.P(304), Ui.P(254), Ui.P(44));
            convertButton.Click += delegate { StartConversion(); };
            settings.Controls.Add(convertButton);

            cancelButton = new PillButton();
            cancelButton.Ghost = true;
            cancelButton.Text = "Отменить";
            cancelButton.SetBounds(Ui.P(288), Ui.P(304), Ui.P(254), Ui.P(44));
            cancelButton.Visible = false;
            cancelButton.Click += delegate { CancelConversion(); };
            settings.Controls.Add(cancelButton);

            Card resultCard = new Card();
            resultCard.SetBounds(Ui.P(600), Ui.P(172), Ui.P(336), Ui.P(376));
            Controls.Add(resultCard);

            resultCard.Controls.Add(Lbl("Результат", Ui.H2, Ui.Text, 18, 12, 300, 26));

            emptyState = new EmptyState();
            emptyState.SetBounds(Ui.P(0), Ui.P(40), Ui.P(336), Ui.P(336));
            resultCard.Controls.Add(emptyState);

            progressView = new Panel();
            progressView.BackColor = Color.Transparent;
            progressView.SetBounds(Ui.P(0), Ui.P(40), Ui.P(336), Ui.P(336));
            progressView.Visible = false;
            resultCard.Controls.Add(progressView);

            percentLabel = Lbl("0%", new Font("Segoe UI", 30f, FontStyle.Bold), Ui.Accent,
                18, 102, 300, 60, ContentAlignment.MiddleCenter);
            progressView.Controls.Add(percentLabel);

            stageLabel = Lbl("Готовимся…", Ui.Base, Ui.Muted, 18, 166, 300, 22,
                ContentAlignment.MiddleCenter);
            progressView.Controls.Add(stageLabel);

            progressBar = new SlimBar();
            progressBar.SetBounds(Ui.P(18), Ui.P(200), Ui.P(300), Ui.P(8));
            progressView.Controls.Add(progressBar);

            progressSub = Lbl("", Ui.Tiny, Ui.Faint, 18, 216, 300, 20,
                ContentAlignment.MiddleCenter);
            progressView.Controls.Add(progressSub);

            resultView = new Panel();
            resultView.BackColor = Color.Transparent;
            resultView.SetBounds(Ui.P(0), Ui.P(40), Ui.P(336), Ui.P(336));
            resultView.Visible = false;
            resultCard.Controls.Add(resultView);

            previewHost = new Panel();
            previewHost.SetBounds(Ui.P(18), Ui.P(8), Ui.P(300), Ui.P(228));
            previewHost.BackColor = Ui.Card;
            previewHost.Tag = "surface";
            previewHost.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, previewHost.Width - 1, previewHost.Height - 1);
                Ui.StrokeRound(e.Graphics, r, Ui.P(10), Ui.Border, 1f);
            };
            resultView.Controls.Add(previewHost);

            preview = new PictureBox();
            preview.SetBounds(Ui.P(6), Ui.P(6), Ui.P(288), Ui.P(216));
            preview.SizeMode = PictureBoxSizeMode.Zoom;
            preview.BackColor = Ui.Card;
            preview.Tag = "surface";
            previewHost.Controls.Add(preview);

            resultMeta = Lbl("", Ui.Small, Ui.Muted, 18, 244, 300, 20,
                ContentAlignment.MiddleCenter);
            resultView.Controls.Add(resultMeta);

            saveButton = new PillButton();
            saveButton.Accent = true;
            saveButton.Text = "Сохранить";
            saveButton.Font = Ui.Bold;
            saveButton.SetBounds(Ui.P(18), Ui.P(272), Ui.P(94), Ui.P(42));
            saveButton.Click += delegate { SaveResult(); };
            resultView.Controls.Add(saveButton);

            copyButton = new PillButton();
            copyButton.Ghost = true;
            copyButton.Text = "Копировать";
            copyButton.SetBounds(Ui.P(117), Ui.P(272), Ui.P(100), Ui.P(42));
            copyButton.Click += delegate { CopyResult(); };
            resultView.Controls.Add(copyButton);

            openButton = new PillButton();
            openButton.Ghost = true;
            openButton.Text = "Открыть";
            openButton.SetBounds(Ui.P(222), Ui.P(272), Ui.P(96), Ui.P(42));
            openButton.Click += delegate { OpenResult(); };
            resultView.Controls.Add(openButton);

            ResumeLayout(false);
        }

        private void ShowDownloadMenu()
        {
            if (busy) return;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Font = Ui.Base;
            menu.BackColor = Ui.Card;
            menu.ForeColor = Ui.Text;
            menu.Renderer = new MenuRenderer();

            AddMenuItem(menu, "YouTube и другие сайты",
                delegate { OpenMediaDownload(false); });
            AddMenuItem(menu, "Музыка из Spotify",
                delegate { OpenMediaDownload(true); });

            menu.Show(downloadsButton, new Point(0, downloadsButton.Height + Ui.P(4)));
        }

        private static void AddMenuItem(ContextMenuStrip menu, string text, EventHandler click)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ForeColor = Ui.Text;
            item.BackColor = Ui.Card;
            item.Padding = new Padding(Ui.P(10), Ui.P(5), Ui.P(10), Ui.P(5));
            item.Click += click;
            menu.Items.Add(item);
        }

        private void OpenMediaDownload(bool spotify)
        {
            if (busy) return;
            using (MediaDownloadForm dialog = new MediaDownloadForm(spotify))
            {
                dialog.ShowDialog(this);
            }
        }

        /// <summary>Открывает ссылку в браузере по умолчанию.</summary>
        private void OpenLink(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось открыть ссылку: " + ex.Message,
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private sealed class MenuRenderer : ToolStripProfessionalRenderer
        {
            public MenuRenderer() : base(new MenuColors()) { }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (SolidBrush brush = new SolidBrush(Ui.Card))
                {
                    e.Graphics.FillRectangle(brush, e.AffectedBounds);
                }
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Selected) return;
                using (SolidBrush brush = new SolidBrush(Ui.Track))
                {
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
                }
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Ui.Text : Ui.Faint;
                base.OnRenderItemText(e);
            }
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Ui.Card; } }
            public override Color MenuBorder { get { return Ui.Border; } }
            public override Color MenuItemBorder { get { return Ui.Border; } }
            public override Color MenuItemSelected { get { return Ui.Track; } }
            public override Color ImageMarginGradientBegin { get { return Ui.Card; } }
            public override Color ImageMarginGradientMiddle { get { return Ui.Card; } }
            public override Color ImageMarginGradientEnd { get { return Ui.Card; } }
            public override Color SeparatorDark { get { return Ui.Border; } }
            public override Color SeparatorLight { get { return Ui.Border; } }
        }

    }

    internal sealed class EmptyState : UiControl
    {
        public EmptyState()
        {
            Cursor = Cursors.Default;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (Ui.Gta6)
            {
                // Пальмы и чайки вокруг пустого результата.
                Ui.DrawPalm(g, Width * 0.13f, Height * 0.98f, Height * 0.34f,
                    Color.FromArgb(74, Ui.Accent), -0.08f);
                Ui.DrawPalm(g, Width * 0.87f, Height * 0.98f, Height * 0.27f,
                    Color.FromArgb(58, Ui.Accent), 0.10f);
                Ui.DrawGull(g, Width * 0.36f, Height * 0.13f, Ui.P(9),
                    Color.FromArgb(110, Ui.Accent));
                Ui.DrawGull(g, Width * 0.50f, Height * 0.09f, Ui.P(6),
                    Color.FromArgb(90, Ui.Accent));
                Ui.DrawGull(g, Width * 0.63f, Height * 0.16f, Ui.P(8),
                    Color.FromArgb(100, Ui.Accent));
            }
            else if (Ui.Holiday)
            {
                if (Ui.NewYear)
                {
                    Ui.DrawSnowflake(g, Width - Ui.P(28), Ui.P(28), Ui.P(20),
                        Color.FromArgb(120, Ui.Accent), Math.Max(1.2f, Ui.P(1.5)));
                    Ui.DrawSnowflake(g, Ui.P(28), Ui.P(46), Ui.P(12),
                        Color.FromArgb(105, Ui.Accent), Math.Max(1.1f, Ui.P(1.3)));
                    Ui.DrawSnowflake(g, Width - Ui.P(72), Ui.P(88), Ui.P(9),
                        Color.FromArgb(95, Ui.Accent), Math.Max(1f, Ui.P(1.2)));
                    Ui.DrawSnowflake(g, Width - Ui.P(34), Ui.P(142), Ui.P(8),
                        Color.FromArgb(90, Ui.Accent), Math.Max(1f, Ui.P(1.1)));
                }
                else
                {
                    Rectangle pumpkin = new Rectangle(
                        Width - Ui.P(62), Ui.P(20), Ui.P(38), Ui.P(32));
                    Ui.DrawPumpkin(g, pumpkin,
                        Color.FromArgb(140, Ui.Accent), Ui.Bg);
                    Ui.DrawLeaf(g, Ui.P(26), Ui.P(58), Ui.P(20),
                        Color.FromArgb(135, Ui.Accent), -28f);
                    Ui.DrawLeaf(g, Width - Ui.P(92), Ui.P(74), Ui.P(15),
                        Color.FromArgb(120, Ui.Accent), 40f);
                    Ui.DrawLeaf(g, Width - Ui.P(30), Ui.P(126), Ui.P(12),
                        Color.FromArgb(110, Ui.Accent), 18f);
                    Ui.DrawLeaf(g, Ui.P(58), Ui.P(24), Ui.P(11),
                        Color.FromArgb(110, Ui.Accent), 56f);
                    Ui.DrawLeaf(g, Width - Ui.P(26), Ui.P(210), Ui.P(10),
                        Color.FromArgb(100, Ui.Accent), -64f);
                }
            }
            else if (Ui.Sakura)
            {
                Ui.DrawBlossom(g, Width - Ui.P(30), Ui.P(30), Ui.P(14),
                    Color.FromArgb(130, Ui.Accent), Color.FromArgb(150, 255, 220, 130));
                Ui.DrawPetal(g, Ui.P(30), Ui.P(70), Ui.P(10),
                    Color.FromArgb(125, Ui.Accent), -30f);
                Ui.DrawPetal(g, Width - Ui.P(76), Ui.P(92), Ui.P(8),
                    Color.FromArgb(115, Ui.Accent), 40f);
                Ui.DrawPetal(g, Width - Ui.P(36), Ui.P(150), Ui.P(7),
                    Color.FromArgb(100, Ui.Accent), -60f);
            }
            else if (Ui.Summer)
            {
                Ui.DrawSun(g, Width - Ui.P(34), Ui.P(34), Ui.P(13),
                    Color.FromArgb(130, Ui.Accent));
                Ui.DrawGull(g, Ui.P(34), Ui.P(60), Ui.P(10),
                    Color.FromArgb(120, Ui.Accent));
                Ui.DrawGull(g, Ui.P(64), Ui.P(42), Ui.P(7),
                    Color.FromArgb(100, Ui.Accent));
                Ui.DrawWave(g, Width - Ui.P(110), Ui.P(120), Ui.P(70), Ui.P(8),
                    Color.FromArgb(110, Ui.Accent));
            }

            int cx = Width / 2;
            int top = Ui.P(74);
            int bw = Ui.P(64);
            int bh = Ui.P(50);
            Rectangle box = new Rectangle(cx - bw / 2, top, bw, bh);
            Ui.FillRound(g, box, Ui.P(14), Ui.AccentSoft);

            Rectangle frame = new Rectangle(box.X + Ui.P(15), box.Y + Ui.P(12),
                box.Width - Ui.P(30), box.Height - Ui.P(24));
            using (Pen pen = new Pen(Ui.Accent, 1.6f))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawRectangle(pen, frame);
                g.DrawEllipse(pen, frame.X + Ui.P(3), frame.Y + Ui.P(3), Ui.P(6), Ui.P(6));
                Point[] hills = new Point[]
                {
                    new Point(frame.X + Ui.P(2), frame.Bottom - Ui.P(3)),
                    new Point(frame.X + frame.Width / 2, frame.Y + frame.Height / 2 + Ui.P(2)),
                    new Point(frame.Right - Ui.P(3), frame.Bottom - Ui.P(3))
                };
                g.DrawLines(pen, hills);
            }

            int textTop = top + bh + Ui.P(24);
            Ui.Write(g, "Здесь появится GIF", Ui.Semi,
                new Rectangle(Ui.P(16), textTop, Width - Ui.P(32), Ui.P(24)),
                Ui.Text, Ui.CenterMid);
            Ui.Write(g, "Настройте параметры и нажмите «Сделать GIF»", Ui.Small,
                new Rectangle(Ui.P(16), textTop + Ui.P(26), Width - Ui.P(32), Ui.P(18)),
                Ui.Muted, Ui.CenterMid);
        }
    }
}
