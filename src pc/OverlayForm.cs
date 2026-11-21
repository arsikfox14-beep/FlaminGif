using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FlaminGif
{
    /// <summary>Настройки подписи и второй картинки поверх кадра/видео.</summary>
    internal sealed class OverlayForm : ThemedForm
    {
        public string Caption = "";
        public bool CaptionTop;
        public string OverlayPath = "";
        public int OverlayCorner = 3;
        public int OverlayScale = 20;

        private FlatInput captionInput;
        private FlatInput overlayInput;
        private FlatInput scaleInput;
        private PillButton captionTopPill;
        private PillButton captionBottomPill;
        private PillButton[] cornerPills;

        public OverlayForm()
        {
            BuildUi();
        }

        public void SetValues(string caption, bool captionTop, string overlay,
            int corner, int scale)
        {
            Caption = caption;
            CaptionTop = captionTop;
            OverlayPath = overlay;
            OverlayCorner = Math.Max(0, Math.Min(3, corner));
            OverlayScale = Math.Max(5, Math.Min(80, scale));
            if (captionInput != null) captionInput.Value = Caption;
            if (overlayInput != null)
                overlayInput.Value = OverlayPath.Length > 0 ? Path.GetFileName(OverlayPath) : "";
            if (scaleInput != null) scaleInput.Value = OverlayScale.ToString();
            UpdatePills();
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = "Текст и поверх — " + Program.AppName;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.P(480), Ui.P(380));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Controls.Add(MakeLabel("Текст и поверх", Ui.H1, Ui.Text, 24, 16, 420, 30));
            Controls.Add(MakeLabel("Подпись и вторая картинка на фото или видео",
                Ui.Small, Ui.Muted, 24, 48, 432, 18));

            Controls.Add(MakeLabel("Подпись", Ui.Small, Ui.Muted, 24, 82, 200, 18));
            captionInput = new FlatInput();
            captionInput.SetBounds(Ui.P(24), Ui.P(102), Ui.P(432), Ui.P(36));
            captionInput.Placeholder = "необязательно";
            captionInput.Value = Caption;
            Controls.Add(captionInput);

            captionTopPill = new PillButton();
            captionTopPill.Ghost = true;
            captionTopPill.Text = "Сверху";
            captionTopPill.Font = Ui.Small;
            captionTopPill.SetBounds(Ui.P(24), Ui.P(146), Ui.P(120), Ui.P(32));
            captionTopPill.Click += delegate { CaptionTop = true; UpdatePills(); };
            Controls.Add(captionTopPill);

            captionBottomPill = new PillButton();
            captionBottomPill.Ghost = true;
            captionBottomPill.Text = "Снизу";
            captionBottomPill.Font = Ui.Small;
            captionBottomPill.SetBounds(Ui.P(150), Ui.P(146), Ui.P(120), Ui.P(32));
            captionBottomPill.Click += delegate { CaptionTop = false; UpdatePills(); };
            Controls.Add(captionBottomPill);

            Controls.Add(MakeLabel("Картинка поверх", Ui.Small, Ui.Muted, 24, 190, 220, 18));
            overlayInput = new FlatInput();
            overlayInput.SetBounds(Ui.P(24), Ui.P(210), Ui.P(320), Ui.P(36));
            overlayInput.Box.ReadOnly = true;
            overlayInput.Placeholder = "PNG или JPG";
            overlayInput.Value = OverlayPath.Length > 0 ? Path.GetFileName(OverlayPath) : "";
            Controls.Add(overlayInput);

            PillButton choose = new PillButton();
            choose.Ghost = true;
            choose.Text = "Выбрать";
            choose.Font = Ui.Small;
            choose.SetBounds(Ui.P(352), Ui.P(210), Ui.P(104), Ui.P(36));
            choose.Click += delegate { ChooseOverlay(); };
            Controls.Add(choose);

            Controls.Add(MakeLabel("Угол", Ui.Small, Ui.Muted, 24, 254, 100, 18));
            string[] arrows = new string[] { "↖", "↗", "↙", "↘" };
            cornerPills = new PillButton[4];
            for (int i = 0; i < 4; i++)
            {
                PillButton pill = new PillButton();
                pill.Ghost = true;
                pill.Text = arrows[i];
                pill.Font = Ui.H2;
                pill.SetBounds(Ui.P(24 + i * 48), Ui.P(274), Ui.P(42), Ui.P(34));
                int corner = i;
                pill.Click += delegate { OverlayCorner = corner; UpdatePills(); };
                Controls.Add(pill);
                cornerPills[i] = pill;
            }

            Controls.Add(MakeLabel("Размер, %", Ui.Small, Ui.Muted, 236, 254, 100, 18));
            scaleInput = new FlatInput();
            scaleInput.SetBounds(Ui.P(236), Ui.P(274), Ui.P(76), Ui.P(34));
            scaleInput.Value = OverlayScale.ToString();
            Controls.Add(scaleInput);

            PillButton done = new PillButton();
            done.Accent = true;
            done.Text = "Готово";
            done.Font = Ui.Bold;
            done.SetBounds(Ui.P(24), Ui.P(326), Ui.P(220), Ui.P(40));
            done.Click += delegate { Apply(); };
            Controls.Add(done);

            PillButton cancel = new PillButton();
            cancel.Ghost = true;
            cancel.Text = "Отмена";
            cancel.SetBounds(Ui.P(256), Ui.P(326), Ui.P(120), Ui.P(40));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            UpdatePills();
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

        private void ChooseOverlay()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Картинка поверх";
                dialog.Filter = "Картинки|*.png;*.jpg;*.jpeg;*.bmp|Все файлы|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    OverlayPath = dialog.FileName;
                    overlayInput.Value = Path.GetFileName(OverlayPath);
                }
            }
        }

        private void UpdatePills()
        {
            captionTopPill.Active = CaptionTop;
            captionBottomPill.Active = !CaptionTop;
            for (int i = 0; i < cornerPills.Length; i++)
                cornerPills[i].Active = i == OverlayCorner;
        }

        private void Apply()
        {
            double scale;
            if (!Fmt.TryNum(scaleInput.Value.Trim(), out scale) || scale < 5 || scale > 80)
            {
                MessageBox.Show(this, "Размер картинки — от 5 до 80%.",
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (OverlayPath.Length > 0 && !File.Exists(OverlayPath))
            {
                MessageBox.Show(this, "Файл картинки не найден.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Caption = captionInput.Value.Trim();
            OverlayScale = (int)Math.Round(scale);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
