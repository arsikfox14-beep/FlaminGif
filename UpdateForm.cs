using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Video2Gif
{
    /// <summary>Маленькое окно обновления: скачивает exe с GitHub и перезапускает программу.</summary>
    internal sealed class UpdateForm : Form
    {
        private readonly UpdateInfo info;
        private Label statusLabel;
        private Label percentLabel;
        private PillButton updateButton;
        private PillButton closeButton;
        private SlimBar bar;

        private bool working;

        public UpdateForm(UpdateInfo update)
        {
            info = update;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Обновление — " + Program.AppName;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            ClientSize = new Size(Ui.P(440), Ui.P(236));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Controls.Add(MakeLabel("Обновление программы", Ui.H2, Ui.Text, 24, 16, 392, 26));
            Controls.Add(MakeLabel("Установлено: " + Program.Version +
                "   ·   доступно: " + info.Version, Ui.Small, Ui.Muted, 24, 48, 392, 20));

            if (!string.IsNullOrEmpty(info.Notes))
            {
                string notes = info.Notes.Replace("\r\n", "\n").Replace('\n', ' ');
                if (notes.Length > 160) notes = notes.Substring(0, 157) + "…";
                Controls.Add(MakeLabel(notes, Ui.Tiny, Ui.Faint, 24, 72, 392, 32));
            }

            bar = new SlimBar();
            bar.SetBounds(Ui.P(24), Ui.P(118), Ui.P(392), Ui.P(8));
            bar.Visible = false;
            Controls.Add(bar);

            percentLabel = MakeLabel("", Ui.Small, Ui.Accent, 344, 100, 72, 18);
            percentLabel.TextAlign = ContentAlignment.MiddleRight;
            percentLabel.Visible = false;
            Controls.Add(percentLabel);

            statusLabel = MakeLabel("Нажмите «Скачать и установить».", Ui.Tiny, Ui.Muted, 24, 134, 392, 32);
            Controls.Add(statusLabel);

            updateButton = new PillButton();
            updateButton.Accent = true;
            updateButton.Text = "Скачать и установить";
            updateButton.Font = Ui.Bold;
            updateButton.SetBounds(Ui.P(24), Ui.P(178), Ui.P(220), Ui.P(42));
            updateButton.Click += delegate { StartUpdate(); };
            Controls.Add(updateButton);

            closeButton = new PillButton();
            closeButton.Ghost = true;
            closeButton.Text = "Позже";
            closeButton.SetBounds(Ui.P(256), Ui.P(178), Ui.P(160), Ui.P(42));
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);
        }

        private void StartUpdate()
        {
            if (working) return;
            working = true;
            updateButton.Enabled = false;
            closeButton.Enabled = false;
            bar.Value = 0;
            bar.Visible = true;
            percentLabel.Text = "0%";
            percentLabel.Visible = true;
            SetStatus("Скачиваем обновление…", false);

            Task.Factory.StartNew(delegate
            {
                return Updater.Download(info, delegate(int percent)
                {
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (IsDisposed) return;
                            bar.Value = percent;
                            percentLabel.Text = percent + "%";
                        });
                    }
                    catch { }
                });
            }).ContinueWith(delegate(Task<string> task)
            {
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate { OnDownloaded(task); });
                }
                catch { }
            });
        }

        private void OnDownloaded(Task<string> task)
        {
            if (task.IsFaulted)
            {
                working = false;
                updateButton.Enabled = true;
                closeButton.Enabled = true;
                bar.Visible = false;
                percentLabel.Visible = false;
                Exception inner = task.Exception != null ? task.Exception.InnerException : null;
                SetStatus(inner != null ? inner.Message : "Не удалось скачать обновление.", true);
                return;
            }

            bar.Value = 100;
            percentLabel.Text = "100%";
            // Снимаем блокировку закрытия: иначе Application.Exit не закроет окно
            // и установщик не сможет заменить exe.
            working = false;
            updateButton.Enabled = false;
            closeButton.Enabled = false;
            SetStatus("Готово. Программа перезапустится с новой версией.", false);
            try
            {
                Updater.ApplyAndRestart(task.Result);
            }
            catch (Exception ex)
            {
                closeButton.Enabled = true;
                SetStatus("Не удалось запустить установку: " + ex.Message, true);
            }
        }

        private void SetStatus(string text, bool error)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = error ? Ui.Bad : Ui.Muted;
            statusLabel.Tag = Ui.RoleOf(statusLabel.ForeColor);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (working)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.ApplyTitleBar(Handle);
        }

        private static Label MakeLabel(string text, Font font, Color color,
            int x, int y, int w, int h)
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
