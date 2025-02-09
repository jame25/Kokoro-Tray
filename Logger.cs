using System;
using System.IO;

namespace KokoroTray
{
    public static class Logger
    {
        private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system.log");
        private static readonly object LockObject = new object();

        public static void Info(string message)
        {
            Log("INFO", message);
        }

        public static void Error(string message, Exception ex = null)
        {
            Log("ERROR", message + (ex != null ? $"\nException: {ex.Message}\nStack Trace: {ex.StackTrace}" : ""));
        }

        public static void Debug(string message)
        {
            Log("DEBUG", message);
        }

        private static void Log(string level, string message)
        {
            try
            {
                lock (LockObject)
                {
                    var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFile, logMessage);
                }
            }
            catch
            {
                // Silently fail if logging fails
            }
        }
    }
} 