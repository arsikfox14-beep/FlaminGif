using System;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("FlaminGif")]
[assembly: System.Reflection.AssemblyDescription("FlaminGif — видео и картинки в GIF, скачивание TikTok, YouTube и музыки")]
[assembly: System.Reflection.AssemblyProduct("FlaminGif")]
[assembly: System.Reflection.AssemblyVersion("5.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("5.3.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("5.3")]

namespace Video2Gif
{
    internal static class Program
    {
        public const string AppName = "FlaminGif";
        public const string Version = "5.3";

        /// <summary>Самопроверка: окна не должны лезть в сеть за обновлениями.</summary>
        public static bool SelfTestMode;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
            {
                SelfTestMode = true;
                return SelfTest.Run();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppSettings.Load();
            Loc.Load();
            Loc.InstallWatcher();
            BackgroundPainter.Install();
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось запустить приложение.\n\n" + ex.Message,
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
            return 0;
        }
    }
}
