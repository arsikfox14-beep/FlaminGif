using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Video2Gif
{
    internal enum IconKind
    {
        Mouse,
        Editor,
        Frames,
        Download,
        Settings,
        Discord,
        Donate
    }

    /// <summary>
    /// Логотип Discord из исходного PNG: чёрный фон превращается в прозрачность,
    /// а сам знак перекрашивается в цвет темы.
    /// </summary>
    internal static class DiscordMark
    {
        private const string ResourceName = "Video2Gif.discord.png";

        private static Bitmap source;
        private static bool sourceLoaded;
        private static readonly Dictionary<int, Bitmap> cache = new Dictionary<int, Bitmap>();

        public static bool Available
        {
            get { return Source() != null; }
        }

        public static void Draw(Graphics g, RectangleF target, Color color)
        {
            Bitmap image = Get(color);
            if (image == null) return;

            InterpolationMode oldMode = g.InterpolationMode;
            PixelOffsetMode oldPixel = g.PixelOffsetMode;
            CompositingQuality oldQuality = g.CompositingQuality;
            try
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(image, target);
            }
            finally
            {
                g.InterpolationMode = oldMode;
                g.PixelOffsetMode = oldPixel;
                g.CompositingQuality = oldQuality;
            }
        }

        private static Bitmap Get(Color color)
        {
            int key = color.ToArgb();
            lock (cache)
            {
                Bitmap cached;
                if (cache.TryGetValue(key, out cached)) return cached;
            }

            Bitmap src = Source();
            if (src == null) return null;

            Bitmap tinted = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    Color pixel = src.GetPixel(x, y);
                    int alpha = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                    if (alpha < 6) continue;
                    tinted.SetPixel(x, y, Color.FromArgb(alpha, color));
                }
            }

            lock (cache) cache[key] = tinted;
            return tinted;
        }

        private static Bitmap Source()
        {
            lock (cache)
            {
                if (sourceLoaded) return source;
                sourceLoaded = true;
                try
                {
                    using (Stream stream = Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream(ResourceName))
                    {
                        if (stream != null)
                        {
                            using (Bitmap loaded = new Bitmap(stream))
                            {
                                source = new Bitmap(loaded);
                            }
                        }
                    }
                }
                catch
                {
                    source = null;
                }
                return source;
            }
        }
    }

    internal sealed class IconButton : UiControl
    {
        public readonly IconKind Kind;

        private bool hover;
        private bool down;

        public IconButton(IconKind kind)
        {
            Kind = kind;
            TabStop = false;
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

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = !Enabled ? Ui.Track : (hover ? Ui.Track : Ui.Card);
            Color border = !Enabled ? Ui.Border : (hover ? Ui.BorderStrong : Ui.Border);
            Color ink = !Enabled ? Ui.Faint : (hover ? Ui.Text : Ui.Muted);
            if (down && Enabled) fill = Ui.AccentSoft;

            Ui.FillRound(g, r, Ui.P(9), fill);
            Ui.StrokeRound(g, r, Ui.P(9), border, 1f);

            float cx = Width / 2f;
            float cy = Height / 2f;
            float unit = Math.Min(Width, Height) * 0.30f;

            using (Pen pen = new Pen(ink, Math.Max(1.4f, Ui.P(1.5))))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                if (Kind == IconKind.Mouse)
                {
                    float w = unit * 1.02f;
                    float h = unit * 1.58f;
                    Rectangle body = Rectangle.Round(
                        new RectangleF(cx - w / 2f, cy - h / 2f, w, h));
                    using (GraphicsPath path = Ui.Round(body, (int)Math.Max(4, w / 2f)))
                    {
                        g.DrawPath(pen, path);
                    }

                    float top = cy - h / 2f;
                    g.DrawLine(pen, cx, top + 1, cx, cy - unit * 0.06f);
                    g.DrawLine(pen, cx - unit * 0.18f, top + h * 0.30f,
                        cx + unit * 0.18f, top + h * 0.30f);
                }
                else if (Kind == IconKind.Editor)
                {
                    // Ножницы — обрезка видео и фото.
                    float u = unit * 1.05f;
                    float ring = u * 0.27f;
                    g.DrawLine(pen,
                        cx - u * 0.34f, cy + u * 0.55f,
                        cx + u * 0.55f, cy - u * 0.95f);
                    g.DrawLine(pen,
                        cx + u * 0.34f, cy + u * 0.55f,
                        cx - u * 0.55f, cy - u * 0.95f);
                    g.DrawEllipse(pen,
                        cx - u * 0.44f - ring, cy + u * 0.74f - ring,
                        ring * 2f, ring * 2f);
                    g.DrawEllipse(pen,
                        cx + u * 0.44f - ring, cy + u * 0.74f - ring,
                        ring * 2f, ring * 2f);
                }
                else if (Kind == IconKind.Frames)
                {
                    // Киноплёнка: три кадра и треугольник воспроизведения.
                    float frameW = unit * 0.62f;
                    float frameH = unit * 1.16f;
                    float gap = unit * 0.16f;
                    float left = cx - (frameW * 3f + gap * 2f) / 2f;
                    for (int i = 0; i < 3; i++)
                    {
                        float x = left + i * (frameW + gap);
                        g.DrawRectangle(pen, x, cy - frameH / 2f, frameW, frameH);
                    }
                    float mx = left + frameW + gap;
                    using (SolidBrush play = new SolidBrush(ink))
                    {
                        PointF[] triangle = new PointF[]
                        {
                            new PointF(mx + frameW * 0.30f, cy - frameH * 0.22f),
                            new PointF(mx + frameW * 0.30f, cy + frameH * 0.22f),
                            new PointF(mx + frameW * 0.72f, cy)
                        };
                        g.FillPolygon(play, triangle);
                    }
                }
                else if (Kind == IconKind.Download)
                {
                    float top = cy - unit * 0.85f;
                    float bottom = cy + unit * 0.42f;
                    g.DrawLine(pen, cx, top, cx, bottom);
                    g.DrawLine(pen, cx - unit * 0.52f, bottom - unit * 0.44f, cx, bottom);
                    g.DrawLine(pen, cx + unit * 0.52f, bottom - unit * 0.44f, cx, bottom);

                    float tray = unit * 0.62f;
                    float trayY = cy + unit * 0.85f;
                    g.DrawLine(pen, cx - tray, trayY - unit * 0.28f, cx - tray, trayY);
                    g.DrawLine(pen, cx - tray, trayY, cx + tray, trayY);
                    g.DrawLine(pen, cx + tray, trayY - unit * 0.28f, cx + tray, trayY);
                }
                else if (Kind == IconKind.Discord)
                {
                    // Оригинальный логотип Discord из ресурса, перекрашенный под тему.
                    Color mark = !Enabled ? Ui.Faint : (hover ? Ui.AccentInk : Ui.Accent);
                    float size = Math.Min(Width, Height) * 0.84f;
                    // Логотип ровно по центру кнопки.
                    DiscordMark.Draw(g,
                        new RectangleF(cx - size / 2f, cy - size / 2f, size, size),
                        mark);
                }
                else if (Kind == IconKind.Donate)
                {
                    float u = unit * 1.28f;
                    using (GraphicsPath heart = new GraphicsPath())
                    {
                        PointF tip = new PointF(cx, cy + u * 0.98f);
                        PointF left = new PointF(cx - u * 0.95f, cy - u * 0.02f);
                        PointF cleft = new PointF(cx, cy - u * 0.24f);
                        PointF right = new PointF(cx + u * 0.95f, cy - u * 0.02f);

                        // Левая сторона: остриё → левое плечо.
                        heart.AddBezier(tip,
                            new PointF(cx - u * 0.48f, cy + u * 0.62f),
                            new PointF(cx - u * 1.00f, cy + u * 0.28f),
                            left);
                        // Левая доля: плечо → выемка.
                        heart.AddBezier(left,
                            new PointF(cx - u * 0.92f, cy - u * 0.62f),
                            new PointF(cx - u * 0.42f, cy - u * 0.82f),
                            cleft);
                        // Правая доля: выемка → правое плечо.
                        heart.AddBezier(cleft,
                            new PointF(cx + u * 0.42f, cy - u * 0.82f),
                            new PointF(cx + u * 0.92f, cy - u * 0.62f),
                            right);
                        // Правая сторона: плечо → остриё.
                        heart.AddBezier(right,
                            new PointF(cx + u * 1.00f, cy + u * 0.28f),
                            new PointF(cx + u * 0.48f, cy + u * 0.62f),
                            tip);
                        heart.CloseFigure();

                        // Ставим сердечко ровно по центру кнопки по его видимым границам.
                        using (GraphicsPath measured = (GraphicsPath)heart.Clone())
                        {
                            measured.Flatten(null);
                            RectangleF box = measured.GetBounds();
                            using (Matrix shift = new Matrix())
                            {
                                shift.Translate(
                                    cx - (box.Left + box.Width / 2f),
                                    cy - (box.Top + box.Height / 2f));
                                heart.Transform(shift);
                            }
                        }

                        using (SolidBrush heartBrush = new SolidBrush(ink))
                        {
                            g.FillPath(heartBrush, heart);
                        }
                    }
                }
                else
                {
                    float outer = unit * 0.98f;
                    float inner = unit * 0.70f;
                    PointF[] gear = new PointF[32];
                    for (int i = 0; i < 32; i++)
                    {
                        double a = -Math.PI / 2 + Math.PI * 2 / 32 * i;
                        float radius = (i % 4 < 2) ? outer : inner;
                        gear[i] = new PointF(
                            cx + (float)(Math.Cos(a) * radius),
                            cy + (float)(Math.Sin(a) * radius));
                    }
                    using (SolidBrush inkBrush = new SolidBrush(ink))
                    {
                        g.FillPolygon(inkBrush, gear);
                    }
                    float hole = unit * 0.30f;
                    using (SolidBrush holeBrush = new SolidBrush(fill))
                    {
                        g.FillEllipse(holeBrush, cx - hole, cy - hole, hole * 2, hole * 2);
                    }
                }
            }
        }
    }

    internal sealed class InfoBadge : Control
    {
        public string InfoText = "";

        private bool hover;

        public InfoBadge()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
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
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            Color color = hover ? Ui.Text : Ui.Faint;
            if (hover)
            {
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(26, Ui.Text)))
                {
                    g.FillEllipse(fill, r);
                }
            }
            using (Pen pen = new Pen(color, Math.Max(1.4f, Ui.P(1.5))))
            {
                g.DrawEllipse(pen, r);
            }

            // Чёткий и ровно отцентрованный «!».
            float cx = r.X + r.Width / 2f;
            float barWidth = Math.Max(1.8f, Width * 0.10f);
            float dot = Math.Max(1.6f, Width * 0.09f);
            float gap = Math.Max(1.2f, Width * 0.06f);
            float barLength = Math.Max(6f, Width * 0.34f);
            float total = barLength + gap + dot;
            float top = r.Y + r.Height / 2f - total / 2f;

            using (Pen mark = new Pen(color, barWidth))
            {
                mark.StartCap = LineCap.Round;
                mark.EndCap = LineCap.Round;
                g.DrawLine(mark, cx, top + barWidth / 2f,
                    cx, top + barLength - barWidth / 2f);
            }
            using (SolidBrush brush = new SolidBrush(color))
            {
                float dotY = top + barLength + gap + dot / 2f;
                g.FillEllipse(brush, cx - dot / 2f, dotY - dot / 2f, dot, dot);
            }
        }
    }

    /// <summary>Небольшое окно с авторами, открывается по клику на «!».</summary>
    internal sealed class CreditsForm : Form
    {
        public CreditsForm(string text)
        {
            BackColor = Ui.Card;
            Font = Ui.Base;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            ClientSize = new Size(Ui.P(392), Ui.P(136));

            Controls.Add(MakeLabel("Авторы", Ui.H2, Ui.Text, 16, 10, 356, 26));

            string[] lines = (text ?? "").Split(new char[] { '\n' });
            int y = 40;
            for (int i = 0; i < lines.Length && i < 4; i++)
            {
                Controls.Add(MakeLabel(lines[i].Trim(), Ui.Small, Ui.Muted,
                    16, y, 360, 20));
                y += 22;
            }

            Deactivate += delegate { Close(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
            };
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

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Ui.Card);
            if (Ui.Gamesense) Ui.DrawCarbon(e.Graphics, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.StrokeRound(g, r, Ui.P(10), Ui.Border, 1.1f);
            if (Ui.Gamesense)
            {
                Rectangle strip = new Rectangle(0, 0, Width, Math.Max(2, Ui.P(3)));
                using (LinearGradientBrush brush = Ui.RainbowBrush(strip, 0.12, 8))
                {
                    g.FillRectangle(brush, strip);
                }
            }
        }
    }

    internal sealed class ToggleRow : UiControl
    {
        private bool isChecked;

        public string Title = "";
        public string Subtitle = "";

        public event EventHandler CheckedChanged;

        public ToggleRow()
        {
            Height = Ui.P(36);
        }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Invalidate();
                EventHandler h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int box = Ui.P(18);
            int y = Subtitle.Length > 0 ? Ui.P(2) : (Height - box) / 2;
            Rectangle r = new Rectangle(0, y, box, box);
            if (isChecked)
            {
                Ui.FillRound(g, r, Ui.P(5), Ui.Accent);
                Point[] check = new Point[]
                {
                    new Point(r.X + Ui.P(4), r.Y + Ui.P(9)),
                    new Point(r.X + Ui.P(8), r.Y + Ui.P(13)),
                    new Point(r.X + Ui.P(14), r.Y + Ui.P(5))
                };
                using (Pen pen = new Pen(Ui.OnAccent, Ui.P(2)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, check);
                }
            }
            else
            {
                Ui.FillRound(g, r, Ui.P(5), Ui.Card);
                Ui.StrokeRound(g, r, Ui.P(5), Ui.BorderStrong, 1.2f);
            }

            int tx = box + Ui.P(10);
            if (Subtitle.Length > 0)
            {
                Ui.Write(g, Title, Ui.Base, new Rectangle(tx, Ui.P(-1), Width - tx, Ui.P(20)), Ui.Text, Ui.LeftMid);
                Ui.Write(g, Subtitle, Ui.Tiny, new Rectangle(tx, Ui.P(18), Width - tx, Ui.P(16)), Ui.Faint, Ui.LeftMid);
            }
            else
            {
                Ui.Write(g, Title, Ui.Base, new Rectangle(tx, 0, Width - tx, Height), Ui.Text, Ui.LeftMid);
            }
        }
    }

    internal sealed class ColorSlider : UiControl
    {
        private int value = 256;
        private bool dragging;

        public int Minimum = 2;
        public int Maximum = 256;
        public int Step = 2;

        public event EventHandler ValueChanged;

        public ColorSlider()
        {
            Height = Ui.P(22);
        }

        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(Minimum, Math.Min(Maximum, value));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
                EventHandler h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        private int ThumbX
        {
            get
            {
                int pad = Ui.P(8);
                double ratio = (double)(value - Minimum) / Math.Max(1, Maximum - Minimum);
                return pad + (int)Math.Round(ratio * (Width - pad * 2));
            }
        }

        private void SetFromX(int x)
        {
            int pad = Ui.P(8);
            double ratio = (double)(x - pad) / Math.Max(1, Width - pad * 2);
            ratio = Math.Max(0, Math.Min(1, ratio));
            int raw = Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
            int steps = (int)Math.Round((double)(raw - Minimum) / Step);
            int v = Minimum + steps * Step;
            Value = v;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                SetFromX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) SetFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = Height / 2;
            int pad = Ui.P(8);
            Rectangle track = new Rectangle(pad, cy - Ui.P(2), Width - pad * 2, Ui.P(4));
            Ui.FillRound(g, track, Ui.P(2), Ui.Track);

            int tx = ThumbX;
            Rectangle fill = new Rectangle(track.X, track.Y, Math.Max(0, tx - track.X), track.Height);
            if (fill.Width > 2)
            {
                Ui.FillRound(g, fill, Ui.P(2), Ui.Accent);
            }

            int thumb = Ui.P(14);
            Rectangle knob = new Rectangle(tx - thumb / 2, cy - thumb / 2, thumb, thumb);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            {
                g.FillEllipse(shadow, knob.X, knob.Y + Ui.P(1), knob.Width, knob.Height);
            }
            using (SolidBrush knobBrush = new SolidBrush(Ui.Card))
            {
                g.FillEllipse(knobBrush, knob);
            }
            using (Pen pen = new Pen(Ui.Accent, Ui.P(2)))
            {
                g.DrawEllipse(pen, knob.X + 1, knob.Y + 1, knob.Width - 2, knob.Height - 2);
            }
        }
    }

    internal sealed class SlimBar : UiControl
    {
        private int value;

        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(0, Math.Min(100, value));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
            }
        }

        public SlimBar()
        {
            Height = Ui.P(8);
            Cursor = Cursors.Default;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Width, Height);
            Ui.FillRound(g, track, Height / 2, Ui.Track);
            int w = (int)Math.Round(Width * (value / 100.0));
            if (w > 0)
            {
                Rectangle fill = new Rectangle(0, 0, Math.Max(Height, w), Height);
                using (GraphicsPath path = Ui.Round(fill, Height / 2))
                using (SolidBrush brush = new SolidBrush(Ui.Accent))
                {
                    g.FillPath(brush, path);
                }
            }
        }
    }

    internal sealed class FlatInput : Panel
    {
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public readonly TextBox Box;
        private string placeholder = "";
        private bool focused;

        public FlatInput()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Card;

            Box = new TextBox();
            Box.BorderStyle = BorderStyle.None;
            Box.Font = Ui.Base;
            Box.BackColor = Ui.Card;
            Box.ForeColor = Ui.Text;
            Box.TextAlign = HorizontalAlignment.Left;
            Controls.Add(Box);

            Box.GotFocus += delegate { focused = true; Invalidate(); };
            Box.LostFocus += delegate { focused = false; Invalidate(); };
            Box.HandleCreated += delegate { ApplyPlaceholder(); };
            Cursor = Cursors.IBeam;
        }

        public string Value
        {
            get { return Box.Text; }
            set { Box.Text = value; }
        }

        public string Placeholder
        {
            get { return placeholder; }
            set
            {
                placeholder = value ?? "";
                ApplyPlaceholder();
            }
        }

        public void ApplyTheme()
        {
            BackColor = Ui.Card;
            Box.BackColor = Ui.Card;
            Box.ForeColor = Ui.Text;
            Invalidate();
        }

        private void ApplyPlaceholder()
        {
            if (Box.IsHandleCreated && placeholder.Length > 0)
            {
                SendMessage(Box.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 4 && Height > 4)
            {
                using (GraphicsPath path = Ui.Round(new Rectangle(0, 0, Width, Height), Ui.P(9)))
                {
                    Region = new Region(path);
                }
            }
            int pad = Ui.P(11);
            Box.SetBounds(pad, Math.Max(0, (Height - Box.Height) / 2), Math.Max(10, Width - pad * 2), Box.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.StrokeRound(e.Graphics, r, Ui.P(9), focused ? Ui.AccentLine : Ui.Border, focused ? 1.6f : 1f);
        }
    }
}
