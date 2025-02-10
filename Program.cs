using System;
using System.Windows.Forms;
using System.Threading;
using System.Security.Principal;

namespace KokoroTray
{
    internal static class Program
    {
        private static Mutex mutex = null;
        private const string MutexName = "KokoroTrayApplication_SingleInstance_Mutex";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;
            mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Don't log here since logging might be disabled
                MessageBox.Show("Another instance of Kokoro Tray is already running.", "Kokoro Tray", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Initialize logger state from settings before any logging occurs
                Logger.SetEnabled(Settings.Instance.GetSetting<bool>("EnableLogging", true));

                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                using (var trayApp = new TrayApplication())
                {
                    Application.Run();
                }
            }
            finally
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }
    }
}
