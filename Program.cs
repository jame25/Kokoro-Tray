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
                Logger.Info("Another instance is already running. Exiting.");
                MessageBox.Show("Another instance of Kokoro Tray is already running.", "Kokoro Tray", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
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
