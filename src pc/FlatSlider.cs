using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FlaminGif
{
    /// <summary>Плоский ползунок в стиле приложения, без системного серого фона.</summary>
    internal sealed class FlatSlider : UiControl
    {
        public int Minimum = 0;
        public int Maximum = 100;

        private int value;
        private bool dragging;

        public event EventHandler ValueChanged;

        public FlatSlider()
        {
            Height = Ui.P(28);
            TabStop = true;
        }

        public int Value
        {
            get { return value; }
            set
            {
                int next = Math.Max(Minimum, Math.Min(Maximum, value));
                if (next == this.value) return;
                this.value = next;
                Invalidate();
                EventHandler handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private int Pad
        {
            get { return Ui.P(9); }
        }

        private int TrackWidth
        {
            get { return Math.Max(1, Width - Pad * 2); }
        }

        private int ValueAt(int x)
        {
            double part = (x - Pad) / (double)TrackWidth;
            if (part < 0) part = 0;
            if (part > 1) part = 1;
            return Minimum + (int)Math.Round((Maximum - Minimum) * part);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            Value = ValueAt(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            // Если кнопку отпустили вне ползунка (быстрый рывок мышью,
            // переключение окна), значение больше не должно меняться.
            if (dragging && (e.Button & MouseButtons.Left) == 0 &&
                Control.MouseButtons == MouseButtons.None)
            {
                dragging = false;
                return;
            }
            if (dragging) Value = ValueAt(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (Control.MouseButtons == MouseButtons.None) dragging = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int height = Ui.P(6);
            int y = (Height - height) / 2;
            Rectangle track = new Rectangle(Pad, y, TrackWidth, height);
            Ui.FillRound(g, track, height / 2, Ui.Track);

            double part = Maximum > Minimum
                ? (value - Minimum) / (double)(Maximum - Minimum)
                : 0;
            int filled = (int)Math.Round(TrackWidth * part);
            if (filled > 0)
            {
                Rectangle progress = new Rectangle(Pad, y, filled, height);
                Ui.FillRound(g, progress, height / 2, Ui.Accent);
            }

            int knob = Ui.P(8);
            int cx = Pad + filled;
            int cy = Height / 2;
            Rectangle dot = new Rectangle(cx - knob, cy - knob, knob * 2, knob * 2);
            using (SolidBrush brush = new SolidBrush(Ui.Accent))
            {
                g.FillEllipse(brush, dot);
            }
            using (Pen pen = new Pen(Ui.Bg, Math.Max(1f, Ui.P(1.5f))))
            {
                g.DrawEllipse(pen, dot);
            }
        }
    }
}
