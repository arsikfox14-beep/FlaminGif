using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Video2Gif
{
    /// <summary>Удаление фона по цвету для фото и GIF.</summary>
    internal static class CutoutCore
    {
        public static Bitmap RemoveBackground(Bitmap source, Color key,
            int tolerance, int feather, bool connected)
        {
            return RemoveBackground(source, new Color[] { key },
                tolerance, feather, connected);
        }

        /// <summary>
        /// Удаляет фон по набору цветов: градиент и разные оттенки фона
        /// находятся точнее, чем по одному цвету с большим допуском.
        /// </summary>
        public static Bitmap RemoveBackground(Bitmap source, IList<Color> keys,
            int tolerance, int feather, bool connected)
        {
            if (keys == null || keys.Count == 0)
                keys = new Color[] { Color.White };

            int width = source.Width;
            int height = source.Height;
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
                g.DrawImageUnscaled(source, 0, 0);
            }

            BitmapData data = result.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int[] pixels = new int[width * height];
            try
            {
                int stride = data.Stride;
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (long)y * stride),
                        pixels, y * width, width);
                }

                int limit = ToleranceToDistance(tolerance);
                bool[] background = new bool[width * height];
                if (connected)
                {
                    FloodFromBorder(pixels, width, height, keys, limit, background);
                }
                else
                {
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        if (MatchesAny(pixels[i], keys, limit)) background[i] = true;
                    }
                }

                byte[] alpha = new byte[width * height];
                for (int i = 0; i < pixels.Length; i++)
                {
                    int sourceAlpha = (pixels[i] >> 24) & 0xFF;
                    alpha[i] = background[i] ? (byte)0 : (byte)sourceAlpha;
                }

                RefineEdges(alpha, pixels, width, height);
                if (feather > 0)
                {
                    int radius = Math.Max(1, Math.Min(8, feather));
                    BlurAlpha(alpha, width, height, radius);
                }

                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = (pixels[i] & 0x00FFFFFF) | (alpha[i] << 24);
                }

                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(pixels, y * width,
                        new IntPtr(data.Scan0.ToInt64() + (long)y * stride), width);
                }
            }
            finally
            {
                result.UnlockBits(data);
            }
            return result;
        }

        /// <summary>Уменьшенная копия для быстрого предпросмотра.</summary>
        public static Bitmap Fit(Bitmap source, int maxWidth, int maxHeight)
        {
            if (source == null) return null;
            double scale = Math.Min(1.0, Math.Min(
                maxWidth / (double)source.Width, maxHeight / (double)source.Height));
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, width, height));
            }
            return result;
        }

        /// <summary>
        /// Цвет фона по углам картинки. Углы надёжнее всего края: объект
        /// часто касается нижней или боковой кромки и не должен попадать в фон.
        /// </summary>
        public static Color AutoColor(Bitmap bitmap)
        {
            if (bitmap == null || bitmap.Width < 1 || bitmap.Height < 1)
                return Color.White;

            int[] counts = new int[32768];
            int[] sumR = new int[32768];
            int[] sumG = new int[32768];
            int[] sumB = new int[32768];

            int width = bitmap.Width;
            int height = bitmap.Height;
            int patch = Math.Max(3, Math.Min(16, Math.Min(width, height) / 18));
            int samples = 0;
            samples += AddCorner(bitmap, 0, 0, patch, counts, sumR, sumG, sumB);
            samples += AddCorner(bitmap, width - patch, 0, patch, counts, sumR, sumG, sumB);
            samples += AddCorner(bitmap, 0, height - patch, patch, counts, sumR, sumG, sumB);
            samples += AddCorner(bitmap, width - patch, height - patch, patch,
                counts, sumR, sumG, sumB);

            if (samples == 0)
            {
                int stepX = Math.Max(1, width / 500);
                int stepY = Math.Max(1, height / 500);
                for (int x = 0; x < width; x += stepX)
                {
                    AddBorderSample(bitmap.GetPixel(x, 0), counts, sumR, sumG, sumB);
                    AddBorderSample(bitmap.GetPixel(x, height - 1), counts, sumR, sumG, sumB);
                }
                for (int y = 0; y < height; y += stepY)
                {
                    AddBorderSample(bitmap.GetPixel(0, y), counts, sumR, sumG, sumB);
                    AddBorderSample(bitmap.GetPixel(width - 1, y), counts, sumR, sumG, sumB);
                }
            }

            int best = -1;
            int bestCount = 0;
            int bestSaturation = int.MaxValue;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                int r = sumR[i] / counts[i];
                int g = sumG[i] / counts[i];
                int b = sumB[i] / counts[i];
                int saturation = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
                // При равном количестве выбираем более нейтральный цвет.
                if (counts[i] > bestCount ||
                    (counts[i] == bestCount && saturation < bestSaturation))
                {
                    bestCount = counts[i];
                    best = i;
                    bestSaturation = saturation;
                }
            }
            if (best < 0 || bestCount == 0) return Color.White;
            return Color.FromArgb(
                sumR[best] / bestCount,
                sumG[best] / bestCount,
                sumB[best] / bestCount);
        }

        /// <summary>
        /// До четырёх основных цветов фона по углам. Градиент и разные
        /// оттенки фона обрабатываются точнее, чем одним цветом.
        /// </summary>
        public static List<Color> AutoPalette(Bitmap bitmap)
        {
            List<Color> result = new List<Color>();
            if (bitmap == null || bitmap.Width < 1 || bitmap.Height < 1)
            {
                result.Add(Color.White);
                return result;
            }

            int[] counts = new int[32768];
            int[] sumR = new int[32768];
            int[] sumG = new int[32768];
            int[] sumB = new int[32768];
            int width = bitmap.Width;
            int height = bitmap.Height;
            int patch = Math.Max(3, Math.Min(16, Math.Min(width, height) / 18));
            int samples = 0;
            samples += AddCorner(bitmap, 0, 0, patch, counts, sumR, sumG, sumB);
            samples += AddCorner(bitmap, width - patch, 0, patch, counts, sumR, sumG, sumB);
            samples += AddCorner(bitmap, 0, height - patch, patch, counts, sumR, sumG, sumB);
            samples += AddCorner(bitmap, width - patch, height - patch, patch,
                counts, sumR, sumG, sumB);

            if (samples == 0)
            {
                int stepX = Math.Max(1, width / 500);
                int stepY = Math.Max(1, height / 500);
                for (int x = 0; x < width; x += stepX)
                {
                    AddBorderSample(bitmap.GetPixel(x, 0), counts, sumR, sumG, sumB);
                    AddBorderSample(bitmap.GetPixel(x, height - 1), counts, sumR, sumG, sumB);
                }
                for (int y = 0; y < height; y += stepY)
                {
                    AddBorderSample(bitmap.GetPixel(0, y), counts, sumR, sumG, sumB);
                    AddBorderSample(bitmap.GetPixel(width - 1, y), counts, sumR, sumG, sumB);
                }
            }

            int topCount = 0;
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] > topCount) topCount = counts[i];
            if (topCount == 0)
            {
                result.Add(Color.White);
                return result;
            }

            bool[] used = new bool[counts.Length];
            int minimum = Math.Max(3, topCount / 8);
            for (int k = 0; k < 4; k++)
            {
                int best = -1;
                int bestCount = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    if (used[i] || counts[i] <= bestCount) continue;
                    bestCount = counts[i];
                    best = i;
                }
                if (best < 0) break;
                if (result.Count > 0 && bestCount < minimum) break;
                used[best] = true;
                result.Add(Color.FromArgb(
                    sumR[best] / bestCount,
                    sumG[best] / bestCount,
                    sumB[best] / bestCount));
            }
            if (result.Count == 0) result.Add(Color.White);
            return result;
        }

        /// <summary>Подходящий начальный допуск по разбросу цвета фона.</summary>
        public static int SuggestTolerance(Bitmap bitmap, Color key)
        {
            return SuggestTolerance(bitmap, new Color[] { key });
        }

        public static int SuggestTolerance(Bitmap bitmap, IList<Color> keys)
        {
            if (bitmap == null || keys == null || keys.Count == 0) return 14;
            int width = bitmap.Width;
            int height = bitmap.Height;
            int stepX = Math.Max(1, width / 300);
            int stepY = Math.Max(1, height / 300);
            int max = 0;
            int count = 0;
            for (int x = 0; x < width; x += stepX)
            {
                AddSpreadAny(bitmap.GetPixel(x, 0), keys, ref max, ref count);
                AddSpreadAny(bitmap.GetPixel(x, height - 1), keys, ref max, ref count);
            }
            for (int y = 0; y < height; y += stepY)
            {
                AddSpreadAny(bitmap.GetPixel(0, y), keys, ref max, ref count);
                AddSpreadAny(bitmap.GetPixel(width - 1, y), keys, ref max, ref count);
            }
            if (count == 0) return 12;
            int tolerance = 4 + max / 2;
            if (tolerance < 8) tolerance = 8;
            if (tolerance > 30) tolerance = 30;
            return tolerance;
        }

        private static void AddSpreadAny(Color color, IList<Color> keys,
            ref int max, ref int count)
        {
            if (color.A < 20) return;
            int distance = int.MaxValue;
            for (int i = 0; i < keys.Count; i++)
            {
                int value = RawDistance(color.ToArgb(), keys[i]);
                if (value < distance) distance = value;
            }
            if (distance > 60) return;
            if (distance > max) max = distance;
            count++;
        }

        private static int AddCorner(Bitmap bitmap, int left, int top, int size,
            int[] counts, int[] sumR, int[] sumG, int[] sumB)
        {
            int samples = 0;
            int right = Math.Min(bitmap.Width, left + size);
            int bottom = Math.Min(bitmap.Height, top + size);
            for (int y = Math.Max(0, top); y < bottom; y++)
            {
                for (int x = Math.Max(0, left); x < right; x++)
                {
                    AddBorderSample(bitmap.GetPixel(x, y),
                        counts, sumR, sumG, sumB);
                    samples++;
                }
            }
            return samples;
        }

        private static void AddSpread(Color color, Color key, ref int max, ref int count)
        {
            if (color.A < 20) return;
            int distance = RawDistance(color.ToArgb(), key);
            // Явно чужие пиксели (объект у края) не раздувают допуск.
            if (distance > 60) return;
            if (distance > max) max = distance;
            count++;
        }

        private static void AddBorderSample(Color color,
            int[] counts, int[] sumR, int[] sumG, int[] sumB)
        {
            if (color.A < 20) return;
            int index = ((color.R >> 3) << 10) | ((color.G >> 3) << 5) | (color.B >> 3);
            counts[index]++;
            sumR[index] += color.R;
            sumG[index] += color.G;
            sumB[index] += color.B;
        }

        private static int ToleranceToDistance(int tolerance)
        {
            if (tolerance < 0) tolerance = 0;
            if (tolerance > 100) tolerance = 100;
            return 4 + tolerance * 2;
        }

        private static bool MatchesAny(int pixel, IList<Color> keys, int limit)
        {
            int alpha = (pixel >> 24) & 0xFF;
            if (alpha < 20) return true;
            for (int i = 0; i < keys.Count; i++)
            {
                if (Distance(pixel, keys[i]) <= limit) return true;
            }
            return false;
        }

        /// <summary>
        /// Разница цветов. Чуть сильнее наказываем насыщенные пиксели:
        /// блики кожи на нейтральном фоне перестают считаться фоном.
        /// </summary>
        private static int Distance(int pixel, Color key)
        {
            int r = (pixel >> 16) & 0xFF;
            int g = (pixel >> 8) & 0xFF;
            int b = pixel & 0xFF;
            int distance = Math.Max(Math.Abs(r - key.R),
                Math.Max(Math.Abs(g - key.G), Math.Abs(b - key.B)));

            int saturation = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
            int keySaturation = Math.Max(key.R, Math.Max(key.G, key.B)) -
                Math.Min(key.R, Math.Min(key.G, key.B));
            int extra = saturation - keySaturation - 16;
            if (extra > 0) distance += extra / 3;
            return distance;
        }

        private static int RawDistance(int pixel, Color key)
        {
            int r = (pixel >> 16) & 0xFF;
            int g = (pixel >> 8) & 0xFF;
            int b = pixel & 0xFF;
            return Math.Max(Math.Abs(r - key.R),
                Math.Max(Math.Abs(g - key.G), Math.Abs(b - key.B)));
        }

        private static int EdgeDiff(int first, int second)
        {
            int r1 = (first >> 16) & 0xFF;
            int g1 = (first >> 8) & 0xFF;
            int b1 = first & 0xFF;
            int r2 = (second >> 16) & 0xFF;
            int g2 = (second >> 8) & 0xFF;
            int b2 = second & 0xFF;
            return Math.Max(Math.Abs(r1 - r2),
                Math.Max(Math.Abs(g1 - g2), Math.Abs(b1 - b2)));
        }

        private static void FloodFromBorder(int[] pixels, int width, int height,
            IList<Color> keys, int limit, bool[] background)
        {
            Stack<int> stack = new Stack<int>();
            for (int x = 0; x < width; x++)
            {
                Push(x, 0, pixels, width, keys, limit, background, stack, 0, false);
                Push(x, height - 1, pixels, width, keys, limit, background, stack, 0, false);
            }
            for (int y = 1; y < height - 1; y++)
            {
                Push(0, y, pixels, width, keys, limit, background, stack, 0, false);
                Push(width - 1, y, pixels, width, keys, limit, background, stack, 0, false);
            }

            while (stack.Count > 0)
            {
                int index = stack.Pop();
                int x = index % width;
                int y = index / width;
                int from = pixels[index];
                if (x > 0) Push(x - 1, y, pixels, width, keys, limit, background, stack, from, true);
                if (x + 1 < width) Push(x + 1, y, pixels, width, keys, limit, background, stack, from, true);
                if (y > 0) Push(x, y - 1, pixels, width, keys, limit, background, stack, from, true);
                if (y + 1 < height) Push(x, y + 1, pixels, width, keys, limit, background, stack, from, true);
            }
        }

        private static void Push(int x, int y, int[] pixels, int width,
            IList<Color> keys, int limit, bool[] background, Stack<int> stack,
            int fromPixel, bool checkEdge)
        {
            int index = y * width + x;
            if (background[index]) return;
            if (!MatchesAny(pixels[index], keys, limit)) return;
            // Не протекаем в объект через резкую границу.
            if (checkEdge &&
                ((fromPixel >> 24) & 0xFF) >= 20 &&
                ((pixels[index] >> 24) & 0xFF) >= 20 &&
                EdgeDiff(fromPixel, pixels[index]) > 80) return;
            background[index] = true;
            stack.Push(index);
        }

        private static void BlurAlpha(byte[] alpha, int width, int height, int radius)
        {
            byte[] temp = new byte[alpha.Length];
            int window = radius * 2 + 1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                int sum = 0;
                for (int i = -radius; i <= radius; i++)
                    sum += alpha[row + Clamp(i, 0, width - 1)];
                for (int x = 0; x < width; x++)
                {
                    temp[row + x] = (byte)(sum / window);
                    int add = Clamp(x + radius + 1, 0, width - 1);
                    int sub = Clamp(x - radius, 0, width - 1);
                    sum += alpha[row + add] - alpha[row + sub];
                }
            }

            for (int x = 0; x < width; x++)
            {
                int sum = 0;
                for (int i = -radius; i <= radius; i++)
                    sum += temp[Clamp(i, 0, height - 1) * width + x];
                for (int y = 0; y < height; y++)
                {
                    alpha[y * width + x] = (byte)(sum / window);
                    int add = Clamp(y + radius + 1, 0, height - 1);
                    int sub = Clamp(y - radius, 0, height - 1);
                    sum += temp[add * width + x] - temp[sub * width + x];
                }
            }
        }

        /// <summary>
        /// Сглаживает альфу только между похожими цветами: край становится
        /// ровнее, но не размазывается через границу объекта.
        /// </summary>
        private static void RefineEdges(byte[] alpha, int[] pixels, int width, int height)
        {
            byte[] source = (byte[])alpha.Clone();
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int index = row + x;
                    int center = pixels[index];
                    int sum = 0;
                    int weight = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= height) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= width) continue;
                            int n = ny * width + nx;
                            int diff = EdgeDiff(center, pixels[n]);
                            int w = diff <= 24 ? 2 : (diff <= 56 ? 1 : 0);
                            if (w == 0) continue;
                            sum += source[n] * w;
                            weight += w;
                        }
                    }
                    alpha[index] = weight > 0 ? (byte)(sum / weight) : source[index];
                }
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }

    /// <summary>Превью с шахматкой и пипеткой.</summary>
    internal sealed class CutoutPreview : UiControl
    {
        private Bitmap image;
        private Bitmap sample;
        private bool pickMode;
        private Rectangle imageRect = Rectangle.Empty;

        public event Action<Color> Picked;

        public void SetImages(Bitmap display, Bitmap sampleSource)
        {
            image = display;
            sample = sampleSource;
            Invalidate();
        }

        public bool PickMode
        {
            get { return pickMode; }
            set
            {
                pickMode = value;
                Cursor = value ? Cursors.Cross : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (image == null)
            {
                Ui.Write(g, "Откройте фото или GIF", Ui.Base,
                    ClientRectangle, Ui.Muted, Ui.CenterMid);
                return;
            }

            int pad = Ui.P(10);
            Rectangle area = new Rectangle(pad, pad,
                Math.Max(1, Width - pad * 2), Math.Max(1, Height - pad * 2));
            double scale = Math.Min(area.Width / (double)image.Width,
                area.Height / (double)image.Height);
            int width = Math.Max(1, (int)Math.Round(image.Width * scale));
            int height = Math.Max(1, (int)Math.Round(image.Height * scale));
            imageRect = new Rectangle(
                area.X + (area.Width - width) / 2,
                area.Y + (area.Height - height) / 2,
                width, height);

            DrawChecker(g, imageRect);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(image, imageRect);
            Ui.StrokeRound(g, imageRect, Ui.P(6), Ui.Border, 1f);
        }

        private static void DrawChecker(Graphics g, Rectangle rect)
        {
            int cell = Math.Max(6, Ui.P(8));
            using (SolidBrush light = new SolidBrush(Color.FromArgb(244, 244, 247)))
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(214, 214, 220)))
            {
                g.FillRectangle(light, rect);
                for (int y = rect.Top; y < rect.Bottom; y += cell)
                {
                    for (int x = rect.Left; x < rect.Right; x += cell)
                    {
                        if (((x - rect.Left) / cell + (y - rect.Top) / cell) % 2 == 0)
                            continue;
                        g.FillRectangle(dark, x, y,
                            Math.Min(cell, rect.Right - x),
                            Math.Min(cell, rect.Bottom - y));
                    }
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!pickMode || sample == null || !imageRect.Contains(e.Location)) return;
            double fx = (e.X - imageRect.X) / (double)Math.Max(1, imageRect.Width);
            double fy = (e.Y - imageRect.Y) / (double)Math.Max(1, imageRect.Height);
            int x = Math.Max(0, Math.Min(sample.Width - 1,
                (int)Math.Round(fx * (sample.Width - 1))));
            int y = Math.Max(0, Math.Min(sample.Height - 1,
                (int)Math.Round(fy * (sample.Height - 1))));
            PickMode = false;
            Action<Color> handler = Picked;
            if (handler != null) handler(sample.GetPixel(x, y));
        }
    }

    internal sealed class ColorChip : UiControl
    {
        private Color value = Color.White;

        public Color Value
        {
            get { return value; }
            set
            {
                this.value = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, r, Ui.P(8), value);
            Ui.StrokeRound(g, r, Ui.P(8), Ui.BorderStrong, 1f);
        }
    }

    /// <summary>Окно «Убрать фон» для фото и GIF.</summary>
    internal sealed class CutoutForm : ThemedForm
    {
        private readonly CutoutPreview preview;
        private readonly ColorChip chip;
        private readonly Label statusLabel;
        private readonly PillButton openButton;
        private readonly PillButton pickButton;
        private readonly PillButton autoButton;
        private readonly PillButton saveButton;
        private readonly PillButton resetButton;
        private readonly PillButton closeButton;
        private readonly FlatSlider toleranceBar;
        private readonly FlatSlider featherBar;
        private readonly Label toleranceLabel;
        private readonly Label featherLabel;
        private readonly ToggleRow linkedToggle;

        private string path = "";
        private bool isGif;
        private bool working;
        private bool suppress;
        private Bitmap original;
        private Bitmap previewBase;
        private Bitmap previewResult;
        private Color key = Color.White;
        private List<Color> palette = new List<Color>();
        private int tolerance = 14;
        private int feather = 2;
        private int suggestedTolerance = 14;
        private bool connected = true;

        public CutoutForm()
        {
            Text = "Убрать фон — " + Program.AppName;
            BackColor = Ui.Bg;
            ForeColor = Ui.Text;
            Font = Ui.Base;
            ClientSize = new Size(Ui.P(760), Ui.P(592));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            DragEnter += delegate(object sender, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };
            DragDrop += delegate(object sender, DragEventArgs e)
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0) LoadFile(files[0]);
            };

            Controls.Add(Lbl("Убрать фон", Ui.H1, Ui.Text, 24, 16, 420, 30));
            Controls.Add(Lbl("Фото и GIF · удаление фона по цвету",
                Ui.Small, Ui.Muted, 24, 48, 520, 18));

            openButton = new PillButton();
            openButton.Ghost = true;
            openButton.Text = "Открыть файл";
            openButton.Font = Ui.Small;
            openButton.SetBounds(Ui.P(584), Ui.P(16), Ui.P(152), Ui.P(40));
            openButton.Click += delegate { PickFile(); };
            Controls.Add(openButton);

            preview = new CutoutPreview();
            preview.SetBounds(Ui.P(24), Ui.P(76), Ui.P(712), Ui.P(270));
            preview.Picked += delegate(Color color)
            {
                key = color;
                palette = new List<Color> { color };
                suggestedTolerance = CutoutCore.SuggestTolerance(previewBase, palette);
                chip.Value = color;
                pickButton.Active = false;
                ResetSlidersQuietly();
                UpdatePreview();
                statusLabel.Text = "Цвет фона: #" + color.R.ToString("X2") +
                    color.G.ToString("X2") + color.B.ToString("X2");
            };
            Controls.Add(preview);

            statusLabel = Lbl("Откройте фото или GIF", Ui.Tiny, Ui.Faint, 24, 352, 712, 18);
            statusLabel.AutoEllipsis = true;
            Controls.Add(statusLabel);

            chip = new ColorChip();
            chip.Value = Color.White;
            chip.SetBounds(Ui.P(24), Ui.P(380), Ui.P(32), Ui.P(32));
            Controls.Add(chip);
            Controls.Add(Lbl("Цвет фона", Ui.Small, Ui.Muted, 64, 380, 130, 32));

            autoButton = new PillButton();
            autoButton.Ghost = true;
            autoButton.Text = "Взять из угла";
            autoButton.Font = Ui.Small;
            autoButton.SetBounds(Ui.P(430), Ui.P(380), Ui.P(140), Ui.P(32));
            autoButton.Click += delegate { TakeAutoColor(); };
            Controls.Add(autoButton);

            pickButton = new PillButton();
            pickButton.Ghost = true;
            pickButton.Text = "Пипетка";
            pickButton.Font = Ui.Small;
            pickButton.SetBounds(Ui.P(578), Ui.P(380), Ui.P(158), Ui.P(32));
            pickButton.Click += delegate
            {
                if (original == null) return;
                preview.PickMode = !preview.PickMode;
                pickButton.Active = preview.PickMode;
                statusLabel.Text = preview.PickMode
                    ? "Кликните по фону на картинке"
                    : "Пипетка выключена";
            };
            Controls.Add(pickButton);

            toleranceLabel = Lbl("Допуск: 30", Ui.Small, Ui.Muted, 24, 424, 340, 18);
            Controls.Add(toleranceLabel);
            toleranceBar = new FlatSlider();
            toleranceBar.Minimum = 0;
            toleranceBar.Maximum = 100;
            toleranceBar.Value = tolerance;
            toleranceBar.SetBounds(Ui.P(24), Ui.P(444), Ui.P(340), Ui.P(28));
            toleranceBar.ValueChanged += delegate
            {
                tolerance = toleranceBar.Value;
                toleranceLabel.Text = "Допуск: " + tolerance;
                if (!suppress) UpdatePreview();
            };
            Controls.Add(toleranceBar);

            featherLabel = Lbl("Края: 2", Ui.Small, Ui.Muted, 390, 424, 346, 18);
            Controls.Add(featherLabel);
            featherBar = new FlatSlider();
            featherBar.Minimum = 0;
            featherBar.Maximum = 12;
            featherBar.Value = feather;
            featherBar.SetBounds(Ui.P(390), Ui.P(444), Ui.P(346), Ui.P(28));
            featherBar.ValueChanged += delegate
            {
                feather = featherBar.Value;
                featherLabel.Text = "Края: " + feather;
                if (!suppress) UpdatePreview();
            };
            Controls.Add(featherBar);

            linkedToggle = new ToggleRow();
            linkedToggle.Title = "Только связанный фон";
            linkedToggle.Subtitle = "Не трогать похожие цвета внутри объекта";
            linkedToggle.Checked = true;
            linkedToggle.SetBounds(Ui.P(24), Ui.P(480), Ui.P(712), Ui.P(38));
            linkedToggle.CheckedChanged += delegate
            {
                connected = linkedToggle.Checked;
                if (!suppress) UpdatePreview();
            };
            Controls.Add(linkedToggle);

            saveButton = new PillButton();
            saveButton.Accent = true;
            saveButton.Text = "Сохранить PNG";
            saveButton.Font = Ui.Bold;
            saveButton.SetBounds(Ui.P(24), Ui.P(532), Ui.P(300), Ui.P(44));
            saveButton.Enabled = false;
            saveButton.Click += delegate { Save(); };
            Controls.Add(saveButton);

            resetButton = new PillButton();
            resetButton.Ghost = true;
            resetButton.Text = "Сбросить";
            resetButton.Font = Ui.Small;
            resetButton.SetBounds(Ui.P(336), Ui.P(532), Ui.P(120), Ui.P(44));
            resetButton.Enabled = false;
            resetButton.Click += delegate { ResetSettings(); };
            Controls.Add(resetButton);

            closeButton = new PillButton();
            closeButton.Ghost = true;
            closeButton.Text = "Закрыть";
            closeButton.SetBounds(Ui.P(468), Ui.P(532), Ui.P(268), Ui.P(44));
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (working)
            {
                e.Cancel = true;
                statusLabel.Text = "Подождите, идёт обработка…";
                return;
            }
            DisposeImages();
            base.OnFormClosing(e);
        }

        private static Label Lbl(string text, Font font, Color color,
            int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.UseMnemonic = false;
            label.Tag = Ui.RoleOf(color);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.SetBounds(Ui.P(x), Ui.P(y), Ui.P(width), Ui.P(height));
            return label;
        }

        private void PickFile()
        {
            if (working) return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите фото или GIF";
                dialog.Filter = "Фото и GIF|*.png;*.jpg;*.jpeg;*.jfif;*.webp;*.bmp;*.gif|Все файлы|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                LoadFile(dialog.FileName);
            }
        }

        private void LoadFile(string file)
        {
            if (working) return;
            try
            {
                SetWorking(true, "Открываем…");
                isGif = string.Equals(Path.GetExtension(file), ".gif",
                    StringComparison.OrdinalIgnoreCase);

                Bitmap loaded;
                using (FileStream stream = new FileStream(
                    file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image image = Image.FromStream(stream))
                {
                    if (isGif && image.FrameDimensionsList.Length > 0)
                    {
                        try
                        {
                            image.SelectActiveFrame(
                                new FrameDimension(image.FrameDimensionsList[0]), 0);
                        }
                        catch { }
                    }
                    loaded = new Bitmap(image.Width, image.Height,
                        PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(loaded))
                    {
                        g.Clear(Color.Transparent);
                        g.DrawImageUnscaled(image, 0, 0);
                    }
                }

                DisposeImages();
                original = loaded;
                path = file;
                previewBase = CutoutCore.Fit(original, 640, 400);
                palette = CutoutCore.AutoPalette(previewBase);
                key = palette.Count > 0 ? palette[0] : Color.White;
                suggestedTolerance = CutoutCore.SuggestTolerance(previewBase, palette);
                chip.Value = key;
                ResetSlidersQuietly();
                UpdatePreview();
                saveButton.Enabled = true;
                resetButton.Enabled = true;
                saveButton.Text = isGif ? "Сохранить GIF" : "Сохранить PNG";
                statusLabel.Text = Path.GetFileName(file) +
                    (isGif ? " · GIF" : " · фото");
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Не удалось открыть файл.";
                MessageBox.Show(this, "Не удалось открыть файл.\n\n" + ex.Message,
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetWorking(false, "");
            }
        }

        private void ResetSlidersQuietly()
        {
            suppress = true;
            tolerance = suggestedTolerance;
            if (tolerance < 0) tolerance = 0;
            if (tolerance > 100) tolerance = 100;
            feather = 2;
            toleranceBar.Value = tolerance;
            featherBar.Value = feather;
            toleranceLabel.Text = "Допуск: " + tolerance;
            featherLabel.Text = "Края: " + feather;
            suppress = false;
        }

        private void ResetSettings()
        {
            if (original == null) return;
            suppress = true;
            palette = CutoutCore.AutoPalette(previewBase);
            key = palette.Count > 0 ? palette[0] : Color.White;
            suggestedTolerance = CutoutCore.SuggestTolerance(previewBase, palette);
            chip.Value = key;
            linkedToggle.Checked = true;
            connected = true;
            ResetSlidersQuietly();
            suppress = false;
            preview.PickMode = false;
            pickButton.Active = false;
            UpdatePreview();
        }

        private void TakeAutoColor()
        {
            if (previewBase == null) return;
            palette = CutoutCore.AutoPalette(previewBase);
            key = palette.Count > 0 ? palette[0] : Color.White;
            suggestedTolerance = CutoutCore.SuggestTolerance(previewBase, palette);
            chip.Value = key;
            preview.PickMode = false;
            pickButton.Active = false;
            ResetSlidersQuietly();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (original == null || previewBase == null) return;
            if (previewResult != null)
            {
                previewResult.Dispose();
                previewResult = null;
            }
            previewResult = CutoutCore.RemoveBackground(
                previewBase, palette, tolerance, feather, connected);
            preview.SetImages(previewResult, previewBase);
            preview.Invalidate();
        }

        private void Save()
        {
            if (working || original == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.InitialDirectory = SavePaths.Desktop();
                string suffix = Loc.IsEnglish ? "-no-bg" : "-без-фона";
                if (isGif)
                {
                    dialog.Title = Loc.T("Сохранить GIF без фона");
                    dialog.Filter = "GIF|*.gif";
                    dialog.FileName = Path.GetFileNameWithoutExtension(path) +
                        suffix + ".gif";
                }
                else
                {
                    dialog.Title = Loc.T("Сохранить фото без фона");
                    dialog.Filter = "PNG|*.png";
                    dialog.FileName = Path.GetFileNameWithoutExtension(path) +
                        suffix + ".png";
                }
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string output = dialog.FileName;
                string wanted = isGif ? ".gif" : ".png";
                if (!output.ToLowerInvariant().EndsWith(wanted)) output += wanted;

                string sourcePath = path;
                bool gif = isGif;
                List<Color> colors = new List<Color>(palette);
                int tol = tolerance;
                int soft = feather;
                bool onlyLinked = connected;
                Bitmap sourceBitmap = original;

                SetWorking(true, "Сохраняем…");
                Task.Factory.StartNew(delegate
                {
                    if (gif)
                    {
                        string folder = Path.Combine(Path.GetTempPath(),
                            "Video2Gif", "cutout-" + Guid.NewGuid().ToString("N"));
                        try
                        {
                            int count = EditorCore.ExtractGifFrames(sourcePath, folder);
                            for (int i = 1; i <= count; i++)
                            {
                                Report(Loc.T("Кадр ") + i + Loc.T(" из ") + count + "…");
                                string frame = Path.Combine(folder,
                                    "frame-" + i.ToString("0000") + ".png");
                                using (Bitmap image = EditorCore.LoadBitmap(frame))
                                using (Bitmap cut = CutoutCore.RemoveBackground(
                                    image, colors, tol, soft, onlyLinked))
                                {
                                    cut.Save(frame, ImageFormat.Png);
                                }
                            }
                            int fps = 10;
                            try
                            {
                                VideoInfo info = Ffmpeg.Probe(sourcePath);
                                if (info.Fps >= 1 && info.Fps <= 50)
                                    fps = (int)Math.Round(info.Fps);
                            }
                            catch { }
                            EditorCore.AssembleTransparentGif(folder, fps, output);
                        }
                        finally
                        {
                            try { Directory.Delete(folder, true); } catch { }
                        }
                    }
                    else
                    {
                        using (Bitmap cut = CutoutCore.RemoveBackground(
                            sourceBitmap, colors, tol, soft, onlyLinked))
                        {
                            cut.Save(output, ImageFormat.Png);
                        }
                    }
                }).ContinueWith(delegate(Task task)
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            SetWorking(false, "");
                            if (task.IsFaulted)
                            {
                                string message = "Не удалось убрать фон.";
                                if (task.Exception != null &&
                                    task.Exception.InnerException != null)
                                {
                                    message = task.Exception.InnerException.Message;
                                }
                                statusLabel.Text = message;
                                MessageBox.Show(this, message, Program.AppName,
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            statusLabel.Text = "Готово: " + Path.GetFileName(output);
                        });
                    }
                    catch { }
                });
            }
        }

        private void Report(string text)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate { statusLabel.Text = text; });
            }
            catch { }
        }

        private void SetWorking(bool busy, string text)
        {
            working = busy;
            openButton.Enabled = !busy;
            pickButton.Enabled = !busy;
            autoButton.Enabled = !busy;
            toleranceBar.Enabled = !busy;
            featherBar.Enabled = !busy;
            saveButton.Enabled = !busy && original != null;
            resetButton.Enabled = !busy && original != null;
            closeButton.Enabled = !busy;
            UseWaitCursor = busy;
            if (!string.IsNullOrEmpty(text)) statusLabel.Text = text;
        }

        private void DisposeImages()
        {
            if (previewResult != null) { previewResult.Dispose(); previewResult = null; }
            if (previewBase != null) { previewBase.Dispose(); previewBase = null; }
            if (original != null) { original.Dispose(); original = null; }
        }
    }
}
