using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Video2Gif
{
    internal enum ThemeKind
    {
        Light,
        Dark,
        NewYear,
        Halloween,
        Gta6,
        Gamesense
    }

    internal static class Ui
    {
        public static readonly double S = DetectScale();

        public static bool Dark;
        public static ThemeKind Theme = ThemeKind.Dark;

        /// <summary>Тема выбирается автоматически по дате Windows.</summary>
        public static bool AutoTheme;

        public static bool Holiday
        {
            get { return Theme == ThemeKind.NewYear || Theme == ThemeKind.Halloween; }
        }

        public static bool Gta6
        {
            get { return Theme == ThemeKind.Gta6; }
        }

        public static bool Gamesense
        {
            get { return Theme == ThemeKind.Gamesense; }
        }

        public static bool NewYear
        {
            get { return Theme == ThemeKind.NewYear; }
        }

        public static bool Halloween
        {
            get { return Theme == ThemeKind.Halloween; }
        }

        public static string ThemeName
        {
            get
            {
                if (Theme == ThemeKind.Light) return "light";
                if (Theme == ThemeKind.NewYear) return "newyear";
                if (Theme == ThemeKind.Halloween) return "halloween";
                if (Theme == ThemeKind.Gta6) return "gta6";
                if (Theme == ThemeKind.Gamesense) return "gamesense";
                return "dark";
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static int ColorRef(Color color)
        {
            return color.R | (color.G << 8) | (color.B << 16);
        }

        /// <summary>Яркий цвет радуги: hue 0–1, saturation/value 0–1.</summary>
        public static Color Rainbow(double hue, double saturation, double value)
        {
            hue -= Math.Floor(hue);
            double h = hue * 6.0;
            int sector = (int)h;
            double f = h - sector;
            double p = value * (1.0 - saturation);
            double q = value * (1.0 - saturation * f);
            double t = value * (1.0 - saturation * (1.0 - f));
            double r;
            double g;
            double b;
            switch (sector % 6)
            {
                case 0: r = value; g = t; b = p; break;
                case 1: r = q; g = value; b = p; break;
                case 2: r = p; g = value; b = t; break;
                case 3: r = p; g = q; b = value; break;
                case 4: r = t; g = p; b = value; break;
                default: r = value; g = p; b = q; break;
            }
            return Color.FromArgb(ClampByte(r), ClampByte(g), ClampByte(b));
        }

        private static int ClampByte(double value)
        {
            int result = (int)Math.Round(value * 255.0);
            if (result < 0) return 0;
            if (result > 255) return 255;
            return result;
        }

        /// <summary>Горизонтальный радужный градиент со сдвигом фазы.</summary>
        public static LinearGradientBrush RainbowBrush(Rectangle rect, double offset, int steps)
        {
            if (steps < 2) steps = 2;
            ColorBlend blend = new ColorBlend(steps);
            Color[] colors = new Color[steps];
            float[] positions = new float[steps];
            for (int i = 0; i < steps; i++)
            {
                colors[i] = Rainbow(offset + (double)i / steps, 0.72, 1.0);
                positions[i] = (float)i / (steps - 1);
            }
            blend.Colors = colors;
            blend.Positions = positions;
            LinearGradientBrush brush = new LinearGradientBrush(
                rect, colors[0], colors[steps - 1], 0f);
            brush.InterpolationColors = blend;
            return brush;
        }

        /// <summary>Лёгкая текстура карбона: тонкая диагональная сетка.</summary>
        public static void DrawCarbon(Graphics g, Rectangle bounds)
        {
            int step = Math.Max(4, P(6));
            using (Pen light = new Pen(Color.FromArgb(13, 255, 255, 255), 1f))
            using (Pen dark = new Pen(Color.FromArgb(24, 0, 0, 0), 1f))
            {
                for (int x = bounds.Left - bounds.Height; x < bounds.Right; x += step)
                {
                    g.DrawLine(light, x, bounds.Top,
                        x + bounds.Height, bounds.Bottom);
                }
                for (int x = bounds.Left; x < bounds.Right + bounds.Height; x += step)
                {
                    g.DrawLine(dark, x, bounds.Top,
                        x - bounds.Height, bounds.Bottom);
                }
            }
        }

        public static void ApplyTitleBar(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            int dark = Dark ? 1 : 0;
            TryAttribute(hwnd, 20, dark);
            TryAttribute(hwnd, 19, dark);
            TryAttribute(hwnd, 35, ColorRef(Bg));
            TryAttribute(hwnd, 36, ColorRef(Text));
            TryAttribute(hwnd, 34, ColorRef(Border));
        }

        private static void TryAttribute(IntPtr hwnd, int attribute, int value)
        {
            try { DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)); } catch { }
        }

        public static Color Bg;
        public static Color Card;
        public static Color Border;
        public static Color BorderStrong;
        public static Color Text;
        public static Color Muted;
        public static Color Faint;
        public static Color Accent;
        public static Color AccentDark;
        public static Color AccentInk;
        public static Color AccentSoft;
        public static Color AccentLine;
        public static Color AccentWash;
        public static Color Track;
        public static Color Good;
        public static Color Bad;
        public static Color OnAccent;

        static Ui()
        {
            ApplyColors(ThemeKind.Dark);
        }

        public static void SetTheme(string name)
        {
            ThemeKind kind = ThemeKind.Dark;
            string value = (name ?? "").Trim().ToLowerInvariant();
            if (value == "auto") { SetThemeAuto(DateTime.Now); return; }
            if (value == "light") kind = ThemeKind.Light;
            else if (value == "newyear" || value == "new-year" || value == "ny") kind = ThemeKind.NewYear;
            else if (value == "halloween" || value == "spooky") kind = ThemeKind.Halloween;
            else if (value == "gta6" || value == "gta" || value == "gta-6") kind = ThemeKind.Gta6;
            else if (value == "gamesense" || value == "game-sense" ||
                     value == "game sense" || value == "gs") kind = ThemeKind.Gamesense;
            SetTheme(kind);
        }

        public static void SetTheme(ThemeKind kind)
        {
            AutoTheme = false;
            ApplyTheme(kind);
        }

        public static void SetThemeAuto(DateTime now)
        {
            ApplyTheme(SeasonalTheme(now));
            AutoTheme = true;
        }

        /// <summary>Переключает сезонную тему, если включён авторежим.</summary>
        public static bool RefreshAutoTheme(DateTime now)
        {
            if (!AutoTheme) return false;
            ThemeKind kind = SeasonalTheme(now);
            if (kind == Theme) return false;
            ApplyTheme(kind);
            return true;
        }

        /// <summary>
        /// Хэллоуин — с 1 октября по 1 ноября, Новый год — с 1 декабря
        /// по 1 января, а тема GTA 6 — с 18 ноября 15:00 по 20 ноября 00:00
        /// (границы включительно). В остальное время — тёмная.
        /// </summary>
        public static ThemeKind SeasonalTheme(DateTime date)
        {
            if (Gta6Window(date))
                return ThemeKind.Gta6;
            if (date.Month == 10 || (date.Month == 11 && date.Day == 1))
                return ThemeKind.Halloween;
            if (date.Month == 12 || (date.Month == 1 && date.Day == 1))
                return ThemeKind.NewYear;
            return ThemeKind.Dark;
        }

        /// <summary>
        /// Праздничное окно темы GTA 6: с 18 ноября 2026, 15:00
        /// до 20 ноября 2026, 00:00 (конец не включается).
        /// </summary>
        public static bool Gta6Window(DateTime date)
        {
            DateTime start = new DateTime(2026, 11, 18, 15, 0, 0);
            DateTime end = new DateTime(2026, 11, 20, 0, 0, 0);
            return date >= start && date < end;
        }

        private static void ApplyTheme(ThemeKind kind)
        {
            Theme = kind;
            Dark = kind != ThemeKind.Light;
            ApplyColors(kind);
        }

        public static void SetDark(bool dark)
        {
            SetTheme(dark ? ThemeKind.Dark : ThemeKind.Light);
        }

        private static void ApplyColors(ThemeKind kind)
        {
            if (kind == ThemeKind.Light)
            {
                Bg = Color.FromArgb(247, 247, 248);
                Card = Color.White;
                Border = Color.FromArgb(232, 232, 235);
                BorderStrong = Color.FromArgb(201, 201, 206);
                Text = Color.FromArgb(20, 20, 20);
                Muted = Color.FromArgb(111, 111, 118);
                Faint = Color.FromArgb(160, 160, 167);
                Accent = Color.FromArgb(23, 23, 23);
                AccentDark = Color.Black;
                AccentInk = Color.FromArgb(23, 23, 23);
                AccentSoft = Color.FromArgb(240, 240, 241);
                AccentLine = Color.FromArgb(214, 214, 217);
                AccentWash = Color.FromArgb(247, 247, 248);
                Track = Color.FromArgb(237, 237, 239);
                Good = Color.FromArgb(16, 148, 104);
                Bad = Color.FromArgb(214, 48, 49);
                OnAccent = Color.White;
            }
            else if (kind == ThemeKind.NewYear)
            {
                Bg = Color.FromArgb(9, 21, 38);
                Card = Color.FromArgb(16, 34, 56);
                Border = Color.FromArgb(31, 57, 86);
                BorderStrong = Color.FromArgb(60, 96, 133);
                Text = Color.FromArgb(240, 247, 255);
                Muted = Color.FromArgb(157, 181, 206);
                Faint = Color.FromArgb(109, 134, 161);
                Accent = Color.FromArgb(126, 200, 255);
                AccentDark = Color.FromArgb(90, 175, 240);
                AccentInk = Color.FromArgb(126, 200, 255);
                AccentSoft = Color.FromArgb(24, 50, 78);
                AccentLine = Color.FromArgb(49, 86, 122);
                AccentWash = Color.FromArgb(15, 34, 55);
                Track = Color.FromArgb(23, 45, 70);
                Good = Color.FromArgb(104, 226, 168);
                Bad = Color.FromArgb(255, 120, 120);
                OnAccent = Color.FromArgb(7, 19, 33);
            }
            else if (kind == ThemeKind.Halloween)
            {
                Bg = Color.FromArgb(18, 12, 24);
                Card = Color.FromArgb(29, 19, 38);
                Border = Color.FromArgb(54, 38, 68);
                BorderStrong = Color.FromArgb(86, 62, 104);
                Text = Color.FromArgb(246, 238, 255);
                Muted = Color.FromArgb(180, 160, 198);
                Faint = Color.FromArgb(126, 107, 144);
                Accent = Color.FromArgb(255, 140, 66);
                AccentDark = Color.FromArgb(232, 110, 38);
                AccentInk = Color.FromArgb(255, 140, 66);
                AccentSoft = Color.FromArgb(45, 29, 55);
                AccentLine = Color.FromArgb(80, 54, 94);
                AccentWash = Color.FromArgb(34, 22, 44);
                Track = Color.FromArgb(41, 28, 52);
                Good = Color.FromArgb(118, 224, 160);
                Bad = Color.FromArgb(255, 92, 92);
                OnAccent = Color.FromArgb(26, 14, 8);
            }
            else if (kind == ThemeKind.Gta6)
            {
                Bg = Color.FromArgb(10, 9, 12);
                Card = Color.FromArgb(22, 17, 27);
                Border = Color.FromArgb(45, 35, 55);
                BorderStrong = Color.FromArgb(78, 58, 92);
                Text = Color.FromArgb(250, 247, 252);
                Muted = Color.FromArgb(190, 176, 205);
                Faint = Color.FromArgb(132, 118, 146);
                Accent = Color.FromArgb(255, 79, 163);
                AccentDark = Color.FromArgb(226, 58, 138);
                AccentInk = Color.FromArgb(255, 143, 198);
                AccentSoft = Color.FromArgb(46, 26, 47);
                AccentLine = Color.FromArgb(96, 52, 92);
                AccentWash = Color.FromArgb(30, 18, 34);
                Track = Color.FromArgb(40, 30, 46);
                Good = Color.FromArgb(74, 222, 128);
                Bad = Color.FromArgb(255, 107, 107);
                OnAccent = Color.White;
            }
            else if (kind == ThemeKind.Gamesense)
            {
                Bg = Color.FromArgb(18, 18, 20);
                Card = Color.FromArgb(24, 24, 27);
                Border = Color.FromArgb(50, 50, 55);
                BorderStrong = Color.FromArgb(84, 84, 90);
                Text = Color.FromArgb(235, 235, 237);
                Muted = Color.FromArgb(168, 168, 173);
                Faint = Color.FromArgb(120, 120, 125);
                Accent = Color.FromArgb(88, 178, 124);
                AccentDark = Color.FromArgb(60, 132, 92);
                AccentInk = Color.FromArgb(146, 204, 172);
                AccentSoft = Color.FromArgb(24, 34, 28);
                AccentLine = Color.FromArgb(52, 76, 60);
                AccentWash = Color.FromArgb(20, 28, 22);
                Track = Color.FromArgb(44, 44, 48);
                Good = Color.FromArgb(88, 178, 124);
                Bad = Color.FromArgb(232, 96, 96);
                OnAccent = Color.FromArgb(10, 12, 10);
            }
            else
            {
                Bg = Color.FromArgb(14, 14, 15);
                Card = Color.FromArgb(24, 24, 26);
                Border = Color.FromArgb(43, 43, 46);
                BorderStrong = Color.FromArgb(69, 69, 74);
                Text = Color.FromArgb(242, 242, 243);
                Muted = Color.FromArgb(160, 160, 168);
                Faint = Color.FromArgb(110, 110, 118);
                Accent = Color.FromArgb(242, 242, 243);
                AccentDark = Color.White;
                AccentInk = Color.FromArgb(242, 242, 243);
                AccentSoft = Color.FromArgb(42, 42, 43);
                AccentLine = Color.FromArgb(69, 69, 71);
                AccentWash = Color.FromArgb(32, 32, 33);
                Track = Color.FromArgb(38, 38, 41);
                Good = Color.FromArgb(52, 211, 153);
                Bad = Color.FromArgb(248, 113, 113);
                OnAccent = Color.FromArgb(17, 17, 17);
            }
        }

        public static void ApplyTheme(Control root)
        {
            Form form = root as Form;
            if (form != null) form.BackColor = Bg;

            Card card = root as Card;
            if (card != null) card.BackColor = Card;

            string tag = root.Tag as string;
            if (tag == "surface") root.BackColor = Card;

            FlatInput input = root as FlatInput;
            if (input != null) input.ApplyTheme();

            Label label = root as Label;
            if (label != null && label.Tag is string) label.ForeColor = RoleColor((string)label.Tag);

            root.Invalidate();
            foreach (Control child in root.Controls) ApplyTheme(child);
        }

        public static string RoleOf(Color color)
        {
            if (color.ToArgb() == Text.ToArgb()) return "text";
            if (color.ToArgb() == Muted.ToArgb()) return "muted";
            if (color.ToArgb() == Faint.ToArgb()) return "faint";
            if (color.ToArgb() == Accent.ToArgb()) return "accent";
            if (color.ToArgb() == AccentInk.ToArgb()) return "accent";
            if (color.ToArgb() == AccentDark.ToArgb()) return "accent";
            return "text";
        }

        private static Color RoleColor(string role)
        {
            if (role == "muted") return Muted;
            if (role == "faint") return Faint;
            if (role == "accent") return Accent;
            return Text;
        }

        public static readonly Font Base = new Font("Segoe UI", 9.5f);
        public static readonly Font Small = new Font("Segoe UI", 8.75f);
        public static readonly Font Tiny = new Font("Segoe UI", 8f);
        public static readonly Font Bold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font Semi = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        public static readonly Font H2 = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        public static readonly Font H1 = new Font("Segoe UI", 15f, FontStyle.Bold);
        public static readonly Font Logo = new Font("Segoe UI", 14f, FontStyle.Bold);
        public static readonly Font GtaMark = new Font("Segoe UI", 38f, FontStyle.Bold);

        /* ------------------------------------------------- Тема GTA 6 (неон) */

        /// <summary>Неоновый градиент темы GTA 6: сине-фиолетовый → пурпурный → розовый → оранжевый.</summary>
        public static ColorBlend GtaBlend()
        {
            ColorBlend blend = new ColorBlend(5);
            blend.Colors = new Color[]
            {
                Color.FromArgb(255, 96, 92, 240),
                Color.FromArgb(255, 150, 48, 245),
                Color.FromArgb(255, 255, 79, 163),
                Color.FromArgb(255, 255, 122, 84),
                Color.FromArgb(255, 255, 158, 66)
            };
            blend.Positions = new float[] { 0f, 0.28f, 0.55f, 0.78f, 1f };
            return blend;
        }

        public static LinearGradientBrush GtaGradient(Rectangle bounds, float angle)
        {
            LinearGradientBrush brush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(255, 96, 92, 240),
                Color.FromArgb(255, 255, 158, 66),
                angle);
            brush.InterpolationColors = GtaBlend();
            return brush;
        }

        private static double DetectScale()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return Math.Max(1.0, Math.Min(3.0, g.DpiX / 96.0));
                }
            }
            catch
            {
                return 1.0;
            }
        }

        public static int P(double value)
        {
            return (int)Math.Round(value * S);
        }

        public static GraphicsPath Round(Rectangle rect, int radius)
        {
            if (Gamesense && radius > 0)
            {
                // В Gamesense всё выглядит почти прямоугольным.
                radius = Math.Min(radius, Math.Max(1, P(2)));
            }

            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = Math.Max(2, radius * 2);
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle rect, int radius, Color color)
        {
            using (GraphicsPath path = Round(rect, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void StrokeRound(Graphics g, Rectangle rect, int radius, Color color, float width)
        {
            using (GraphicsPath path = Round(rect, radius))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        public static void Write(Graphics g, string text, Font font, Rectangle bounds, Color color,
            TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, bounds, color, flags);
        }

        public const TextFormatFlags LeftMid =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

        public const TextFormatFlags CenterMid =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
            TextFormatFlags.SingleLine;

        /* ------------------------------------------------------ Праздничные узоры */

        public static void DrawSnowflake(Graphics g, float cx, float cy, float radius, Color color, float width)
        {
            using (Pen pen = new Pen(color, Math.Max(1f, width)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                for (int i = 0; i < 6; i++)
                {
                    double angle = Math.PI / 3.0 * i;
                    float x = cx + (float)(Math.Cos(angle) * radius);
                    float y = cy + (float)(Math.Sin(angle) * radius);
                    g.DrawLine(pen, cx, cy, x, y);

                    float bx = cx + (float)(Math.Cos(angle) * radius * 0.62);
                    float by = cy + (float)(Math.Sin(angle) * radius * 0.62);
                    float branch = radius * 0.34f;
                    double a1 = angle + Math.PI / 4.0;
                    double a2 = angle - Math.PI / 4.0;
                    g.DrawLine(pen, bx, by,
                        bx + (float)(Math.Cos(a1) * branch), by + (float)(Math.Sin(a1) * branch));
                    g.DrawLine(pen, bx, by,
                        bx + (float)(Math.Cos(a2) * branch), by + (float)(Math.Sin(a2) * branch));
                }
            }
        }

        public static void DrawLeaf(Graphics g, float cx, float cy, float size, Color color, float angle)
        {
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(cx, cy);
                g.RotateTransform(angle);

                float w = size * 0.62f;
                float h = size;
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddBezier(0, h / 2f, -w / 2f, h * 0.12f, -w / 2f, -h * 0.32f, 0, -h / 2f);
                    path.AddBezier(0, -h / 2f, w / 2f, -h * 0.32f, w / 2f, h * 0.12f, 0, h / 2f);
                    path.CloseFigure();

                    using (SolidBrush brush = new SolidBrush(color))
                    {
                        g.FillPath(brush, path);
                    }

                    int veinAlpha = Math.Min(255, color.A + 34);
                    using (Pen pen = new Pen(Color.FromArgb(veinAlpha, color), Math.Max(1f, size * 0.045f)))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, 0, h * 0.42f, 0, -h * 0.42f);
                        g.DrawLine(pen, 0, 0, w * 0.30f, -h * 0.18f);
                        g.DrawLine(pen, 0, 0, -w * 0.30f, -h * 0.18f);
                    }
                }

                using (Pen stem = new Pen(color, Math.Max(1f, size * 0.05f)))
                {
                    stem.StartCap = LineCap.Round;
                    stem.EndCap = LineCap.Round;
                    g.DrawLine(stem, 0, h * 0.45f, size * 0.10f, h * 0.64f);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        /// <summary>Силуэт пальмы для темы GTA 6: ствол и широкие листья.</summary>
        public static void DrawPalm(Graphics g, float x, float baseY, float height,
            Color color, float lean)
        {
            float topX = x + lean * height;
            float topY = baseY - height;
            float baseWidth = Math.Max(2.4f, height * 0.105f);
            float topWidth = Math.Max(1.6f, height * 0.05f);

            using (SolidBrush brush = new SolidBrush(color))
            {
                // Ствол — изогнутая сужающаяся фигура.
                using (GraphicsPath trunk = new GraphicsPath())
                {
                    trunk.AddBezier(
                        x - baseWidth / 2f, baseY,
                        x - baseWidth * 0.25f + lean * height * 0.3f, baseY - height * 0.42f,
                        topX - topWidth * 1.2f, topY + height * 0.20f,
                        topX - topWidth / 2f, topY);
                    trunk.AddBezier(
                        topX + topWidth / 2f, topY,
                        topX + topWidth * 1.2f, topY + height * 0.20f,
                        x + baseWidth * 0.25f + lean * height * 0.3f, baseY - height * 0.42f,
                        x + baseWidth / 2f, baseY);
                    trunk.CloseFigure();
                    g.FillPath(brush, trunk);
                }

                float reach = height * 0.42f;
                float leafWidth = Math.Max(3f, height * 0.14f);
                PointF crown = new PointF(topX, topY + height * 0.02f);
                PointF[] tips = new PointF[]
                {
                    new PointF(crown.X - reach, crown.Y - reach * 0.12f),
                    new PointF(crown.X - reach * 0.55f, crown.Y - reach * 0.82f),
                    new PointF(crown.X + reach * 0.55f, crown.Y - reach * 0.82f),
                    new PointF(crown.X + reach, crown.Y - reach * 0.12f),
                    new PointF(crown.X + reach * 0.55f, crown.Y + reach * 0.36f),
                    new PointF(crown.X - reach * 0.55f, crown.Y + reach * 0.36f)
                };

                for (int i = 0; i < tips.Length; i++)
                {
                    float dx = tips[i].X - crown.X;
                    float dy = tips[i].Y - crown.Y;
                    float length = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (length < 1f) continue;
                    float px = -dy / length * leafWidth;
                    float py = dx / length * leafWidth;

                    using (GraphicsPath leaf = new GraphicsPath())
                    {
                        leaf.AddBezier(crown,
                            new PointF(crown.X + dx * 0.32f + px, crown.Y + dy * 0.32f + py),
                            new PointF(crown.X + dx * 0.72f + px * 0.55f, crown.Y + dy * 0.72f + py * 0.55f),
                            tips[i]);
                        leaf.AddBezier(tips[i],
                            new PointF(crown.X + dx * 0.72f - px * 0.55f, crown.Y + dy * 0.72f - py * 0.55f),
                            new PointF(crown.X + dx * 0.32f - px, crown.Y + dy * 0.32f - py),
                            crown);
                        leaf.CloseFigure();
                        g.FillPath(brush, leaf);
                    }
                }

                // Основание кроны.
                float cap = Math.Max(2f, height * 0.05f);
                g.FillEllipse(brush, crown.X - cap, crown.Y - cap * 0.6f,
                    cap * 2f, cap * 1.2f);

                // Кокосы у основания кроны.
                float nut = Math.Max(1.4f, height * 0.045f);
                g.FillEllipse(brush, crown.X - nut * 1.6f, crown.Y - nut * 0.4f,
                    nut * 1.5f, nut * 1.5f);
                g.FillEllipse(brush, crown.X + nut * 0.2f, crown.Y - nut * 0.6f,
                    nut * 1.5f, nut * 1.5f);
            }
        }

        /// <summary>Маленькая чайка: две дуги.</summary>
        public static void DrawGull(Graphics g, float x, float y, float size, Color color)
        {
            using (Pen pen = new Pen(color, Math.Max(1f, size * 0.16f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddBezier(x - size, y,
                        x - size * 0.55f, y - size * 0.55f,
                        x - size * 0.12f, y - size * 0.55f,
                        x, y);
                    path.AddBezier(x, y,
                        x + size * 0.12f, y - size * 0.55f,
                        x + size * 0.55f, y - size * 0.55f,
                        x + size, y);
                    g.DrawPath(pen, path);
                }
            }
        }

        public static void DrawUpArrow(Graphics g, Rectangle r, Color color)
        {
            float cx = r.X + r.Width / 2f;
            float top = r.Y + r.Height * 0.24f;
            float bottom = r.Y + r.Height * 0.62f;
            float head = r.Width * 0.19f;

            using (Pen pen = new Pen(color, Math.Max(2f, P(2.1))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                g.DrawLine(pen, cx, bottom, cx, top);
                g.DrawLine(pen, cx, top, cx - head, top + head);
                g.DrawLine(pen, cx, top, cx + head, top + head);

                float left = cx - r.Width * 0.26f;
                float right = cx + r.Width * 0.26f;
                float trayY = r.Y + r.Height * 0.76f;
                g.DrawLine(pen, left, trayY - r.Height * 0.14f, left, trayY);
                g.DrawLine(pen, left, trayY, right, trayY);
                g.DrawLine(pen, right, trayY - r.Height * 0.14f, right, trayY);
            }
        }

        public static void DrawPlayGlyph(Graphics g, Rectangle r, Color color)
        {
            PointF[] triangle =
            {
                new PointF(r.X + r.Width * 0.38f, r.Y + r.Height * 0.28f),
                new PointF(r.X + r.Width * 0.38f, r.Y + r.Height * 0.72f),
                new PointF(r.X + r.Width * 0.74f, r.Y + r.Height * 0.50f)
            };
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPolygon(brush, triangle);
            }
        }

        public static void DrawPumpkin(Graphics g, Rectangle r, Color body, Color face)
        {
            using (SolidBrush brush = new SolidBrush(body))
            {
                g.FillEllipse(brush, r);
            }

            float ridge = Math.Max(1f, r.Width * 0.035f);
            using (Pen pen = new Pen(Color.FromArgb(70, face), ridge))
            {
                g.DrawArc(pen, r.X + r.Width / 4, r.Y + r.Height / 8,
                    r.Width / 2, r.Height * 3 / 4, 80, 200);
            }

            float stemW = Math.Max(2f, r.Width * 0.10f);
            using (Pen stem = new Pen(body, stemW))
            {
                stem.StartCap = LineCap.Round;
                stem.EndCap = LineCap.Round;
                g.DrawLine(stem, r.X + r.Width * 0.52f, r.Y + r.Height * 0.06f,
                    r.X + r.Width * 0.56f, r.Y - r.Height * 0.10f);
            }

            using (SolidBrush ink = new SolidBrush(face))
            {
                PointF[] left =
                {
                    new PointF(r.X + r.Width * 0.28f, r.Y + r.Height * 0.38f),
                    new PointF(r.X + r.Width * 0.44f, r.Y + r.Height * 0.38f),
                    new PointF(r.X + r.Width * 0.36f, r.Y + r.Height * 0.52f)
                };
                PointF[] right =
                {
                    new PointF(r.X + r.Width * 0.56f, r.Y + r.Height * 0.38f),
                    new PointF(r.X + r.Width * 0.72f, r.Y + r.Height * 0.38f),
                    new PointF(r.X + r.Width * 0.64f, r.Y + r.Height * 0.52f)
                };
                g.FillPolygon(ink, left);
                g.FillPolygon(ink, right);

                float my = r.Y + r.Height * 0.66f;
                PointF[] mouth =
                {
                    new PointF(r.X + r.Width * 0.30f, my),
                    new PointF(r.X + r.Width * 0.40f, my + r.Height * 0.10f),
                    new PointF(r.X + r.Width * 0.50f, my),
                    new PointF(r.X + r.Width * 0.60f, my + r.Height * 0.10f),
                    new PointF(r.X + r.Width * 0.70f, my)
                };
                using (Pen pen = new Pen(face, Math.Max(1.2f, r.Width * 0.05f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, mouth);
                }
            }
        }

        public static void DrawMotif(Graphics g, Rectangle r, Color body, Color face)
        {
            if (Theme == ThemeKind.NewYear)
            {
                DrawSnowflake(g, r.X + r.Width / 2f, r.Y + r.Height / 2f,
                    Math.Min(r.Width, r.Height) / 2f, body, Math.Max(1.4f, r.Width * 0.055f));
            }
            else if (Theme == ThemeKind.Halloween)
            {
                DrawPumpkin(g, r, body, face);
            }
        }
    }

    internal abstract class UiControl : Control
    {
        protected UiControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Ui.Base;
            Cursor = Cursors.Hand;
        }
    }

    internal sealed class Card : Panel
    {
        public Card()
        {
            BackColor = Ui.Card;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Ui.Base;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 4 && Height > 4)
            {
                using (GraphicsPath path = Ui.Round(new Rectangle(0, 0, Width, Height), Ui.P(12)))
                {
                    Region = new Region(path);
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Ui.Card);
            if (Ui.Gamesense) Ui.DrawCarbon(e.Graphics, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.StrokeRound(e.Graphics, r, Ui.P(12), Ui.Border, 1f);
        }
    }

    internal sealed class PillButton : UiControl
    {
        public bool Accent;
        public bool Ghost;

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

        public PillButton()
        {
            SetStyle(ControlStyles.Selectable, true);
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

        protected override void OnTextChanged(EventArgs e)
        {
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Ui.P(9);

            if (Ui.Gta6 && Accent && Enabled)
            {
                using (GraphicsPath path = Ui.Round(r, radius))
                using (LinearGradientBrush brush = Ui.GtaGradient(r, 18f))
                {
                    g.FillPath(brush, path);
                    if (hover || down)
                    {
                        using (SolidBrush veil = new SolidBrush(
                            Color.FromArgb(down ? 42 : 20, 0, 0, 0)))
                        {
                            g.FillPath(veil, path);
                        }
                    }
                    using (Pen pen = new Pen(Color.FromArgb(64, 255, 255, 255), 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
                Ui.Write(g, Text, Font, r, Color.White, Ui.CenterMid);
                return;
            }

            Color fill;
            Color border;
            Color fore;

            if (!Enabled)
            {
                fill = Ui.Track;
                border = Ui.Border;
                fore = Ui.Faint;
            }
            else if (Accent)
            {
                fill = hover ? Ui.AccentDark : (down ? Ui.AccentDark : Ui.Accent);
                border = fill;
                fore = Ui.OnAccent;
            }
            else if (Ghost)
            {
                fill = hover ? Ui.Track : Ui.Card;
                border = hover ? Ui.BorderStrong : Ui.Border;
                fore = Ui.Text;
            }
            else
            {
                fill = Active ? Ui.AccentSoft : (hover ? Ui.Track : Ui.Card);
                border = Active ? Ui.AccentLine : (hover ? Ui.BorderStrong : Ui.Border);
                fore = Active ? Ui.AccentInk : Ui.Muted;
            }

            if (down && Enabled && !Accent) fill = Ui.Track;

            Ui.FillRound(g, r, radius, fill);
            Ui.StrokeRound(g, r, radius, border, 1f);
            Ui.Write(g, Text, Font, r, fore, Ui.CenterMid);
        }
    }

    internal sealed class DropCard : UiControl
    {
        public bool Selected;
        public bool Highlight;
        public string FileName = "";
        public string FileMeta = "";
        public string BusyText = "";

        public event EventHandler PickFile;

        private bool hover;

        public DropCard()
        {
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
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

        protected override void OnClick(EventArgs e)
        {
            EventHandler h = PickFile;
            if (h != null) h(this, EventArgs.Empty);
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Ui.P(12);

            if (Ui.Gta6)
            {
                using (GraphicsPath fill = Ui.Round(new Rectangle(0, 0, Width, Height), radius))
                using (LinearGradientBrush brush = Ui.GtaGradient(
                    new Rectangle(0, 0, Width, Height), 24f))
                {
                    g.FillPath(brush, fill);
                }

                GraphicsState neon = g.Save();
                try
                {
                    using (GraphicsPath clip = Ui.Round(new Rectangle(0, 0, Width, Height), radius))
                    {
                        g.SetClip(clip);
                    }

                    // Затемнение слева, чтобы белый текст читался на градиенте.
                    Rectangle full = new Rectangle(0, 0, Width, Height);
                    using (LinearGradientBrush shade = new LinearGradientBrush(
                        full,
                        Color.FromArgb(224, 8, 6, 12),
                        Color.FromArgb(0, 8, 6, 12),
                        0f))
                    {
                        ColorBlend blend = new ColorBlend(4);
                        blend.Colors = new Color[]
                        {
                            Color.FromArgb(235, 8, 6, 12),
                            Color.FromArgb(205, 8, 6, 12),
                            Color.FromArgb(60, 8, 6, 12),
                            Color.FromArgb(0, 8, 6, 12)
                        };
                        blend.Positions = new float[] { 0f, 0.55f, 0.80f, 1f };
                        shade.InterpolationColors = blend;
                        g.FillRectangle(shade, full);
                    }

                    // Пальмы и чайки в стиле Vice City.
                    Ui.DrawPalm(g, Width * 0.52f, Height * 0.99f, Height * 0.94f,
                        Color.FromArgb(140, 18, 6, 28), -0.10f);
                    Ui.DrawPalm(g, Width * 0.63f, Height * 0.99f, Height * 0.74f,
                        Color.FromArgb(112, 18, 6, 28), 0.12f);
                    Ui.DrawGull(g, Width * 0.46f, Height * 0.22f, Ui.P(13),
                        Color.FromArgb(125, 255, 255, 255));
                    Ui.DrawGull(g, Width * 0.54f, Height * 0.13f, Ui.P(9),
                        Color.FromArgb(105, 255, 255, 255));
                    Ui.DrawGull(g, Width * 0.61f, Height * 0.28f, Ui.P(11),
                        Color.FromArgb(115, 255, 255, 255));

                    Ui.Write(g, "VI", Ui.GtaMark,
                        new Rectangle(Width - Ui.P(130), (Height - Ui.P(64)) / 2,
                            Ui.P(104), Ui.P(64)),
                        Color.FromArgb(104, 255, 255, 255),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                }
                finally
                {
                    g.Restore(neon);
                }
            }
            else
            {
                Ui.FillRound(g, r, radius, Ui.Card);
            }

            if (Ui.Gamesense)
            {
                GraphicsState texture = g.Save();
                using (GraphicsPath clip = Ui.Round(
                    new Rectangle(0, 0, Width, Height), radius))
                {
                    g.SetClip(clip);
                }
                Ui.DrawCarbon(g, new Rectangle(0, 0, Width, Height));
                g.Restore(texture);
            }

            Color edge = Ui.Gta6
                ? Color.FromArgb(126, 255, 255, 255)
                : (Highlight ? Ui.Accent : (hover ? Ui.BorderStrong : Ui.Border));
            using (GraphicsPath path = Ui.Round(r, radius))
            using (Pen pen = new Pen(edge, 1.2f))
            {
                pen.DashStyle = DashStyle.Solid;
                g.DrawPath(pen, path);
            }

            if (Ui.Holiday)
            {
                if (Ui.NewYear)
                {
                    // Бледный фон под текстом: даёт глубину, но не мешает читать.
                    Ui.DrawSnowflake(g, Width * 0.33f, Height * 0.30f, Ui.P(22),
                        Color.FromArgb(44, Ui.Accent), Math.Max(1.4f, Ui.P(1.6)));
                    Ui.DrawSnowflake(g, Width * 0.30f, Height * 0.80f, Ui.P(14),
                        Color.FromArgb(38, Ui.Accent), Math.Max(1.2f, Ui.P(1.4)));
                    Ui.DrawSnowflake(g, Width * 0.45f, Height * 0.72f, Ui.P(11),
                        Color.FromArgb(34, Ui.Accent), Math.Max(1f, Ui.P(1.3)));

                    // Правая полоса свободна и в пустой, и в выбранной карточке:
                    // между текстом и кнопкой «Другой файл» узор остаётся заметным.
                    int right = Width - Ui.P(18);
                    Ui.DrawSnowflake(g, right - Ui.P(100), Height * 0.30f, Ui.P(20),
                        Color.FromArgb(140, Ui.Accent), Math.Max(1.4f, Ui.P(1.7)));
                    Ui.DrawSnowflake(g, right - Ui.P(12), Height * 0.54f, Ui.P(12),
                        Color.FromArgb(118, Ui.Accent), Math.Max(1.1f, Ui.P(1.4)));
                    Ui.DrawSnowflake(g, right - Ui.P(60), Height * 0.82f, Ui.P(9),
                        Color.FromArgb(106, Ui.Accent), Math.Max(1f, Ui.P(1.2)));
                    Ui.DrawSnowflake(g, right - Ui.P(34), Ui.P(10), Ui.P(7),
                        Color.FromArgb(96, Ui.Accent), Math.Max(1f, Ui.P(1.1)));

                    // Нижняя кромка свободна в обоих состояниях.
                    Ui.DrawSnowflake(g, Width * 0.42f, Height - Ui.P(9), Ui.P(8),
                        Color.FromArgb(104, Ui.Accent), Math.Max(1f, Ui.P(1.2)));
                    Ui.DrawSnowflake(g, Width * 0.52f, Height - Ui.P(6), Ui.P(6),
                        Color.FromArgb(92, Ui.Accent), Math.Max(1f, Ui.P(1.1)));
                    Ui.DrawSnowflake(g, Width * 0.62f, Height - Ui.P(10), Ui.P(7),
                        Color.FromArgb(98, Ui.Accent), Math.Max(1f, Ui.P(1.1)));

                    if (Selected)
                    {
                        // В выбранном состоянии заголовок ушёл ниже — верх тоже свободен.
                        Ui.DrawSnowflake(g, Width * 0.44f, Ui.P(9), Ui.P(8),
                            Color.FromArgb(104, Ui.Accent), Math.Max(1f, Ui.P(1.2)));
                        Ui.DrawSnowflake(g, Width * 0.53f, Ui.P(12), Ui.P(6),
                            Color.FromArgb(92, Ui.Accent), Math.Max(1f, Ui.P(1.1)));
                    }
                }
                else
                {
                    // Тыква и листва держатся правой полосы: она свободна и в пустой,
                    // и в выбранной карточке — там же, где кнопка «Другой файл» не мешает.
                    int right = Width - Ui.P(18);
                    Rectangle pumpkin = new Rectangle(
                        right - Ui.P(66), (Height - Ui.P(44)) / 2, Ui.P(44), Ui.P(38));
                    Ui.DrawPumpkin(g, pumpkin, Color.FromArgb(138, Ui.Accent), Ui.Card);
                    Ui.DrawLeaf(g, right - Ui.P(96), Height * 0.26f, Ui.P(20),
                        Color.FromArgb(132, Ui.Accent), 28f);
                    Ui.DrawLeaf(g, right - Ui.P(10), Height * 0.66f, Ui.P(15),
                        Color.FromArgb(118, Ui.Accent), -34f);
                    Ui.DrawLeaf(g, right - Ui.P(52), Height * 0.90f, Ui.P(11),
                        Color.FromArgb(106, Ui.Accent), 52f);
                    Ui.DrawLeaf(g, right - Ui.P(120), Ui.P(12), Ui.P(10),
                        Color.FromArgb(92, Ui.Accent), -18f);

                    // Бледная листва под текстом — только фактура.
                    Ui.DrawLeaf(g, Width * 0.34f, Height * 0.28f, Ui.P(20),
                        Color.FromArgb(42, Ui.Accent), 24f);
                    Ui.DrawLeaf(g, Width * 0.31f, Height * 0.80f, Ui.P(14),
                        Color.FromArgb(36, Ui.Accent), -40f);
                    Ui.DrawLeaf(g, Width * 0.47f, Height * 0.74f, Ui.P(11),
                        Color.FromArgb(32, Ui.Accent), 60f);

                    // Нижняя кромка свободна в обоих состояниях.
                    Ui.DrawLeaf(g, Width * 0.42f, Height - Ui.P(9), Ui.P(8),
                        Color.FromArgb(102, Ui.Accent), -20f);
                    Ui.DrawLeaf(g, Width * 0.52f, Height - Ui.P(7), Ui.P(7),
                        Color.FromArgb(92, Ui.Accent), 40f);
                    Ui.DrawLeaf(g, Width * 0.62f, Height - Ui.P(10), Ui.P(8),
                        Color.FromArgb(98, Ui.Accent), -60f);

                    if (Selected)
                    {
                        Ui.DrawLeaf(g, Width * 0.44f, Ui.P(10), Ui.P(8),
                            Color.FromArgb(102, Ui.Accent), 30f);
                        Ui.DrawLeaf(g, Width * 0.53f, Ui.P(12), Ui.P(7),
                            Color.FromArgb(90, Ui.Accent), -50f);
                    }
                }
            }

            int iconSize = Ui.P(42);
            int iconX = Ui.P(20);
            int iconY = (Height - iconSize) / 2;
            Rectangle iconRect = new Rectangle(iconX, iconY, iconSize, iconSize);
            Ui.FillRound(g, iconRect, Ui.P(12),
                Ui.Gta6 ? Color.FromArgb(62, 255, 255, 255) : Ui.AccentSoft);

            Color title = Ui.Gta6 ? Color.White : Ui.Text;
            Color sub = Ui.Gta6 ? Color.FromArgb(224, 255, 255, 255) : Ui.Muted;
            Color hint = Ui.Gta6 ? Color.FromArgb(172, 255, 255, 255) : Ui.Faint;

            if (Selected)
            {
                Ui.DrawPlayGlyph(g, iconRect, title);
            }
            else
            {
                Ui.DrawUpArrow(g, iconRect, title);
            }

            int textX = iconX + iconSize + Ui.P(16);
            int rightPad = Selected ? Ui.P(274) : Ui.P(150);

            if (Selected)
            {
                string meta = BusyText.Length > 0 ? BusyText : FileMeta;
                Ui.Write(g, FileName, Ui.Semi,
                    new Rectangle(textX, Ui.P(22), Width - textX - rightPad, Ui.P(24)),
                    title, Ui.LeftMid);
                Ui.Write(g, meta, Ui.Small,
                    new Rectangle(textX, Ui.P(48), Width - textX - rightPad, Ui.P(20)),
                    sub, Ui.LeftMid);

                Rectangle chip = new Rectangle(Width - Ui.P(260), (Height - Ui.P(32)) / 2, Ui.P(116), Ui.P(32));
                Ui.FillRound(g, chip, Ui.P(8),
                    Ui.Gta6 ? Color.FromArgb(150, 8, 6, 12) : Ui.Card);
                Ui.StrokeRound(g, chip, Ui.P(8),
                    Ui.Gta6 ? Color.FromArgb(120, 255, 255, 255) : (hover ? Ui.BorderStrong : Ui.Border), 1f);
                Ui.Write(g, "Другой файл", Ui.Small, chip, title, Ui.CenterMid);
            }
            else
            {
                Ui.Write(g, "Перетащите файл сюда", Ui.Semi,
                    new Rectangle(textX, Ui.P(12), Width - textX - rightPad, Ui.P(24)),
                    title, Ui.LeftMid);
                Ui.Write(g, "или нажмите, чтобы выбрать", Ui.Small,
                    new Rectangle(textX, Ui.P(36), Width - textX - rightPad, Ui.P(18)),
                    sub, Ui.LeftMid);
                Ui.Write(g, "Видео: MP4, MKV, MOV, WEBM   ·   Картинки: PNG, JPG, WEBP, BMP, TIFF", Ui.Tiny,
                    new Rectangle(textX, Ui.P(56), Width - textX - rightPad, Ui.P(16)),
                    hint, Ui.LeftMid);
            }
        }
    }

    internal sealed class LogoBadge : Control
    {
        public LogoBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Ui.Gta6)
            {
                using (GraphicsPath path = Ui.Round(r, Ui.P(10)))
                using (LinearGradientBrush brush = Ui.GtaGradient(r, 45f))
                {
                    g.FillPath(brush, path);
                }
            }
            else
            {
                Color badge = Ui.Halloween ? Ui.AccentWash : Ui.Accent;
                using (GraphicsPath path = Ui.Round(r, Ui.P(10)))
                using (SolidBrush brush = new SolidBrush(badge))
                {
                    g.FillPath(brush, path);
                }
            }

            if (Ui.NewYear)
            {
                Ui.DrawSnowflake(g, Width / 2f, Height / 2f,
                    Math.Min(Width, Height) * 0.33f, Ui.OnAccent, Math.Max(1.6f, Ui.P(1.6)));
                return;
            }

            if (Ui.Halloween)
            {
                Rectangle pumpkin = new Rectangle(
                    (int)(Width * 0.18f), (int)(Height * 0.30f),
                    (int)(Width * 0.64f), (int)(Height * 0.52f));
                Ui.DrawPumpkin(g, pumpkin, Ui.Accent, Ui.AccentWash);
                return;
            }

            PointF[] triangle = new PointF[]
            {
                new PointF(Width * 0.40f, Height * 0.29f),
                new PointF(Width * 0.40f, Height * 0.71f),
                new PointF(Width * 0.72f, Height * 0.50f)
            };
            using (SolidBrush ink = new SolidBrush(Ui.Gta6 ? Color.White : Ui.OnAccent))
            {
                g.FillPolygon(ink, triangle);
            }
        }
    }
}
