using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Video2Gif
{
    /// <summary>Общий фон для всех окон: цвет или картинка/GIF с прозрачностью.</summary>
    internal static class BackgroundPainter
    {
        private static Image image;
        private static MemoryStream stream;
        private static EventHandler frameHandler;
        private static bool animated;
        private static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            BackgroundSettings.Load();
            BackgroundSettings.Changed += Refresh;
            Refresh();
        }

        public static void Refresh()
        {
            Stop();
            string path = BackgroundSettings.ImagePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    stream = new MemoryStream(File.ReadAllBytes(path));
                    image = Image.FromStream(stream);
                    if (ImageAnimator.CanAnimate(image))
                    {
                        frameHandler = OnFrame;
                        ImageAnimator.Animate(image, frameHandler);
                        animated = true;
                    }
                }
                catch
                {
                    Stop();
                }
            }
            InvalidateAll();
        }

        public static void Paint(Graphics g, Rectangle bounds)
        {
            double alpha = BackgroundSettings.BackgroundOpacity;
            if (alpha <= 0.001) return;

            if (image != null)
            {
                if (alpha >= 0.999)
                {
                    g.DrawImage(image, bounds);
                }
                else
                {
                    ColorMatrix matrix = new ColorMatrix();
                    matrix.Matrix33 = (float)alpha;
                    using (ImageAttributes attributes = new ImageAttributes())
                    {
                        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default,
                            ColorAdjustType.Bitmap);
                        g.DrawImage(image, bounds, 0, 0, image.Width, image.Height,
                            GraphicsUnit.Pixel, attributes);
                    }
                }
            }
            else if (BackgroundSettings.FillColor.HasValue)
            {
                Color color = BackgroundSettings.FillColor.Value;
                if (alpha < 0.999)
                {
                    color = Color.FromArgb((int)Math.Round(255 * alpha), color);
                }
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillRectangle(brush, bounds);
                }
            }
        }

        private static void OnFrame(object sender, EventArgs e)
        {
            InvalidateAll();
        }

        private static void InvalidateAll()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form == null || !form.IsHandleCreated || form.IsDisposed) continue;
                try
                {
                    form.BeginInvoke((MethodInvoker)delegate { form.Invalidate(true); });
                }
                catch
                {
                }
            }
        }

        private static void Stop()
        {
            if (image != null)
            {
                if (animated && frameHandler != null)
                {
                    try { ImageAnimator.StopAnimate(image, frameHandler); } catch { }
                }
                image.Dispose();
                image = null;
            }
            animated = false;
            frameHandler = null;
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }
        }
    }

    /// <summary>Окно с общим фоном приложения.</summary>
    internal class ThemedForm : Form
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            BackgroundPainter.Paint(e.Graphics, ClientRectangle);
            if (!string.IsNullOrEmpty(BackgroundSettings.ImagePath) ||
                BackgroundSettings.FillColor.HasValue)
            {
                using (SolidBrush veil = new SolidBrush(Color.FromArgb(150, Ui.Bg)))
                {
                    e.Graphics.FillRectangle(veil, ClientRectangle);
                }
            }
        }
    }
}
