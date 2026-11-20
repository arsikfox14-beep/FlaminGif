using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Video2Gif
{
    /// <summary>
    /// Минимальный редактор: обрезка видео по времени и кадрирование фото.
    /// </summary>
    internal static class EditorCore
    {
        public static Bitmap LoadBitmap(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image image = Image.FromStream(stream))
            {
                return new Bitmap(image);
            }
        }

        public static Bitmap CropBitmap(Bitmap source, Rectangle crop)
        {
            Rectangle safe = Rectangle.Intersect(
                crop, new Rectangle(0, 0, source.Width, source.Height));
            if (safe.Width < 1) safe.Width = 1;
            if (safe.Height < 1) safe.Height = 1;

            Bitmap result = new Bitmap(safe.Width, safe.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source,
                    new Rectangle(0, 0, safe.Width, safe.Height),
                    safe, GraphicsUnit.Pixel);
            }
            return result;
        }

        public static void SaveBitmap(Bitmap bitmap, string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".jpg" || ext == ".jpeg")
            {
                ImageCodecInfo jpeg = null;
                foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
                {
                    if (codec.FormatID == ImageFormat.Jpeg.Guid)
                    {
                        jpeg = codec;
                        break;
                    }
                }
                if (jpeg != null)
                {
                    using (EncoderParameters parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(
                            System.Drawing.Imaging.Encoder.Quality, 92L);
                        bitmap.Save(path, jpeg, parameters);
                        return;
                    }
                }
            }
            bitmap.Save(path, ImageFormat.Png);
        }

        /// <summary>Рисует подпись и картинку поверх готового фото.</summary>
        public static void DecoratePhoto(Bitmap bitmap, string overlay,
            int corner, int scale, string caption, bool captionTop)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                int margin = Math.Max(8, bitmap.Width / 40);

                if (!string.IsNullOrEmpty(overlay) && File.Exists(overlay))
                {
                    try
                    {
                        using (Bitmap image = LoadBitmap(overlay))
                        {
                            int width = Math.Max(1,
                                (int)Math.Round(bitmap.Width * scale / 100.0));
                            int height = Math.Max(1,
                                (int)Math.Round(image.Height * (double)width / image.Width));
                            int x = corner % 2 == 0 ? margin : bitmap.Width - width - margin;
                            int y = corner < 2 ? margin : bitmap.Height - height - margin;
                            g.DrawImage(image, x, y, width, height);
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(caption)) return;

                int size = Math.Max(12, bitmap.Height / 12);
                Font font = null;
                while (size >= 8)
                {
                    font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
                    SizeF measured = g.MeasureString(caption, font);
                    if (measured.Width <= bitmap.Width - margin * 2) break;
                    font.Dispose();
                    font = null;
                    size -= 2;
                }
                if (font == null)
                    font = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Pixel);

                try
                {
                    SizeF measured = g.MeasureString(caption, font);
                    float x = (bitmap.Width - measured.Width) / 2f;
                    float y = captionTop
                        ? margin
                        : bitmap.Height - measured.Height - margin;
                    using (SolidBrush shadow = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                    using (SolidBrush fill = new SolidBrush(Color.White))
                    {
                        for (int dx = -2; dx <= 2; dx += 2)
                        {
                            for (int dy = -2; dy <= 2; dy += 2)
                            {
                                if (dx == 0 && dy == 0) continue;
                                g.DrawString(caption, font, shadow, x + dx, y + dy);
                            }
                        }
                        g.DrawString(caption, font, fill, x, y);
                    }
                }
                finally
                {
                    font.Dispose();
                }
            }
        }

        public static void ExtractFrame(string input, double position, string output)
        {
            RunFfmpeg(
                "-hide_banner -loglevel error -y -ss " +
                Fmt.Num(Math.Max(0, position)) +
                " -i " + Fmt.Quote(input) +
                " -frames:v 1 -f image2 " + Fmt.Quote(output));
        }

        /// <summary>Раскладывает GIF на PNG-кадры. Возвращает число кадров.</summary>
        public static int ExtractGifFrames(string input, string folder)
        {
            Directory.CreateDirectory(folder);
            string pattern = Path.Combine(folder, "frame-%04d.png");
            RunFfmpeg(
                "-hide_banner -loglevel error -y -i " + Fmt.Quote(input) +
                " " + Fmt.Quote(pattern));
            return Directory.GetFiles(folder, "frame-*.png").Length;
        }

        /// <summary>Собирает GIF из последовательных PNG-кадров f0001.png…</summary>
        public static void AssembleGif(string folder, int fps, int width, string output)
        {
            string pattern = Path.Combine(folder, "f%04d.png");
            string filter = "fps=" + fps + ",";
            if (width > 0) filter += "scale=" + width + ":-1:flags=lanczos,";
            filter += "split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse";
            RunFfmpeg(
                "-hide_banner -loglevel error -y -framerate " + fps +
                " -i " + Fmt.Quote(pattern) +
                " -vf \"" + filter + "\" -loop 0 " + Fmt.Quote(output));
        }

        public static void TrimVideo(string input, string output,
            double start, double end)
        {
            double duration = Math.Max(0.05, end - start);
            string args = "-hide_banner -loglevel error -y -ss " + Fmt.Num(start) +
                " -i " + Fmt.Quote(input) +
                " -t " + Fmt.Num(duration) +
                " -map 0:v:0 -map 0:a:0?" +
                " -vf scale=trunc(iw/2)*2:trunc(ih/2)*2" +
                " -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p" +
                " -c:a aac -b:a 192k -movflags +faststart " + Fmt.Quote(output);
            RunFfmpeg(args);
            RetryAudioCopy(input, output, args);
        }

        /// <summary>Обрезка видео с подписью и картинкой поверх.</summary>
        public static void TrimVideo(string input, string output,
            double start, double end, string overlay, int corner, int scale,
            string caption, bool captionTop, int sourceWidth, int sourceHeight)
        {
            bool hasOverlay = !string.IsNullOrEmpty(overlay) && File.Exists(overlay);
            bool hasCaption = !string.IsNullOrEmpty(caption);
            if (!hasOverlay && !hasCaption)
            {
                TrimVideo(input, output, start, end);
                return;
            }

            double duration = Math.Max(0.05, end - start);
            int baseWidth = sourceWidth > 0 ? sourceWidth : 640;
            StringBuilder filters = new StringBuilder();
            filters.Append("[0:v]scale=trunc(iw/2)*2:trunc(ih/2)*2[base]");
            string current = "base";

            if (hasOverlay)
            {
                int width = Math.Max(24,
                    (int)Math.Round(baseWidth * Math.Max(5, Math.Min(80, scale)) / 100.0));
                string x = corner % 2 == 0 ? "20" : "W-w-20";
                string y = corner < 2 ? "20" : "H-h-20";
                filters.Append(";[1:v]scale=" + width + ":-1[ov];[" + current +
                    "][ov]overlay=" + x + ":" + y + "[withov]");
                current = "withov";
            }

            if (hasCaption)
            {
                int fontSize = Math.Max(18,
                    (sourceHeight > 0 ? sourceHeight : 360) / 14);
                string y = captionTop ? "20" : "h-text_h-20";
                filters.Append(";[" + current +
                    "]drawtext=fontfile='C\\:/Windows/Fonts/segoeui.ttf':text='" +
                    EscapeDrawText(caption) + "':fontcolor=white:fontsize=" + fontSize +
                    ":borderw=2:bordercolor=black:x=(w-text_w)/2:y=" + y + "[v]");
                current = "v";
            }

            string args = "-hide_banner -loglevel error -y -ss " + Fmt.Num(start) +
                " -i " + Fmt.Quote(input);
            if (hasOverlay) args += " -i " + Fmt.Quote(overlay);
            args += " -t " + Fmt.Num(duration) +
                " -filter_complex \"" + filters + "\"" +
                " -map \"[" + current + "]\" -map 0:a:0?" +
                " -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p" +
                " -c:a aac -b:a 192k -movflags +faststart " + Fmt.Quote(output);
            RunFfmpeg(args);
            RetryAudioCopy(input, output, args);
        }

        /// <summary>
        /// Страховка: если кодировщик AAC по какой-то причине не дал звук,
        /// повторяем сборку с копированием исходной дорожки.
        /// </summary>
        private static void RetryAudioCopy(string input, string output, string args)
        {
            if (!HasAudio(input) || HasAudio(output)) return;
            string retry = args
                .Replace("-map 0:a:0?", "-map 0:a:0")
                .Replace("-c:a aac -b:a 192k", "-c:a copy");
            try
            {
                RunFfmpeg(retry);
            }
            catch
            {
                // Основной файл уже создан — оставляем его.
            }
        }

        private static bool HasAudio(string path)
        {
            try
            {
                return Ffmpeg.Probe(path).Audio;
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeDrawText(string text)
        {
            string value = (text ?? "").Replace("\r", " ").Replace("\n", " ");
            value = value.Replace("\\", "\\\\");
            value = value.Replace(":", "\\:");
            value = value.Replace("'", "\\'");
            value = value.Replace("%", "\\%");
            return value;
        }

        /// <summary>Раскадровка выбранного фрагмента для проигрывания в окне.</summary>
        public static void ExtractPreviewFrames(string input, string folder,
            double start, double duration, int fps)
        {
            Directory.CreateDirectory(folder);
            string pattern = Path.Combine(folder, "f%04d.jpg");
            RunFfmpeg(
                "-hide_banner -loglevel error -y -ss " + Fmt.Num(Math.Max(0, start)) +
                " -i " + Fmt.Quote(input) +
                " -t " + Fmt.Num(Math.Max(0.05, duration)) +
                " -vf fps=" + fps + ",scale=480:-2" +
                " -q:v 5 -start_number 0 " + Fmt.Quote(pattern));
        }

        /// <summary>
        /// Собирает GIF из PNG-кадров с прозрачным фоном.
        /// Палитра резервирует прозрачный индекс, поэтому фон остаётся прозрачным.
        /// </summary>
        public static void AssembleTransparentGif(string folder, int fps, string output)
        {
            string pattern = Path.Combine(folder, "frame-%04d.png");
            RunFfmpeg(
                "-hide_banner -loglevel error -y -framerate " + Math.Max(1, fps) +
                " -i " + Fmt.Quote(pattern) +
                " -vf \"split[s0][s1];[s0]palettegen=stats_mode=diff:reserve_transparent=1[p];" +
                "[s1][p]paletteuse=alpha_threshold=128\" -loop 0 " + Fmt.Quote(output));
        }

        /// <summary>Звук выбранного фрагмента для «Проверить» (WAV, моно).</summary>
        public static void ExtractPreviewAudio(string input, string output,
            double start, double duration)
        {
            RunFfmpeg(
                "-hide_banner -loglevel error -y -ss " + Fmt.Num(Math.Max(0, start)) +
                " -i " + Fmt.Quote(input) +
                " -t " + Fmt.Num(Math.Max(0.05, duration)) +
                " -vn -ac 1 -ar 22050 -c:a pcm_s16le " + Fmt.Quote(output));
        }

        private static void RunFfmpeg(string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(FfmpegBundle.Path, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            psi.WorkingDirectory = Path.GetTempPath();

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                StringBuilder error = new StringBuilder();
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) error.AppendLine(e.Data);
                };
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new ConversionException(Ffmpeg.ReadableError(error.ToString()));
                }
            }
        }
    }

    /// <summary>Область предпросмотра: кадр видео или фото с рамкой кадрирования.</summary>
    internal sealed class EditorPreview : Control
    {
        public Bitmap Image;
        public bool CropMode;

        // Рамка кадрирования в долях от размера картинки (0–1).
        public RectangleF Crop = new RectangleF(0, 0, 1, 1);

        private bool dragging;
        private PointF dragStart;

        public EditorPreview()
        {
            DoubleBuffered = true;
            BackColor = Ui.Card;
        }

        public void SetImage(Bitmap image, bool cropMode)
        {
            if (Image != null && !ReferenceEquals(Image, image))
            {
                Image.Dispose();
            }
            Image = image;
            CropMode = cropMode;
            Crop = new RectangleF(0, 0, 1, 1);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, r, Ui.P(12), Ui.Card);
            Ui.StrokeRound(g, r, Ui.P(12), Ui.Border, 1f);

            if (Image == null)
            {
                Ui.Write(g, "Откройте видео или фото",
                    Ui.Small, r, Ui.Faint, Ui.CenterMid);
                return;
            }

            Rectangle display = FitRect(Image.Width, Image.Height);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(Image, display);

            if (!CropMode) return;

            Rectangle crop = CropRect(display);
            using (SolidBrush veil = new SolidBrush(Color.FromArgb(132, 0, 0, 0)))
            {
                g.FillRectangle(veil, display.Left, display.Top,
                    display.Width, crop.Top - display.Top);
                g.FillRectangle(veil, display.Left, crop.Bottom,
                    display.Width, display.Bottom - crop.Bottom);
                g.FillRectangle(veil, display.Left, crop.Top,
                    crop.Left - display.Left, crop.Height);
                g.FillRectangle(veil, crop.Right, crop.Top,
                    display.Right - crop.Right, crop.Height);
            }

            using (Pen pen = new Pen(Ui.Accent, Math.Max(1.5f, Ui.P(2))))
            {
                g.DrawRectangle(pen, crop.X, crop.Y, crop.Width, crop.Height);
            }

            int handle = Math.Max(4, Ui.P(6));
            using (SolidBrush brush = new SolidBrush(Ui.Accent))
            {
                g.FillRectangle(brush, crop.Left - handle / 2, crop.Top - handle / 2,
                    handle, handle);
                g.FillRectangle(brush, crop.Right - handle / 2, crop.Top - handle / 2,
                    handle, handle);
                g.FillRectangle(brush, crop.Left - handle / 2, crop.Bottom - handle / 2,
                    handle, handle);
                g.FillRectangle(brush, crop.Right - handle / 2, crop.Bottom - handle / 2,
                    handle, handle);
            }
        }

        private Rectangle FitRect(int width, int height)
        {
            int pad = Ui.P(12);
            int boxW = Math.Max(1, Width - pad * 2);
            int boxH = Math.Max(1, Height - pad * 2);
            double k = Math.Min((double)boxW / width, (double)boxH / height);
            int w = Math.Max(1, (int)Math.Round(width * k));
            int h = Math.Max(1, (int)Math.Round(height * k));
            return new Rectangle((Width - w) / 2, (Height - h) / 2, w, h);
        }

        public Rectangle CropPixels()
        {
            if (Image == null) return Rectangle.Empty;
            return ToPixels(Crop);
        }

        public Rectangle ToPixels(RectangleF crop)
        {
            int x = (int)Math.Round(crop.X * Image.Width);
            int y = (int)Math.Round(crop.Y * Image.Height);
            int w = (int)Math.Round(crop.Width * Image.Width);
            int h = (int)Math.Round(crop.Height * Image.Height);
            if (w < 1) w = 1;
            if (h < 1) h = 1;
            if (x + w > Image.Width) w = Image.Width - x;
            if (y + h > Image.Height) h = Image.Height - y;
            return new Rectangle(x, y, Math.Max(1, w), Math.Max(1, h));
        }

        public Rectangle CropRect(Rectangle display)
        {
            return new Rectangle(
                display.Left + (int)Math.Round(Crop.X * display.Width),
                display.Top + (int)Math.Round(Crop.Y * display.Height),
                Math.Max(1, (int)Math.Round(Crop.Width * display.Width)),
                Math.Max(1, (int)Math.Round(Crop.Height * display.Height)));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Image == null || !CropMode || e.Button != MouseButtons.Left) return;
            if (!FitRect(Image.Width, Image.Height).Contains(e.Location)) return;
            dragging = true;
            dragStart = ToUnit(e.Location);
            Crop = new RectangleF(dragStart.X, dragStart.Y, 0, 0);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging || Image == null) return;
            PointF current = ToUnit(e.Location);
            float left = Math.Min(dragStart.X, current.X);
            float top = Math.Min(dragStart.Y, current.Y);
            float width = Math.Abs(current.X - dragStart.X);
            float height = Math.Abs(current.Y - dragStart.Y);
            Crop = new RectangleF(left, top, width, height);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            if (Crop.Width < 0.02f || Crop.Height < 0.02f)
            {
                Crop = new RectangleF(0, 0, 1, 1);
            }
            Invalidate();
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (!CropMode) return;
            Crop = new RectangleF(0, 0, 1, 1);
            Invalidate();
        }

        private PointF ToUnit(Point point)
        {
            Rectangle display = FitRect(Image.Width, Image.Height);
            float x = (float)(point.X - display.Left) / Math.Max(1, display.Width);
            float y = (float)(point.Y - display.Top) / Math.Max(1, display.Height);
            return new PointF(Math.Max(0f, Math.Min(1f, x)),
                Math.Max(0f, Math.Min(1f, y)));
        }
    }

    /// <summary>Один ползунок с двумя бегунками: начало и конец обрезки.</summary>
    internal sealed class RangeBar : Control
    {
        private double start;
        private double end = 1;
        private int dragging; // 0 — нет, 1 — начало, 2 — конец
        private int lastHandle = 1;

        public double MinGap = 0.002;

        public event EventHandler Changed;
        public event EventHandler Committed;

        public double Start
        {
            get { return start; }
        }

        public double End
        {
            get { return end; }
        }

        public int LastHandle
        {
            get { return lastHandle; }
        }

        public RangeBar()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        public void SetRange(double from, double to)
        {
            start = Math.Max(0, Math.Min(1, from));
            end = Math.Max(0, Math.Min(1, to));
            if (start > end)
            {
                double swap = start;
                start = end;
                end = swap;
            }
            if (end - start < MinGap) end = Math.Min(1, start + MinGap);
            if (start > end)
            {
                start = end;
                end = Math.Min(1, start + MinGap);
            }
            Invalidate();
        }

        public void Reset()
        {
            start = 0;
            end = 1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int left = Ui.P(12);
            int right = Width - Ui.P(12);
            int center = Height / 2;
            int xs = ValueToX(start);
            int xe = ValueToX(end);

            using (Pen pen = new Pen(Ui.Track, Math.Max(4, Ui.P(5))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, left, center, right, center);
            }

            if (xe > xs)
            {
                using (Pen pen = new Pen(Ui.Accent, Math.Max(4, Ui.P(5))))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, xs, center, xe, center);
                }
            }

            int handleW = Math.Max(8, Ui.P(10));
            int handleH = Math.Max(16, Ui.P(22));
            DrawHandle(g, xs, center, handleW, handleH);
            DrawHandle(g, xe, center, handleW, handleH);
        }

        private void DrawHandle(Graphics g, int x, int center, int width, int height)
        {
            Rectangle r = new Rectangle(x - width / 2, center - height / 2,
                width, height);
            using (GraphicsPath path = Ui.Round(r, width / 2))
            using (SolidBrush brush = new SolidBrush(Ui.Accent))
            {
                g.FillPath(brush, path);
            }
            using (Pen pen = new Pen(Ui.OnAccent, Math.Max(1.4f, Ui.P(1.5))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                int inset = Math.Max(2, Ui.P(3));
                g.DrawLine(pen, x, center - height / 2 + inset,
                    x, center + height / 2 - inset);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            double click = XToValue(e.X);
            double toStart = Math.Abs(click - start);
            double toEnd = Math.Abs(click - end);
            dragging = toStart <= toEnd ? 1 : 2;
            lastHandle = dragging;
            MoveHandle(click);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging == 0) return;
            MoveHandle(XToValue(e.X));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging == 0) return;
            dragging = 0;
            if (Committed != null) Committed(this, EventArgs.Empty);
        }

        private void MoveHandle(double value)
        {
            double next = Math.Max(0, Math.Min(1, value));
            if (dragging == 1)
            {
                start = Math.Min(next, end - MinGap);
                if (start < 0) start = 0;
            }
            else if (dragging == 2)
            {
                end = Math.Max(next, start + MinGap);
                if (end > 1) end = 1;
            }
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        private int ValueToX(double value)
        {
            int left = Ui.P(12);
            int right = Width - Ui.P(12);
            return left + (int)Math.Round((right - left) * value);
        }

        private double XToValue(int x)
        {
            int left = Ui.P(12);
            int right = Width - Ui.P(12);
            return (double)(x - left) / Math.Max(1, right - left);
        }
    }

    /// <summary>Окно «Видеоредактор»: обрезка видео и кадрирование фото.</summary>
    internal sealed class EditorForm : ThemedForm
    {
        private EditorPreview preview;
        private RangeBar range;
        private Label rangeStartLabel;
        private Label rangeEndLabel;
        private Label rangeInfoLabel;
        private Label selectionLabel;
        private Label hintLabel;
        private Label statusLabel;
        private PillButton openButton;
        private PillButton resetButton;
        private PillButton saveButton;
        private PillButton closeButton;
        private PillButton playButton;
        private PillButton styleButton;

        private string overlayPath = "";
        private int overlayCorner = 3;
        private int overlayScale = 20;
        private string caption = "";
        private bool captionTop;

        private readonly List<string> playFrames = new List<string>();
        private Timer playTimer;
        private SoundPlayer playSound;
        private string playDir = "";
        private int playIndex;
        private int playFps;
        private double playSeconds;
        private bool playing;
        private bool preparing;

        private string path = "";
        private VideoInfo info;
        private bool isImage;
        private bool working;
        private int frameTicket;

        public EditorForm()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = "Видеоредактор — " + Program.AppName;
            BackColor = Ui.Bg;
            Font = Ui.Base;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.P(720), Ui.P(564));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;
            AllowDrop = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            Controls.Add(MakeLabel("Видеоредактор", Ui.H1, Ui.Text, 24, 16, 420, 30));
            Controls.Add(MakeLabel("Обрезка видео и фото · самый минимум",
                Ui.Small, Ui.Muted, 24, 48, 420, 18));

            openButton = new PillButton();
            openButton.Ghost = true;
            openButton.Text = "Открыть файл";
            openButton.Font = Ui.Small;
            openButton.SetBounds(Ui.P(536), Ui.P(16), Ui.P(160), Ui.P(40));
            openButton.Click += delegate { PickFile(); };
            Controls.Add(openButton);

            preview = new EditorPreview();
            preview.SetBounds(Ui.P(24), Ui.P(72), Ui.P(672), Ui.P(300));
            Controls.Add(preview);

            hintLabel = MakeLabel("Потяните бегунки, затем «Проверить» — фрагмент проиграется со звуком.",
                Ui.Tiny, Ui.Faint, 24, 376, 672, 18);
            Controls.Add(hintLabel);

            range = new RangeBar();
            range.SetBounds(Ui.P(24), Ui.P(398), Ui.P(672), Ui.P(32));
            range.Changed += delegate { UpdateRange(); };
            range.Committed += delegate
            {
                RequestFrame((range.LastHandle == 1 ? range.Start : range.End) * Duration());
            };
            Controls.Add(range);

            rangeStartLabel = MakeLabel("Начало: 0,0 с", Ui.Small, Ui.Muted,
                24, 436, 200, 18);
            Controls.Add(rangeStartLabel);

            rangeInfoLabel = MakeLabel("Выбрано: 0,0 с", Ui.Small, Ui.Muted,
                248, 436, 200, 18);
            rangeInfoLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(rangeInfoLabel);

            rangeEndLabel = MakeLabel("Конец: 0,0 с", Ui.Small, Ui.Muted,
                472, 436, 224, 18);
            rangeEndLabel.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(rangeEndLabel);

            playButton = new PillButton();
            playButton.Ghost = true;
            playButton.Text = "▶ Проверить";
            playButton.Font = Ui.Small;
            playButton.SetBounds(Ui.P(24), Ui.P(460), Ui.P(150), Ui.P(34));
            playButton.Visible = false;
            playButton.Click += delegate { TogglePreview(); };
            Controls.Add(playButton);

            resetButton = new PillButton();
            resetButton.Ghost = true;
            resetButton.Text = "Весь кадр";
            resetButton.Font = Ui.Small;
            resetButton.SetBounds(Ui.P(24), Ui.P(398), Ui.P(160), Ui.P(34));
            resetButton.Visible = false;
            resetButton.Click += delegate
            {
                preview.Crop = new RectangleF(0, 0, 1, 1);
                preview.Invalidate();
                UpdateSelection();
            };
            Controls.Add(resetButton);

            selectionLabel = MakeLabel("", Ui.Tiny, Ui.Faint, 472, 398, 224, 50);
            selectionLabel.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(selectionLabel);

            statusLabel = MakeLabel("", Ui.Tiny, Ui.Faint, 186, 460, 510, 34);
            statusLabel.AutoEllipsis = true;
            Controls.Add(statusLabel);

            saveButton = new PillButton();
            saveButton.Accent = true;
            saveButton.Text = "Сохранить";
            saveButton.Font = Ui.Bold;
            saveButton.SetBounds(Ui.P(24), Ui.P(500), Ui.P(332), Ui.P(44));
            saveButton.Click += delegate { Save(); };
            Controls.Add(saveButton);

            closeButton = new PillButton();
            closeButton.Ghost = true;
            closeButton.Text = "Закрыть";
            closeButton.SetBounds(Ui.P(364), Ui.P(500), Ui.P(160), Ui.P(44));
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);

            styleButton = new PillButton();
            styleButton.Ghost = true;
            styleButton.Text = "Текст и поверх";
            styleButton.Font = Ui.Small;
            styleButton.SetBounds(Ui.P(532), Ui.P(500), Ui.P(164), Ui.P(44));
            styleButton.Click += delegate { OpenOverlayEditor(); };
            Controls.Add(styleButton);

            playTimer = new Timer();
            playTimer.Tick += delegate { PlayTick(); };

            DragEnter += delegate(object sender, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            DragDrop += delegate(object sender, DragEventArgs e)
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0) LoadFile(files[0]);
            };

            ApplyMode();
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (working || preparing)
            {
                e.Cancel = true;
                statusLabel.Text = "Подождите, идёт подготовка…";
                return;
            }
            StopPlayback(false);
            if (preview.Image != null)
            {
                preview.SetImage(null, false);
            }
            base.OnFormClosing(e);
        }

        private double Duration()
        {
            if (info == null) return 0;
            return info.Duration > 0 ? info.Duration : 0;
        }

        private void PickFile()
        {
            if (working) return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите видео или фото";
                dialog.Filter = MainFilter;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadFile(dialog.FileName);
                }
            }
        }

        private const string MainFilter =
            "Видео и картинки|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.m4v;*.wmv;*.flv;*.mpg;*.mpeg;*.ts;*.mts;*.m2ts;*.3gp;*.ogv;*.gif;*.png;*.jpg;*.jpeg;*.jfif;*.webp;*.bmp;*.tif;*.tiff;*.tga;*.avif|Все файлы|*.*";

        private void LoadFile(string file)
        {
            if (working || preparing) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                StopPlayback(false);
                VideoInfo probe = Ffmpeg.Probe(file);
                path = file;
                info = probe;
                isImage = probe.IsImage;
                frameTicket++;
                statusLabel.Text = new FileInfo(file).Name;

                if (isImage)
                {
                    Bitmap bitmap = EditorCore.LoadBitmap(file);
                    preview.SetImage(bitmap, true);
                }
                else
                {
                    preview.SetImage(null, false);
                    double duration = Duration();
                    range.MinGap = duration > 0
                        ? Math.Max(0.001, Math.Min(0.2, 0.05 / duration))
                        : 0.002;
                    range.SetRange(0, 1);
                    UpdateRange();
                    RequestFrame(0);
                }

                FitPreview();
                ApplyMode();
                UpdateSelection();
            }
            catch (Exception ex)
            {
                statusLabel.Text = ex.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ApplyMode()
        {
            bool photo = isImage;
            range.Visible = !photo;
            rangeStartLabel.Visible = !photo;
            rangeEndLabel.Visible = !photo;
            rangeInfoLabel.Visible = !photo;
            selectionLabel.Visible = photo;
            resetButton.Visible = photo;
            playButton.Visible = !photo && path.Length > 0;
            if (photo)
            {
                statusLabel.SetBounds(Ui.P(24), Ui.P(460), Ui.P(672), Ui.P(34));
            }
            else
            {
                statusLabel.SetBounds(Ui.P(186), Ui.P(460), Ui.P(510), Ui.P(34));
            }
            saveButton.Text = photo ? "Сохранить фото" : "Сохранить видео";
            hintLabel.Text = photo
                ? "Выделите область мышью на фото. Двойной клик — весь кадр."
                : (info != null && !info.Audio && path.Length > 0
                    ? "В этом файле нет звуковой дорожки — видео сохранится без звука."
                    : "Потяните бегунки, затем «Проверить» — фрагмент проиграется со звуком.");
            saveButton.Enabled = !working && CanSave();
            playButton.Enabled = !working && !preparing &&
                !isImage && path.Length > 0 && Duration() > 0;
        }

        /// <summary>Подгоняет карточку предпросмотра под пропорции файла.</summary>
        private void FitPreview()
        {
            int maxWidth = Ui.P(672);
            int height = Ui.P(300);
            int pad = Ui.P(12);
            double aspect = 0;

            if (isImage && preview.Image != null)
            {
                aspect = (double)preview.Image.Width / preview.Image.Height;
            }
            else if (info != null && info.Width > 0 && info.Height > 0)
            {
                aspect = (double)info.Width / info.Height;
            }

            int width = maxWidth;
            if (aspect > 0.1 && aspect < 10)
            {
                width = (int)Math.Round((height - pad * 2) * aspect + pad * 2);
                if (width < Ui.P(240)) width = Ui.P(240);
                if (width > maxWidth) width = maxWidth;
            }
            int x = Ui.P(24) + (maxWidth - width) / 2;
            preview.SetBounds(x, Ui.P(72), width, height);
        }

        private bool CanSave()
        {
            return isImage ? preview.Image != null : path.Length > 0;
        }

        private void UpdateRange()
        {
            if (isImage || info == null) return;
            double duration = Duration();
            rangeStartLabel.Text = "Начало: " + Fmt.Time(range.Start * duration);
            rangeEndLabel.Text = "Конец: " + Fmt.Time(range.End * duration);
            rangeInfoLabel.Text = "Выбрано: " +
                Fmt.Time(Math.Max(0, (range.End - range.Start) * duration));
        }

        private void UpdateSelection()
        {
            if (!isImage) return;
            if (preview.Image == null)
            {
                selectionLabel.Text = "";
                return;
            }
            Rectangle crop = preview.CropPixels();
            selectionLabel.Text = "Кадр:\n" + preview.Image.Width + "×" + preview.Image.Height +
                "\nВыбрано: " + crop.Width + "×" + crop.Height;
        }

        private void RequestFrame(double position)
        {
            if (isImage || info == null || path.Length == 0) return;
            string source = path;
            string folder = Path.Combine(Path.GetTempPath(), "Video2Gif", "editor-frames");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder,
                "frame-" + Guid.NewGuid().ToString("N") + ".png");
            int ticket = ++frameTicket;

            Task.Factory.StartNew(delegate
            {
                EditorCore.ExtractFrame(source, position, file);
                Bitmap bitmap = EditorCore.LoadBitmap(file);
                try { File.Delete(file); }
                catch { }
                return bitmap;
            }).ContinueWith(delegate(Task<Bitmap> task)
            {
                if (IsDisposed)
                {
                    if (!task.IsFaulted) task.Result.Dispose();
                    return;
                }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (ticket != frameTicket)
                        {
                            if (!task.IsFaulted) task.Result.Dispose();
                            return;
                        }
                        if (task.IsFaulted)
                        {
                            statusLabel.Text = "Не удалось показать кадр.";
                            return;
                        }
                        ApplyPreviewDecoration(task.Result);
                        preview.SetImage(task.Result, false);
                        saveButton.Enabled = !working && CanSave();
                    });
                }
                catch { }
            });
        }

        private void TogglePreview()
        {
            if (preparing) return;
            if (playing)
            {
                StopPlayback(true);
                return;
            }
            StartPreview();
        }

        private void StartPreview()
        {
            if (isImage || path.Length == 0) return;
            double total = Duration();
            if (total <= 0) return;

            double start = range.Start * total;
            double length = Math.Max(0, (range.End - range.Start) * total);
            if (length < 0.05) return;

            double previewLength = Math.Min(length, 240.0);
            int fps = (int)Math.Round(Math.Max(1, Math.Min(12, 240.0 / previewLength)));
            if (fps < 1) fps = 1;

            playFps = fps;
            playSeconds = previewLength;
            playIndex = 0;
            playFrames.Clear();
            CleanupPlayFrames();
            playDir = Path.Combine(Path.GetTempPath(), "Video2Gif", "preview",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(playDir);

            string source = path;
            string folder = playDir;
            double from = start;
            double len = previewLength;
            bool hasAudio = info != null && info.Audio;

            preparing = true;
            playButton.Enabled = false;
            openButton.Enabled = false;
            saveButton.Enabled = false;
            range.Enabled = false;
            UseWaitCursor = true;
            statusLabel.Text = "Готовим предпросмотр…";

            Task.Factory.StartNew(delegate
            {
                EditorCore.ExtractPreviewFrames(source, folder, from, len, fps);
                if (hasAudio)
                {
                    try
                    {
                        EditorCore.ExtractPreviewAudio(source,
                            Path.Combine(folder, "preview.wav"), from, len);
                    }
                    catch
                    {
                    }
                }
            }).ContinueWith(delegate(Task task)
            {
                if (IsDisposed) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        preparing = false;
                        UseWaitCursor = false;
                        openButton.Enabled = !working;
                        range.Enabled = !working;
                        saveButton.Enabled = !working && CanSave();
                        playButton.Enabled = !working && Duration() > 0;

                        if (task.IsFaulted)
                        {
                            statusLabel.Text = "Не удалось подготовить предпросмотр.";
                            CleanupPlayFrames();
                            return;
                        }

                        string[] files = Directory.GetFiles(playDir, "*.jpg");
                        Array.Sort(files, StringComparer.Ordinal);
                        if (files.Length == 0)
                        {
                            statusLabel.Text = "Не удалось подготовить предпросмотр.";
                            CleanupPlayFrames();
                            return;
                        }

                        playFrames.AddRange(files);
                        playing = true;
                        playButton.Text = "■ Стоп";
                        openButton.Enabled = false;
                        saveButton.Enabled = false;
                        range.Enabled = false;
                        ShowPlayFrame(0);
                        playTimer.Interval = Math.Max(50, (int)Math.Round(1000.0 / fps));
                        playTimer.Start();
                        StartPreviewAudio(Path.Combine(playDir, "preview.wav"));
                    });
                }
                catch { }
            });
        }

        private void PlayTick()
        {
            if (!playing) return;
            playIndex++;
            if (playIndex >= playFrames.Count)
            {
                StopPlayback(false);
                statusLabel.Text = Path.GetFileName(path);
                return;
            }
            ShowPlayFrame(playIndex);
        }

        private void ShowPlayFrame(int index)
        {
            if (index < 0 || index >= playFrames.Count) return;
            try
            {
                Bitmap bitmap = EditorCore.LoadBitmap(playFrames[index]);
                ApplyPreviewDecoration(bitmap);
                preview.SetImage(bitmap, false);
            }
            catch { }
            double current = index / (double)Math.Max(1, playFps);
            statusLabel.Text = "Проверка: " + Fmt.Time(current) +
                " из " + Fmt.Time(playSeconds);
        }

        private void StopPlayback(bool restoreFrame)
        {
            StopPreviewAudio();
            if (playTimer != null) playTimer.Stop();
            bool hadFrames = playFrames.Count > 0;
            playing = false;
            preparing = false;
            if (playButton != null)
            {
                playButton.Text = "▶ Проверить";
                playButton.Enabled = !working && !isImage &&
                    path.Length > 0 && Duration() > 0;
            }
            if (!working)
            {
                openButton.Enabled = true;
                range.Enabled = !isImage;
                saveButton.Enabled = CanSave();
                UseWaitCursor = false;
            }
            playFrames.Clear();
            CleanupPlayFrames();
            if (restoreFrame && hadFrames && !isImage && path.Length > 0)
            {
                RequestFrame(range.Start * Duration());
            }
        }

        private void CleanupPlayFrames()
        {
            StopPreviewAudio();
            if (playTimer != null) playTimer.Stop();
            if (playDir.Length > 0)
            {
                try { Directory.Delete(playDir, true); }
                catch { }
                playDir = "";
            }
        }

        private void StartPreviewAudio(string file)
        {
            StopPreviewAudio();
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;
            try
            {
                playSound = new SoundPlayer(file);
                playSound.Load();
                playSound.Play();
            }
            catch
            {
                StopPreviewAudio();
            }
        }

        private void StopPreviewAudio()
        {
            if (playSound == null) return;
            try { playSound.Stop(); } catch { }
            try { playSound.Dispose(); } catch { }
            playSound = null;
        }

        private void Save()
        {
            if (working) return;
            if (isImage) SavePhoto();
            else SaveVideo();
        }

        private void OpenOverlayEditor()
        {
            if (working) return;
            if (playing) StopPlayback(true);
            using (OverlayForm dialog = new OverlayForm())
            {
                dialog.SetValues(caption, captionTop, overlayPath, overlayCorner, overlayScale);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    caption = dialog.Caption;
                    captionTop = dialog.CaptionTop;
                    overlayPath = dialog.OverlayPath;
                    overlayCorner = dialog.OverlayCorner;
                    overlayScale = dialog.OverlayScale;
                    statusLabel.Text = "Оформление сохранено. Применится при сохранении.";
                    if (!isImage && path.Length > 0)
                    {
                        RequestFrame(range.Start * Duration());
                    }
                }
            }
        }

        private void ApplyPreviewDecoration(Bitmap bitmap)
        {
            if (isImage || bitmap == null) return;
            if (overlayPath.Length == 0 && caption.Length == 0) return;
            EditorCore.DecoratePhoto(bitmap, overlayPath, overlayCorner,
                overlayScale, caption, captionTop);
        }

        private void SaveVideo()
        {
            if (path.Length == 0) return;
            double duration = Duration();
            if (duration <= 0)
            {
                MessageBox.Show(this, "Не удалось определить длительность видео.",
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            double start = range.Start * duration;
            double end = range.End * duration;
            if (end - start < 0.05)
            {
                MessageBox.Show(this, "Выберите более длинный фрагмент.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Сохранить обрезанное видео";
                dialog.Filter = "Видео MP4|*.mp4";
                dialog.InitialDirectory = SavePaths.Desktop();
                dialog.FileName = Path.GetFileNameWithoutExtension(path) + "-обрезка.mp4";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string output = dialog.FileName;
                if (!output.ToLowerInvariant().EndsWith(".mp4")) output += ".mp4";
                string source = path;
                double from = start;
                double to = end;
                string overlay = overlayPath;
                int corner = overlayCorner;
                int scale = overlayScale;
                string text = caption;
                bool textTop = captionTop;
                int sourceWidth = info != null ? info.Width : 0;
                int sourceHeight = info != null ? info.Height : 0;

                SetWorking(true, "Сохраняем видео…");
                Task.Factory.StartNew(delegate
                {
                    EditorCore.TrimVideo(source, output, from, to,
                        overlay, corner, scale, text, textTop, sourceWidth, sourceHeight);
                }).ContinueWith(delegate(Task task)
                {
                    if (IsDisposed) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (task.IsFaulted)
                            {
                                string message = "Не удалось сохранить видео.";
                                if (task.Exception != null &&
                                    task.Exception.InnerException != null)
                                {
                                    message = task.Exception.InnerException.Message;
                                }
                                SetWorking(false, "");
                                MessageBox.Show(this, message, Program.AppName,
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            SetWorking(false, "Готово: " + Path.GetFileName(output));
                        });
                    }
                    catch { }
                });
            }
        }

        private void SavePhoto()
        {
            if (preview.Image == null) return;
            Rectangle crop = preview.CropPixels();
            if (crop.Width < 1 || crop.Height < 1)
            {
                MessageBox.Show(this, "Выделите область на фото.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Сохранить обрезанное фото";
                dialog.Filter = "PNG|*.png|JPEG|*.jpg";
                dialog.InitialDirectory = SavePaths.Desktop();
                dialog.FileName = Path.GetFileNameWithoutExtension(path) + "-обрезка.png";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    using (Bitmap result = EditorCore.CropBitmap(preview.Image, crop))
                    {
                        EditorCore.DecoratePhoto(result, overlayPath, overlayCorner,
                            overlayScale, caption, captionTop);
                        EditorCore.SaveBitmap(result, dialog.FileName);
                    }
                    statusLabel.Text = "Готово: " + Path.GetFileName(dialog.FileName) +
                        " (" + crop.Width + "×" + crop.Height + ")";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось сохранить фото.\n" + ex.Message,
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SetWorking(bool busy, string status)
        {
            if (busy) StopPlayback(false);
            working = busy;
            openButton.Enabled = !busy;
            resetButton.Enabled = !busy;
            range.Enabled = !busy && !isImage;
            saveButton.Enabled = !busy && CanSave();
            playButton.Enabled = !busy && !isImage && path.Length > 0 && Duration() > 0;
            statusLabel.Text = status;
            UseWaitCursor = busy;
        }
    }
}
