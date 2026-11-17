using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace Video2Gif
{
    /// <summary>
    /// Сертификат подписи лежит внутри exe. При первом запуске программа
    /// сама добавляет его в доверенные текущего пользователя, чтобы Windows
    /// показывала издателя, а не «Неизвестный издатель».
    /// </summary>
    internal static class CertificateTrust
    {
        private const string ResourceName = "Video2Gif.codesign.cer";

        public static void EnsureInstalled()
        {
            try
            {
                byte[] data = ReadResource();
                if (data == null || data.Length == 0) return;

                var cert = new X509Certificate2(data);
                if (!SignedByThisCertificate(cert)) return;
                if (IsTrusted(cert.Thumbprint)) return;

                Install(data);
            }
            catch
            {
                // Подпись — не критичная часть: при ошибке просто продолжаем.
            }
        }

        private static byte[] ReadResource()
        {
            using (Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(ResourceName))
            {
                if (stream == null) return null;
                var data = new byte[stream.Length];
                int offset = 0;
                while (offset < data.Length)
                {
                    int read = stream.Read(data, offset, data.Length - offset);
                    if (read <= 0) break;
                    offset += read;
                }
                if (offset == data.Length) return data;
                var trimmed = new byte[offset];
                Array.Copy(data, trimmed, offset);
                return trimmed;
            }
        }

        private static bool SignedByThisCertificate(X509Certificate2 cert)
        {
            try
            {
                var signer = new X509Certificate2(
                    X509Certificate.CreateFromSignedFile(Application.ExecutablePath));
                return string.Equals(signer.Thumbprint, cert.Thumbprint,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTrusted(string thumbprint)
        {
            return InStore(StoreName.Root, thumbprint) &&
                   InStore(StoreName.TrustedPublisher, thumbprint);
        }

        private static bool InStore(StoreName name, string thumbprint)
        {
            try
            {
                using (var store = new X509Store(name, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadOnly);
                    foreach (X509Certificate2 cert in store.Certificates)
                    {
                        if (string.Equals(cert.Thumbprint, thumbprint,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static void Install(byte[] data)
        {
            string temp = Path.Combine(Path.GetTempPath(), "video2gif-codesign.cer");
            try
            {
                File.WriteAllBytes(temp, data);
                RunCertutil("-user -addstore -f Root \"" + temp + "\"");
                RunCertutil("-user -addstore -f TrustedPublisher \"" + temp + "\"");
            }
            finally
            {
                try { File.Delete(temp); } catch { }
            }
        }

        private static void RunCertutil(string arguments)
        {
            try
            {
                var info = new ProcessStartInfo("certutil.exe", arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process process = Process.Start(info))
                {
                    if (process != null)
                    {
                        process.WaitForExit(15000);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
