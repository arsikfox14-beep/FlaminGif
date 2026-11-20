using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Video2Gif
{
    /// <summary>
    /// Лёгкая нейросеть U²-Netp (4,5 МБ) для выделения объекта.
    /// Модель и ONNX Runtime встроены в exe и распаковываются в
    /// %LOCALAPPDATA%\Video2Gif\ai при первом использовании.
    /// U²-Net: https://github.com/xuebinqin/U-2-Net (Apache 2.0).
    /// ONNX Runtime: https://onnxruntime.ai (MIT).
    /// </summary>
    internal static class AiCutout
    {
        private const string Version = "u2netp-1|ort-1.19.2";
        private const int InputSize = 320;

        private static readonly object Gate = new object();
        private static bool installed;
        private static bool installTried;

        public static string Folder
        {
            get { return Path.Combine(AppSettings.Folder, "ai"); }
        }

        public static string ModelPath
        {
            get { return Path.Combine(Folder, "u2netp.onnx"); }
        }

        public static bool Available
        {
            get
            {
                try
                {
                    Install();
                    return File.Exists(ModelPath);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>Распаковка модели и рантайма, установка путей поиска DLL.</summary>
        public static void Install()
        {
            lock (Gate)
            {
                if (installed) return;
                if (installTried && !File.Exists(ModelPath)) return;

                string folder = Folder;
                Directory.CreateDirectory(folder);

                string[] files =
                {
                    "onnxruntime.dll", "Microsoft.ML.OnnxRuntime.dll",
                    "System.Memory.dll", "System.Buffers.dll",
                    "System.Runtime.CompilerServices.Unsafe.dll",
                    "System.Numerics.Vectors.dll", "u2netp.onnx"
                };

                bool need = AppSettings.Get("ai.version", "") != Version;
                foreach (string name in files)
                {
                    if (!File.Exists(Path.Combine(folder, name)))
                    {
                        need = true;
                        break;
                    }
                }

                if (need)
                {
                    try
                    {
                        Extract("ai-native.gz", "onnxruntime.dll");
                        Extract("ai-ort.gz", "Microsoft.ML.OnnxRuntime.dll");
                        Extract("ai-memory.gz", "System.Memory.dll");
                        Extract("ai-buffers.gz", "System.Buffers.dll");
                        Extract("ai-unsafe.gz", "System.Runtime.CompilerServices.Unsafe.dll");
                        Extract("ai-vectors.gz", "System.Numerics.Vectors.dll");
                        Extract("ai-model.gz", "u2netp.onnx");
                        AppSettings.Set("ai.version", Version);
                    }
                    catch
                    {
                        installTried = true;
                        throw;
                    }
                }

                try
                {
                    SetDllDirectory(folder);
                    LoadLibrary(Path.Combine(folder, "onnxruntime.dll"));
                }
                catch
                {
                }

                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
                installed = true;
                installTried = true;
            }
        }

        private static void Extract(string resource, string fileName)
        {
            string target = Path.Combine(Folder, fileName);
            string temp = target + ".tmp";
            using (Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resource))
            {
                if (stream == null)
                    throw new InvalidOperationException("В сборке нет " + resource);
                using (GZipStream gzip = new GZipStream(stream, CompressionMode.Decompress))
                using (FileStream file = new FileStream(temp, FileMode.Create,
                    FileAccess.Write, FileShare.None))
                {
                    gzip.CopyTo(file, 1 << 20);
                }
            }
            if (File.Exists(target)) File.Delete(target);
            File.Move(temp, target);
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            try
            {
                string name = new AssemblyName(args.Name).Name;
                string path = Path.Combine(Folder, name + ".dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            catch
            {
            }
            return null;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string path);

        /// <summary>Мягкая маска 320×320: 0 — фон, 1 — объект.</summary>
        public static float[] Segment(Bitmap source)
        {
            Install();
            return OrtEngine.Segment(source);
        }

        /// <summary>Применяет маску к картинке, возвращает PNG-совместимый Bitmap.</summary>
        public static Bitmap ApplyMask(Bitmap source, float[] mask,
            int feather, int threshold)
        {
            int width = source.Width;
            int height = source.Height;
            float[] soft = mask;
            if (feather > 0)
            {
                int radius = (int)Math.Round(feather * 0.6);
                if (radius < 1) radius = 1;
                if (radius > 8) radius = 8;
                soft = BlurMask(mask, InputSize, InputSize, radius);
            }

            float low = 0.03f + Math.Max(0, Math.Min(100, threshold)) / 100f * 0.42f;
            float high = Math.Min(0.96f, low + 0.22f);

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

                for (int y = 0; y < height; y++)
                {
                    float sy = (y + 0.5f) * InputSize / height - 0.5f;
                    for (int x = 0; x < width; x++)
                    {
                        float sx = (x + 0.5f) * InputSize / width - 0.5f;
                        float value = SampleMask(soft, sx, sy);
                        if (value < low) value = 0f;
                        else if (value > high) value = 1f;
                        else
                        {
                            value = (value - low) / (high - low);
                            value = value * value * (3f - 2f * value);
                        }

                        int index = y * width + x;
                        int sourceAlpha = (pixels[index] >> 24) & 0xFF;
                        int alpha = (int)Math.Round(sourceAlpha * value);
                        if (alpha < 0) alpha = 0;
                        if (alpha > 255) alpha = 255;
                        pixels[index] = (pixels[index] & 0x00FFFFFF) | (alpha << 24);
                    }
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

        private static float SampleMask(float[] mask, float x, float y)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            float fx = x - x0;
            float fy = y - y0;
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;
            if (x1 < 0) x1 = 0;
            if (y1 < 0) y1 = 0;
            if (x0 >= InputSize) x0 = InputSize - 1;
            if (y0 >= InputSize) y0 = InputSize - 1;
            if (x1 >= InputSize) x1 = InputSize - 1;
            if (y1 >= InputSize) y1 = InputSize - 1;

            float top = mask[y0 * InputSize + x0] * (1 - fx) +
                mask[y0 * InputSize + x1] * fx;
            float bottom = mask[y1 * InputSize + x0] * (1 - fx) +
                mask[y1 * InputSize + x1] * fx;
            return top * (1 - fy) + bottom * fy;
        }

        private static float[] BlurMask(float[] source, int width, int height, int radius)
        {
            float[] temp = new float[source.Length];
            int window = radius * 2 + 1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                float sum = 0;
                for (int i = -radius; i <= radius; i++)
                    sum += source[row + Clamp(i, 0, width - 1)];
                for (int x = 0; x < width; x++)
                {
                    temp[row + x] = sum / window;
                    int add = Clamp(x + radius + 1, 0, width - 1);
                    int sub = Clamp(x - radius, 0, width - 1);
                    sum += source[row + add] - source[row + sub];
                }
            }

            float[] result = new float[source.Length];
            for (int x = 0; x < width; x++)
            {
                float sum = 0;
                for (int i = -radius; i <= radius; i++)
                    sum += temp[Clamp(i, 0, height - 1) * width + x];
                for (int y = 0; y < height; y++)
                {
                    result[y * width + x] = sum / window;
                    int add = Clamp(y + radius + 1, 0, height - 1);
                    int sub = Clamp(y - radius, 0, height - 1);
                    sum += temp[add * width + x] - temp[sub * width + x];
                }
            }
            return result;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        /// <summary>Изолирует типы ONNX Runtime: они грузятся только при первом запуске.</summary>
        private static class OrtEngine
        {
            private static readonly object SessionGate = new object();
            private static InferenceSession session;

            public static float[] Segment(Bitmap source)
            {
                lock (SessionGate)
                {
                    if (session == null)
                    {
                        session = new InferenceSession(AiCutout.ModelPath);
                    }

                    float[] input = BuildInput(source);
                    DenseTensor<float> tensor = new DenseTensor<float>(
                        new Memory<float>(input),
                        new[] { 1, 3, InputSize, InputSize });

                    string inputName = null;
                    foreach (KeyValuePair<string, NodeMetadata> item in session.InputMetadata)
                    {
                        inputName = item.Key;
                        break;
                    }
                    if (inputName == null) inputName = "input.1";

                    var values = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor(inputName, tensor)
                    };

                    using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results =
                        session.Run(values))
                    {
                        foreach (DisposableNamedOnnxValue value in results)
                        {
                            Tensor<float> output = value.AsTensor<float>();
                            if (output.Length != InputSize * InputSize) continue;
                            return Normalize(output);
                        }
                    }
                    throw new InvalidOperationException("Нейросеть не вернула маску.");
                }
            }

            private static float[] BuildInput(Bitmap source)
            {
                float[] data = new float[3 * InputSize * InputSize];
                using (Bitmap small = new Bitmap(InputSize, InputSize,
                    PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(small))
                    {
                        g.Clear(Color.Black);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(source, 0, 0, InputSize, InputSize);
                    }

                    BitmapData bits = small.LockBits(
                        new Rectangle(0, 0, InputSize, InputSize),
                        ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        int stride = bits.Stride;
                        byte[] row = new byte[stride];
                        float[] mean = { 0.485f, 0.456f, 0.406f };
                        float[] std = { 0.229f, 0.224f, 0.225f };
                        for (int y = 0; y < InputSize; y++)
                        {
                            Marshal.Copy(new IntPtr(bits.Scan0.ToInt64() + (long)y * stride),
                                row, 0, stride);
                            for (int x = 0; x < InputSize; x++)
                            {
                                int offset = x * 3;
                                int i = y * InputSize + x;
                                data[i] = (row[offset + 2] / 255f - mean[0]) / std[0];
                                data[InputSize * InputSize + i] =
                                    (row[offset + 1] / 255f - mean[1]) / std[1];
                                data[2 * InputSize * InputSize + i] =
                                    (row[offset] / 255f - mean[2]) / std[2];
                            }
                        }
                    }
                    finally
                    {
                        small.UnlockBits(bits);
                    }
                }
                return data;
            }

            private static float[] Normalize(Tensor<float> output)
            {
                float min = float.MaxValue;
                float max = float.MinValue;
                for (int i = 0; i < output.Length; i++)
                {
                    float value = output.GetValue(i);
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
                float range = Math.Max(0.0001f, max - min);
                float[] mask = new float[InputSize * InputSize];
                for (int i = 0; i < mask.Length; i++)
                    mask[i] = (output.GetValue(i) - min) / range;
                return mask;
            }
        }
    }
}
