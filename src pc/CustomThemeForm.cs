using System;
using System.Drawing;
using System.Windows.Forms;

namespace Video2Gif
{
    /// <summary>Маленькое окно создания своей темы: имя, основа и акцент.</summary>
    internal sealed class CustomThemeForm : ThemedForm
    {
        private readonly TextBox nameInput;
        private readonly PillButton darkButton;
        private readonly PillButton lightButton;
        private readonly PillButton colorButton;
        private bool darkBase = true;
        private Color accent = Color.FromArgb(122, 167, 255);

        public string ThemeName
        {
            get { return nameInput.Text.Trim(); }
        }

        public bool DarkBase
        {
            get { return darkBase; }
        }

        public Color AccentColor
        {
            get { return accent; }
        }

        public CustomThemeForm(string name, bool dark, Color accentColor)
        {
            darkBase = dark;
            accent = accentColor;

            Text = "Своя тема — " + Program.AppName;
            BackColor = Ui.Bg;
            ForeColor = Ui.Text;
            Font = Ui.Base;
            ClientSize = new Size(Ui.P(380), Ui.P(252));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Controls.Add(LabelOf("Название", 24, 18));
            nameInput = new TextBox();
            nameInput.Text = name ?? "";
            nameInput.MaxLength = 28;
            nameInput.BackColor = Ui.Card;
            nameInput.ForeColor = Ui.Text;
            nameInput.BorderStyle = BorderStyle.FixedSingle;
            nameInput.Font = Ui.Base;
            nameInput.SetBounds(Ui.P(24), Ui.P(42), Ui.P(332), Ui.P(28));
            Controls.Add(nameInput);

            Controls.Add(LabelOf("Основа", 24, 82));

            darkButton = new PillButton();
            darkButton.Text = "Тёмная";
            darkButton.Font = Ui.Small;
            darkButton.SetBounds(Ui.P(24), Ui.P(106), Ui.P(150), Ui.P(34));
            darkButton.Click += delegate { SetBase(true); };
            Controls.Add(darkButton);

            lightButton = new PillButton();
            lightButton.Text = "Светлая";
            lightButton.Font = Ui.Small;
            lightButton.SetBounds(Ui.P(186), Ui.P(106), Ui.P(150), Ui.P(34));
            lightButton.Click += delegate { SetBase(false); };
            Controls.Add(lightButton);

            Controls.Add(LabelOf("Цвет акцента", 24, 150));

            colorButton = new PillButton();
            colorButton.Font = Ui.Small;
            colorButton.SetBounds(Ui.P(24), Ui.P(174), Ui.P(150), Ui.P(34));
            colorButton.Click += delegate { PickColor(); };
            Controls.Add(colorButton);

            PillButton save = new PillButton();
            save.Accent = true;
            save.Text = "Сохранить";
            save.Font = Ui.Bold;
            save.SetBounds(Ui.P(24), Ui.P(210), Ui.P(160), Ui.P(34));
            save.Click += delegate { Accept(); };
            Controls.Add(save);

            PillButton cancel = new PillButton();
            cancel.Ghost = true;
            cancel.Text = "Отмена";
            cancel.SetBounds(Ui.P(196), Ui.P(210), Ui.P(140), Ui.P(34));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            SetBase(darkBase);
            UpdateColorButton();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.ApplyTitleBar(Handle);
        }

        private Label LabelOf(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.BackColor = Color.Transparent;
            label.ForeColor = Ui.Muted;
            label.Font = Ui.Small;
            label.SetBounds(Ui.P(x), Ui.P(y), Ui.P(300), Ui.P(18));
            return label;
        }

        private void SetBase(bool dark)
        {
            darkBase = dark;
            darkButton.Accent = dark;
            lightButton.Accent = !dark;
        }

        private void UpdateColorButton()
        {
            colorButton.Text = "#" + accent.R.ToString("X2") + accent.G.ToString("X2") +
                accent.B.ToString("X2");
        }

        private void PickColor()
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = accent;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    accent = dialog.Color;
                    UpdateColorButton();
                }
            }
        }

        private void Accept()
        {
            if (ThemeName.Length == 0)
            {
                MessageBox.Show(this, "Введите название темы.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                nameInput.Focus();
                return;
            }
            if (ThemeName.Length > 28) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
