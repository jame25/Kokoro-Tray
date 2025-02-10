using System;
using System.IO;

namespace KokoroTray
{
    public static class Logger
    {
        private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system.log");
        private static readonly object LockObject = new object();
        private static bool isEnabled = true;  // Default to enabled

        public static void SetEnabled(bool enabled)
        {
            lock (LockObject)
            {
                isEnabled = enabled;
                if (enabled)
                {
                    // Only log the state change if we're enabling logging
                    Log("INFO", $"Logging enabled");
                }
            }
        }

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
            // Skip non-error messages when logging is disabled
            if (!isEnabled && level != "ERROR")
                return;

            try
            {
                lock (LockObject)
                {
                    // Only create/append to log file if logging is enabled or if it's an error
                    if (isEnabled || level == "ERROR")
                    {
                        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                        File.AppendAllText(LogFile, logMessage);
                    }
                }
            }
            catch
            {
                // Silently fail if logging fails
            }
        }
    }
} 
