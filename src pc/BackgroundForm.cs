using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FlaminGif
{
    /// <summary>Окно выбора фона: цвет, картинка или GIF.</summary>
    internal sealed class BackgroundForm : ThemedForm
    {
        private readonly Label status;

        public BackgroundForm()
        {
            Text = "Фон — " + Program.AppName;
            BackColor = Ui.Bg;
            ForeColor = Ui.Text;
            Font = Ui.Base;
            ClientSize = new Size(Ui.P(400), Ui.P(300));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label title = new Label();
            title.Text = "Фон приложения";
            title.Font = Ui.H2;
            title.ForeColor = Ui.Text;
            title.BackColor = Color.Transparent;
            title.SetBounds(Ui.P(24), Ui.P(16), Ui.P(340), Ui.P(26));
            Controls.Add(title);

            status = new Label();
            status.Font = Ui.Small;
            status.ForeColor = Ui.Muted;
            status.BackColor = Color.Transparent;
            status.AutoEllipsis = true;
            status.SetBounds(Ui.P(24), Ui.P(44), Ui.P(352), Ui.P(20));
            Controls.Add(status);

            PillButton color = new PillButton();
            color.Text = "Цвет…";
            color.Font = Ui.Small;
            color.SetBounds(Ui.P(24), Ui.P(74), Ui.P(110), Ui.P(34));
            color.Click += delegate { PickColor(); };
            Controls.Add(color);

            PillButton image = new PillButton();
            image.Text = "Картинка или GIF…";
            image.Font = Ui.Small;
            image.SetBounds(Ui.P(140), Ui.P(74), Ui.P(170), Ui.P(34));
            image.Click += delegate { PickImage(); };
            Controls.Add(image);

            PillButton reset = new PillButton();
            reset.Ghost = true;
            reset.Text = "Сброс";
            reset.Font = Ui.Small;
            reset.SetBounds(Ui.P(316), Ui.P(74), Ui.P(60), Ui.P(34));
            reset.Click += delegate { BackgroundSettings.Clear(); UpdateStatus(); };
            Controls.Add(reset);

            Label opacityLabel = new Label();
            opacityLabel.Font = Ui.Small;
            opacityLabel.ForeColor = Ui.Muted;
            opacityLabel.BackColor = Color.Transparent;
            opacityLabel.SetBounds(Ui.P(24), Ui.P(112), Ui.P(352), Ui.P(20));
            opacityLabel.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(opacityLabel);

            FlatSlider opacityBar = new FlatSlider();
            opacityBar.Minimum = 0;
            opacityBar.Maximum = 70;
            opacityBar.Value = (int)Math.Round((1.0 - BackgroundSettings.SurfaceOpacity) * 100.0);
            opacityBar.SetBounds(Ui.P(24), Ui.P(130), Ui.P(352), Ui.P(28));
            Controls.Add(opacityBar);

            Action refreshOpacity = delegate
            {
                opacityLabel.Text = "Прозрачность карточек: " + opacityBar.Value + "%";
            };
            opacityBar.ValueChanged += delegate
            {
                BackgroundSettings.SetSurfaceOpacity(1.0 - opacityBar.Value / 100.0);
                refreshOpacity();
            };
            refreshOpacity();

            Label windowLabel = new Label();
            windowLabel.Font = Ui.Small;
            windowLabel.ForeColor = Ui.Muted;
            windowLabel.BackColor = Color.Transparent;
            windowLabel.SetBounds(Ui.P(24), Ui.P(170), Ui.P(352), Ui.P(20));
            windowLabel.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(windowLabel);

            FlatSlider windowBar = new FlatSlider();
            windowBar.Minimum = 0;
            windowBar.Maximum = 100;
            windowBar.Value = (int)Math.Round((1.0 - BackgroundSettings.BackgroundOpacity) * 100.0);
            windowBar.SetBounds(Ui.P(24), Ui.P(188), Ui.P(352), Ui.P(28));
            Controls.Add(windowBar);

            Action refreshWindow = delegate
            {
                windowLabel.Text = "Прозрачность фона: " + windowBar.Value + "%";
            };
            windowBar.ValueChanged += delegate
            {
                BackgroundSettings.SetBackgroundOpacity(1.0 - windowBar.Value / 100.0);
                refreshWindow();
            };
            refreshWindow();

            PillButton done = new PillButton();
            done.Accent = true;
            done.Text = "Готово";
            done.Font = Ui.Bold;
            done.SetBounds(Ui.P(24), Ui.P(242), Ui.P(352), Ui.P(36));
            done.Click += delegate { Close(); };
            Controls.Add(done);

            UpdateStatus();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.ApplyTitleBar(Handle);
        }

        private void UpdateStatus()
        {
            if (!string.IsNullOrEmpty(BackgroundSettings.ImagePath))
            {
                status.Text = "Сейчас: " + Path.GetFileName(BackgroundSettings.ImagePath);
            }
            else if (BackgroundSettings.FillColor.HasValue)
            {
                Color color = BackgroundSettings.FillColor.Value;
                status.Text = "Сейчас: цвет #" + color.R.ToString("X2") +
                    color.G.ToString("X2") + color.B.ToString("X2");
            }
            else
            {
                status.Text = "Сейчас: стандартный фон темы";
            }
        }

        private void PickColor()
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                if (BackgroundSettings.FillColor.HasValue)
                {
                    dialog.Color = BackgroundSettings.FillColor.Value;
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    BackgroundSettings.SetColor(dialog.Color);
                    UpdateStatus();
                }
            }
        }

        private void PickImage()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите картинку или GIF";
                dialog.Filter = "Картинки и GIF|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    BackgroundSettings.SetImage(dialog.FileName);
                    UpdateStatus();
                }
            }
        }
    }
}
