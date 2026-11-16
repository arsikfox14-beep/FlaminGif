using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
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

        public static void ExtractFrame(string input, double position, string output)
        {
            RunFfmpeg(
                "-hide_banner -loglevel error -y -ss " +
                Fmt.Num(Math.Max(0, position)) +
                " -i " + Fmt.Quote(input) +
                " -frames:v 1 -f image2 " + Fmt.Quote(output));
        }

        public static void TrimVideo(string input, string output,
            double start, double end)
        {
            double duration = Math.Max(0.05, end - start);
            RunFfmpeg(
                "-hide_banner -loglevel error -y -ss " + Fmt.Num(start) +
                " -i " + Fmt.Quote(input) +
                " -t " + Fmt.Num(duration) +
                " -vf scale=trunc(iw/2)*2:trunc(ih/2)*2" +
                " -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p" +
                " -c:a aac -b:a 192k -movflags +faststart " + Fmt.Quote(output));
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

    /// <summary>Полоса перемотки с перетаскиваемым бегунком.</summary>
    internal sealed class SeekBar : Control
    {
        private double value;
        private bool dragging;

        public event EventHandler ValueChanged;
        public event EventHandler Committed;

        public double Value
        {
            get { return value; }
            set
            {
                double next = Math.Max(0, Math.Min(1, value));
                if (Math.Abs(next - this.value) < 0.0001) return;
                this.value = next;
                Invalidate();
            }
        }

        public SeekBar()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int left = Ui.P(10);
            int right = Width - Ui.P(10);
            int center = Height / 2;
            int thickness = Math.Max(3, Ui.P(4));

            using (Pen pen = new Pen(Ui.Track, thickness))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, left, center, right, center);
            }

            int x = left + (int)Math.Round((right - left) * value);
            if (x > left)
            {
                using (Pen pen = new Pen(Ui.Accent, thickness))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, left, center, x, center);
                }
            }

            int radius = Math.Max(5, Ui.P(7));
            using (SolidBrush brush = new SolidBrush(Ui.Accent))
            {
                g.FillEllipse(brush, x - radius, center - radius,
                    radius * 2, radius * 2);
            }
            using (SolidBrush brush = new SolidBrush(Ui.OnAccent))
            {
                g.FillEllipse(brush, x - radius / 2, center - radius / 2,
                    radius, radius);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            SetFromMouse(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetFromMouse(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            if (Committed != null) Committed(this, EventArgs.Empty);
        }

        private void SetFromMouse(int x)
        {
            int left = Ui.P(10);
            int right = Width - Ui.P(10);
            double next = (double)(x - left) / Math.Max(1, right - left);
            Value = next;
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    /// <summary>Окно «Видеоредактор»: обрезка видео и кадрирование фото.</summary>
    internal sealed class EditorForm : Form
    {
        private EditorPreview preview;
        private SeekBar seek;
        private FlatInput startInput;
        private FlatInput endInput;
        private Label startLabel;
        private Label endLabel;
        private Label timeLabel;
        private Label selectionLabel;
        private Label hintLabel;
        private Label statusLabel;
        private PillButton openButton;
        private PillButton startHereButton;
        private PillButton endHereButton;
        private PillButton resetButton;
        private PillButton saveButton;
        private PillButton closeButton;

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
            ClientSize = new Size(Ui.P(720), Ui.P(620));
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
            preview.SetBounds(Ui.P(24), Ui.P(80), Ui.P(672), Ui.P(304));
            Controls.Add(preview);

            hintLabel = MakeLabel("Ползунок — кадр предпросмотра. Отметьте начало и конец обрезки.",
                Ui.Tiny, Ui.Faint, 24, 390, 672, 18);
            Controls.Add(hintLabel);

            seek = new SeekBar();
            seek.SetBounds(Ui.P(24), Ui.P(414), Ui.P(672), Ui.P(24));
            seek.ValueChanged += delegate { UpdateTimeLabel(); };
            seek.Committed += delegate { RequestFrame(seek.Value * Duration()); };
            Controls.Add(seek);

            timeLabel = MakeLabel("", Ui.Tiny, Ui.Muted, 24, 438, 672, 18);
            timeLabel.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(timeLabel);

            startLabel = MakeLabel("Начало, с", Ui.Small, Ui.Muted, 24, 464, 120, 18);
            Controls.Add(startLabel);
            endLabel = MakeLabel("Конец, с", Ui.Small, Ui.Muted, 156, 464, 120, 18);
            Controls.Add(endLabel);

            startInput = new FlatInput();
            startInput.SetBounds(Ui.P(24), Ui.P(484), Ui.P(120), Ui.P(34));
            startInput.Value = "0";
            Controls.Add(startInput);

            endInput = new FlatInput();
            endInput.SetBounds(Ui.P(156), Ui.P(484), Ui.P(120), Ui.P(34));
            Controls.Add(endInput);

            startHereButton = new PillButton();
            startHereButton.Ghost = true;
            startHereButton.Text = "Начало здесь";
            startHereButton.Font = Ui.Small;
            startHereButton.SetBounds(Ui.P(288), Ui.P(484), Ui.P(120), Ui.P(34));
            startHereButton.Click += delegate
            {
                startInput.Value = Fmt.Num(seek.Value * Duration());
                UpdateSelection();
            };
            Controls.Add(startHereButton);

            endHereButton = new PillButton();
            endHereButton.Ghost = true;
            endHereButton.Text = "Конец здесь";
            endHereButton.Font = Ui.Small;
            endHereButton.SetBounds(Ui.P(416), Ui.P(484), Ui.P(120), Ui.P(34));
            endHereButton.Click += delegate
            {
                endInput.Value = Fmt.Num(seek.Value * Duration());
                UpdateSelection();
            };
            Controls.Add(endHereButton);

            resetButton = new PillButton();
            resetButton.Ghost = true;
            resetButton.Text = "Весь кадр";
            resetButton.Font = Ui.Small;
            resetButton.SetBounds(Ui.P(24), Ui.P(484), Ui.P(160), Ui.P(34));
            resetButton.Visible = false;
            resetButton.Click += delegate
            {
                preview.Crop = new RectangleF(0, 0, 1, 1);
                preview.Invalidate();
                UpdateSelection();
            };
            Controls.Add(resetButton);

            selectionLabel = MakeLabel("", Ui.Tiny, Ui.Faint, 548, 466, 148, 52);
            selectionLabel.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(selectionLabel);

            statusLabel = MakeLabel("", Ui.Tiny, Ui.Faint, 24, 530, 672, 18);
            statusLabel.AutoEllipsis = true;
            Controls.Add(statusLabel);

            saveButton = new PillButton();
            saveButton.Accent = true;
            saveButton.Text = "Сохранить";
            saveButton.Font = Ui.Bold;
            saveButton.SetBounds(Ui.P(24), Ui.P(554), Ui.P(332), Ui.P(44));
            saveButton.Click += delegate { Save(); };
            Controls.Add(saveButton);

            closeButton = new PillButton();
            closeButton.Ghost = true;
            closeButton.Text = "Закрыть";
            closeButton.SetBounds(Ui.P(364), Ui.P(554), Ui.P(160), Ui.P(44));
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);

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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (working)
            {
                e.Cancel = true;
                statusLabel.Text = "Подождите, идёт сохранение…";
                return;
            }
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
            if (working) return;
            try
            {
                Cursor = Cursors.WaitCursor;
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
                    seek.Value = 0;
                }
                else
                {
                    preview.SetImage(null, false);
                    seek.Value = 0;
                    startInput.Value = "0";
                    endInput.Value = Fmt.Num(Duration());
                    UpdateTimeLabel();
                    RequestFrame(0);
                }

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
            seek.Visible = !photo;
            timeLabel.Visible = !photo;
            startLabel.Visible = !photo;
            endLabel.Visible = !photo;
            startInput.Visible = !photo;
            endInput.Visible = !photo;
            startHereButton.Visible = !photo;
            endHereButton.Visible = !photo;
            resetButton.Visible = photo;
            saveButton.Text = photo ? "Сохранить фото" : "Сохранить видео";
            hintLabel.Text = photo
                ? "Выделите область мышью на фото. Двойной клик — весь кадр."
                : "Ползунок — кадр предпросмотра. Отметьте начало и конец обрезки.";
            saveButton.Enabled = preview.Image != null && !working;
        }

        private void UpdateTimeLabel()
        {
            if (isImage || info == null) return;
            double duration = Duration();
            timeLabel.Text = Fmt.Time(seek.Value * duration) + " / " + Fmt.Time(duration);
        }

        private void UpdateSelection()
        {
            if (isImage)
            {
                if (preview.Image == null)
                {
                    selectionLabel.Text = "";
                    return;
                }
                Rectangle crop = preview.CropPixels();
                selectionLabel.Text = "Кадр:\n" + preview.Image.Width + "×" + preview.Image.Height +
                    "\nВыбрано: " + crop.Width + "×" + crop.Height;
                return;
            }

            double start;
            double end;
            if (!Fmt.TryNum(startInput.Value, out start) ||
                !Fmt.TryNum(endInput.Value, out end) || end <= start)
            {
                selectionLabel.Text = "Выбрано:\n—";
                return;
            }
            selectionLabel.Text = "Выбрано:\n" + Fmt.Time(end - start);
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
                if (IsDisposed) return;
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
                        preview.SetImage(task.Result, false);
                        saveButton.Enabled = !working;
                    });
                }
                catch { }
            });
        }

        private void Save()
        {
            if (working) return;
            if (isImage) SavePhoto();
            else SaveVideo();
        }

        private void SaveVideo()
        {
            double start;
            double end;
            if (!Fmt.TryNum(startInput.Value, out start) || start < 0)
            {
                MessageBox.Show(this, "Начало должно быть числом от 0.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Fmt.TryNum(endInput.Value, out end) || end <= 0)
            {
                MessageBox.Show(this, "Укажите конец обрезки в секундах.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            double duration = Duration();
            if (duration > 0 && end > duration) end = duration;
            if (end - start < 0.05)
            {
                MessageBox.Show(this, "Конец должен быть больше начала.", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (path.Length == 0) return;

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

                SetWorking(true, "Сохраняем видео…");
                Task.Factory.StartNew(delegate
                {
                    EditorCore.TrimVideo(source, output, from, to);
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
            working = busy;
            openButton.Enabled = !busy;
            startHereButton.Enabled = !busy;
            endHereButton.Enabled = !busy;
            resetButton.Enabled = !busy;
            startInput.Enabled = !busy;
            endInput.Enabled = !busy;
            seek.Enabled = !busy;
            saveButton.Enabled = !busy && preview.Image != null;
            statusLabel.Text = status;
            UseWaitCursor = busy;
        }
    }
}
