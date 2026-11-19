using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Video2Gif
{
    /* --------------------------------------------------------------- Плитка темы */

    internal sealed class ThemeTile : UiControl
    {
        public string ThemeKey = "dark";
        public string Title = "";
        public Color SwatchBg;
        public Color SwatchCard;
        public Color SwatchAccent;

        private bool active;
        private bool hover;
        private bool down;

        public bool Active
        {
            get { return active; }
            set
            {
                if (active == value) return;
                active = value;
                Invalidate();
            }
        }

        public ThemeTile()
        {
            TabStop = false;
        }

        public void Pick()
        {
            OnClick(EventArgs.Empty);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            down = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            down = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Active ? Ui.AccentSoft : (hover ? Ui.Track : Ui.Card);
            if (down) fill = Ui.Track;
            Color border = Active ? Ui.Accent : (hover ? Ui.BorderStrong : Ui.Border);
            Ui.FillRound(g, r, Ui.P(10), fill);
            Ui.StrokeRound(g, r, Ui.P(10), border, Active ? 1.5f : 1f);

            int d = Ui.P(16);
            int x = Ui.P(14);
            int y = Ui.P(13);
            DrawSwatch(g, new Rectangle(x, y, d, d), SwatchBg, Ui.Border);
            DrawSwatch(g, new Rectangle(x + Ui.P(19), y, d, d), SwatchCard, Ui.Border);
            if (ThemeKey == "gta6")
            {
                Rectangle swatch = new Rectangle(x + Ui.P(38), y, d, d);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(swatch);
                    using (LinearGradientBrush brush = Ui.GtaGradient(swatch, 35f))
                    {
                        g.FillPath(brush, path);
                    }
                    using (Pen pen = new Pen(Ui.Border, 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
            else if (ThemeKey == "gamesense")
            {
                Rectangle swatch = new Rectangle(x + Ui.P(38), y, d, d);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(swatch);
                    using (LinearGradientBrush brush = Ui.RainbowBrush(swatch, 0.08, 7))
                    {
                        g.FillPath(brush, path);
                    }
                    using (Pen pen = new Pen(Ui.Border, 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
            else
            {
                DrawSwatch(g, new Rectangle(x + Ui.P(38), y, d, d), SwatchAccent, Ui.Border);
            }

            Ui.Write(g, Title, Ui.Small,
                new Rectangle(Ui.P(14), Ui.P(34), Width - Ui.P(28), Ui.P(18)),
                Active ? Ui.Text : Ui.Muted, Ui.LeftMid);
        }

        private static void DrawSwatch(Graphics g, Rectangle r, Color color, Color border)
        {
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, r);
            }
            using (Pen pen = new Pen(border, 1f))
            {
                g.DrawEllipse(pen, r);
            }
        }
    }

    /* ------------------------------------------------------------------ Окно */

    internal sealed class SettingsForm : ThemedForm
    {
        private readonly Action<string> pick;
        private readonly ThemeTile autoTile;
        private readonly ThemeTile lightTile;
        private readonly ThemeTile darkTile;
        private readonly ThemeTile newYearTile;
        private readonly ThemeTile halloweenTile;
        private readonly ThemeTile gtaTile;
        private readonly ThemeTile gamesenseTile;
        private readonly ThemeTile sakuraTile;
        private readonly ThemeTile summerTile;
        private PillButton updateButton;
        private Label updateStatus;
        private PillButton customButton;
        private PillButton customDeleteButton;
        private ContextMenuStrip customMenu;
        private bool checking;

        public SettingsForm(Action<string> pickTheme)
        {
            pick = pickTheme;
            BackgroundSettings.Load();

            Text = "Настройки — " + Program.AppName;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            ClientSize = new Size(Ui.P(428), Ui.P(544));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Controls.Add(Lbl("Настройки", Ui.H2, Ui.Text, 24, 12, 300, 26));
            Controls.Add(Lbl("Тема оформления", Ui.Small, Ui.Muted, 24, 40, 300, 18));

            autoTile = MakeTile("auto", "Авто");
            autoTile.SwatchBg = Color.FromArgb(247, 247, 248);
            autoTile.SwatchCard = Color.FromArgb(14, 14, 15);
            ThemeKind seasonal = Ui.SeasonalTheme(DateTime.Now);
            autoTile.SwatchAccent = seasonal == ThemeKind.Halloween
                ? Color.FromArgb(255, 140, 66)
                : seasonal == ThemeKind.NewYear
                    ? Color.FromArgb(126, 200, 255)
                    : seasonal == ThemeKind.Gta6
                        ? Color.FromArgb(255, 79, 163)
                        : seasonal == ThemeKind.Sakura
                            ? Color.FromArgb(238, 138, 163)
                            : seasonal == ThemeKind.Summer
                                ? Color.FromArgb(255, 154, 74)
                                : Color.FromArgb(126, 126, 132);
            autoTile.SetBounds(Ui.P(24), Ui.P(62), Ui.P(89), Ui.P(58));
            autoTile.Click += delegate { Choose("auto"); };
            Controls.Add(autoTile);

            lightTile = MakeTile("light", "Светлая");
            lightTile.SwatchBg = Color.FromArgb(247, 247, 248);
            lightTile.SwatchCard = Color.White;
            lightTile.SwatchAccent = Color.FromArgb(23, 23, 23);
            lightTile.SetBounds(Ui.P(121), Ui.P(62), Ui.P(89), Ui.P(58));
            lightTile.Click += delegate { Choose("light"); };
            Controls.Add(lightTile);

            darkTile = MakeTile("dark", "Тёмная");
            darkTile.SwatchBg = Color.FromArgb(14, 14, 15);
            darkTile.SwatchCard = Color.FromArgb(24, 24, 26);
            darkTile.SwatchAccent = Color.FromArgb(242, 242, 243);
            darkTile.SetBounds(Ui.P(218), Ui.P(62), Ui.P(89), Ui.P(58));
            darkTile.Click += delegate { Choose("dark"); };
            Controls.Add(darkTile);

            newYearTile = MakeTile("newyear", "Снежная");
            newYearTile.SwatchBg = Color.FromArgb(9, 21, 38);
            newYearTile.SwatchCard = Color.FromArgb(16, 34, 56);
            newYearTile.SwatchAccent = Color.FromArgb(126, 200, 255);
            newYearTile.SetBounds(Ui.P(315), Ui.P(62), Ui.P(89), Ui.P(58));
            newYearTile.Click += delegate { Choose("newyear"); };
            Controls.Add(newYearTile);

            halloweenTile = MakeTile("halloween", "Хэллоуин");
            halloweenTile.SwatchBg = Color.FromArgb(18, 12, 24);
            halloweenTile.SwatchCard = Color.FromArgb(29, 19, 38);
            halloweenTile.SwatchAccent = Color.FromArgb(255, 140, 66);
            halloweenTile.SetBounds(Ui.P(24), Ui.P(126), Ui.P(89), Ui.P(58));
            halloweenTile.Click += delegate { Choose("halloween"); };
            Controls.Add(halloweenTile);

            gtaTile = MakeTile("gta6", "GTA 6");
            gtaTile.SwatchBg = Color.FromArgb(10, 9, 12);
            gtaTile.SwatchCard = Color.FromArgb(22, 17, 27);
            gtaTile.SwatchAccent = Color.FromArgb(255, 79, 163);
            gtaTile.SetBounds(Ui.P(121), Ui.P(126), Ui.P(89), Ui.P(58));
            gtaTile.Click += delegate { Choose("gta6"); };
            Controls.Add(gtaTile);

            gamesenseTile = MakeTile("gamesense", "Gamesense");
            gamesenseTile.SwatchBg = Color.FromArgb(18, 18, 20);
            gamesenseTile.SwatchCard = Color.FromArgb(24, 24, 27);
            gamesenseTile.SwatchAccent = Color.FromArgb(88, 178, 124);
            gamesenseTile.SetBounds(Ui.P(218), Ui.P(126), Ui.P(89), Ui.P(58));
            gamesenseTile.Click += delegate { Choose("gamesense"); };
            Controls.Add(gamesenseTile);

            sakuraTile = MakeTile("sakura", "Сакура");
            sakuraTile.SwatchBg = Color.FromArgb(26, 17, 21);
            sakuraTile.SwatchCard = Color.FromArgb(39, 26, 32);
            sakuraTile.SwatchAccent = Color.FromArgb(238, 138, 163);
            sakuraTile.SetBounds(Ui.P(315), Ui.P(126), Ui.P(89), Ui.P(58));
            sakuraTile.Click += delegate { Choose("sakura"); };
            Controls.Add(sakuraTile);

            summerTile = MakeTile("summer", "Лето");
            summerTile.SwatchBg = Color.FromArgb(28, 19, 15);
            summerTile.SwatchCard = Color.FromArgb(41, 29, 22);
            summerTile.SwatchAccent = Color.FromArgb(255, 154, 74);
            summerTile.SetBounds(Ui.P(24), Ui.P(190), Ui.P(89), Ui.P(58));
            summerTile.Click += delegate { Choose("summer"); };
            Controls.Add(summerTile);

            Controls.Add(Lbl("Свои темы", Ui.Small, Ui.Muted, 24, 260, 220, 18));

            customButton = new PillButton();
            customButton.Ghost = true;
            customButton.Font = Ui.Small;
            customButton.Text = "— свои темы —";
            customButton.SetBounds(Ui.P(24), Ui.P(280), Ui.P(196), Ui.P(30));
            customButton.Click += delegate { ShowCustomMenu(); };
            Controls.Add(customButton);

            PillButton addCustom = new PillButton();
            addCustom.Ghost = true;
            addCustom.Text = "Создать…";
            addCustom.Font = Ui.Small;
            addCustom.SetBounds(Ui.P(228), Ui.P(280), Ui.P(80), Ui.P(30));
            addCustom.Click += delegate { CreateCustom(); };
            Controls.Add(addCustom);

            customDeleteButton = new PillButton();
            customDeleteButton.Ghost = true;
            customDeleteButton.Text = "Удалить";
            customDeleteButton.Font = Ui.Small;
            customDeleteButton.SetBounds(Ui.P(316), Ui.P(280), Ui.P(88), Ui.P(30));
            customDeleteButton.Click += delegate { DeleteCustom(); };
            Controls.Add(customDeleteButton);

            Label backgroundStatus = Lbl("", Ui.Tiny, Ui.Faint, 24, 320, 280, 18);
            backgroundStatus.AutoEllipsis = true;
            PillButton backgroundButton = new PillButton();
            backgroundButton.Ghost = true;
            backgroundButton.Text = "Фон…";
            backgroundButton.Font = Ui.Small;
            backgroundButton.SetBounds(Ui.P(316), Ui.P(316), Ui.P(88), Ui.P(30));
            backgroundButton.Click += delegate
            {
                using (BackgroundForm dialog = new BackgroundForm())
                {
                    dialog.ShowDialog(this);
                }
                RefreshBackgroundStatus(backgroundStatus);
                Ui.ApplyTheme(this);
                Invalidate(true);
            };
            Controls.Add(backgroundStatus);
            Controls.Add(backgroundButton);
            RefreshBackgroundStatus(backgroundStatus);

            Controls.Add(Lbl("Язык интерфейса", Ui.Small, Ui.Muted, 24, 358, 220, 18));

            PillButton ruButton = new PillButton();
            ruButton.Text = "Русский";
            ruButton.Font = Ui.Small;
            ruButton.SetBounds(Ui.P(24), Ui.P(380), Ui.P(104), Ui.P(32));
            ruButton.Click += delegate { ChooseLanguage(Loc.Russian); };
            Controls.Add(ruButton);

            PillButton enButton = new PillButton();
            enButton.Text = "English";
            enButton.Font = Ui.Small;
            enButton.SetBounds(Ui.P(136), Ui.P(380), Ui.P(104), Ui.P(32));
            enButton.Click += delegate { ChooseLanguage(Loc.EnglishCode); };
            Controls.Add(enButton);
            ruButton.Accent = !Loc.IsEnglish;
            enButton.Accent = Loc.IsEnglish;

            Controls.Add(Lbl("Обновления", Ui.H2, Ui.Text, 24, 424, 300, 24));
            Controls.Add(Lbl("Версия " + Program.Version + " · GitHub: " + Updater.Repo,
                Ui.Tiny, Ui.Faint, 24, 450, 380, 18));

            updateButton = new PillButton();
            updateButton.Ghost = true;
            updateButton.Text = "Проверить обновления";
            updateButton.Font = Ui.Small;
            updateButton.SetBounds(Ui.P(24), Ui.P(492), Ui.P(208), Ui.P(36));
            updateButton.Click += delegate { CheckUpdates(); };
            Controls.Add(updateButton);

            updateStatus = Lbl("", Ui.Tiny, Ui.Faint, 24, 470, 380, 18);
            Controls.Add(updateStatus);

            PillButton done = new PillButton();
            done.Accent = true;
            done.Text = "Готово";
            done.Font = Ui.Bold;
            done.SetBounds(Ui.P(300), Ui.P(492), Ui.P(104), Ui.P(36));
            done.Click += delegate { Close(); };
            Controls.Add(done);

            RefreshCustomThemes();
            RefreshActive();
        }

        private static ThemeTile MakeTile(string key, string title)
        {
            ThemeTile tile = new ThemeTile();
            tile.ThemeKey = key;
            tile.Title = title;
            return tile;
        }

        private void Choose(string name)
        {
            if (pick != null) pick(name);
            RefreshActive();
            Ui.ApplyTheme(this);
            ApplyTitleBar();
            Invalidate(true);
        }

        private void ChooseLanguage(string code)
        {
            if (Loc.Language == code) return;
            Loc.SaveLanguage(code);
            if (MessageBox.Show(this, Loc.T("Перезапустить сейчас?"), Program.AppName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Application.Restart();
            }
        }

        private void RefreshBackgroundStatus(Label label)
        {
            if (label == null) return;
            if (!string.IsNullOrEmpty(BackgroundSettings.ImagePath))
            {
                label.Text = "Фон: " + Path.GetFileName(BackgroundSettings.ImagePath);
            }
            else if (BackgroundSettings.FillColor.HasValue)
            {
                Color color = BackgroundSettings.FillColor.Value;
                label.Text = "Фон: #" + color.R.ToString("X2") + color.G.ToString("X2") +
                    color.B.ToString("X2");
            }
            else
            {
                label.Text = "Фон: не задан";
            }
        }

        private void RefreshCustomThemes()
        {
            if (customButton == null) return;
            customButton.Text = string.IsNullOrEmpty(Ui.CustomName)
                ? "— свои темы —"
                : Ui.CustomName;
            if (customDeleteButton != null)
            {
                customDeleteButton.Enabled = CustomThemes.All.Count > 0;
            }
        }

        private void ShowCustomMenu()
        {
            if (customMenu != null)
            {
                customMenu.Dispose();
                customMenu = null;
            }
            ContextMenuStrip menu = new ContextMenuStrip();
            customMenu = menu;
            menu.BackColor = Ui.Card;
            menu.ForeColor = Ui.Text;
            menu.Font = Ui.Small;

            bool any = false;
            foreach (CustomTheme theme in CustomThemes.All)
            {
                string name = theme.Name;
                ToolStripMenuItem item = new ToolStripMenuItem(name);
                item.Click += delegate { Choose("custom:" + name); };
                menu.Items.Add(item);
                any = true;
            }
            if (!any)
            {
                ToolStripMenuItem empty = new ToolStripMenuItem("— нет своих тем —");
                empty.Enabled = false;
                menu.Items.Add(empty);
            }
            menu.Show(customButton, new Point(0, customButton.Height));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && customMenu != null)
            {
                customMenu.Dispose();
                customMenu = null;
            }
            base.Dispose(disposing);
        }

        private void CreateCustom()
        {
            using (CustomThemeForm dialog = new CustomThemeForm("", true,
                Color.FromArgb(122, 167, 255)))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                CustomThemes.Save(new CustomTheme
                {
                    Name = dialog.ThemeName,
                    Dark = dialog.DarkBase,
                    Accent = dialog.AccentColor
                });
                RefreshCustomThemes();
                Choose("custom:" + dialog.ThemeName);
            }
        }

        private void DeleteCustom()
        {
            string name = Ui.CustomName;
            if (string.IsNullOrEmpty(name)) return;
            if (MessageBox.Show(this, "Удалить тему «" + name + "»?", Program.AppName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            CustomThemes.Remove(name);
            RefreshCustomThemes();
            Choose("dark");
        }

        private void RefreshActive()
        {
            string current = Ui.ThemeName;
            autoTile.Active = Ui.AutoTheme;
            lightTile.Active = !Ui.AutoTheme && current == "light";
            darkTile.Active = !Ui.AutoTheme && current == "dark";
            newYearTile.Active = !Ui.AutoTheme && current == "newyear";
            halloweenTile.Active = !Ui.AutoTheme && current == "halloween";
            gtaTile.Active = !Ui.AutoTheme && current == "gta6";
            gamesenseTile.Active = !Ui.AutoTheme && current == "gamesense";
            sakuraTile.Active = !Ui.AutoTheme && current == "sakura";
            summerTile.Active = !Ui.AutoTheme && current == "summer";

            if (customButton != null)
            {
                customButton.Text = string.IsNullOrEmpty(Ui.CustomName)
                    ? "— свои темы —"
                    : Ui.CustomName;
            }
        }

        /* ---------------------------------------------------------- Обновления */

        private void CheckUpdates()
        {
            if (checking) return;
            checking = true;
            updateButton.Enabled = false;
            SetUpdateStatus("Проверяем…", false);

            Task.Factory.StartNew(delegate { return Updater.Check(); })
                .ContinueWith(delegate(Task<UpdateInfo> task)
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate { OnUpdateChecked(task); });
                    }
                    catch { }
                });
        }

        private void OnUpdateChecked(Task<UpdateInfo> task)
        {
            checking = false;
            updateButton.Enabled = true;

            if (task.IsFaulted)
            {
                Exception inner = task.Exception != null ? task.Exception.InnerException : null;
                SetUpdateStatus(inner != null ? inner.Message : "Не удалось проверить обновления.", true);
                return;
            }

            UpdateInfo info = task.Result;
            if (info == null || !info.HasUpdate)
            {
                SetUpdateStatus("Установлена последняя версия.", false);
                return;
            }

            SetUpdateStatus("Доступна версия " + info.Version + ".", false);
            DialogResult answer = MessageBox.Show(this,
                "Доступна версия " + info.Version + ".\nСкачать и установить сейчас?",
                Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            using (UpdateForm dialog = new UpdateForm(info))
            {
                dialog.ShowDialog(this);
            }
        }

        private void SetUpdateStatus(string text, bool error)
        {
            updateStatus.Text = text;
            updateStatus.ForeColor = error ? Ui.Bad : Ui.Faint;
            updateStatus.Tag = Ui.RoleOf(updateStatus.ForeColor);
        }

        private void ApplyTitleBar()
        {
            if (!IsHandleCreated) return;
            Ui.ApplyTitleBar(Handle);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTitleBar();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Ui.Gamesense) Ui.DrawCarbon(e.Graphics, ClientRectangle);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        /* Локальная копия помощника, чтобы не дублировать разметку. */
        private static Label Lbl(string text, Font font, Color color, int x, int y, int w, int h)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            label.UseMnemonic = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Tag = Ui.RoleOf(color);
            label.SetBounds(Ui.P(x), Ui.P(y), Ui.P(w), Ui.P(h));
            return label;
        }
    }
}
