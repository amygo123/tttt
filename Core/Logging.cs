using System;
using System.IO;

namespace StyleWatcherWin
{
    /// <summary>
    /// Very small logging helper to record unexpected errors to a local file.
    /// This is intentionally dependency‑free and fail‑safe: any logging failure is swallowed.
    /// </summary>
    internal static class AppLogger
    {
        private static readonly object _sync = new();

        private static string GetLogFilePath()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(appData))
                {
                    // Fallback to base directory if LocalApplicationData is not available.
                    appData = AppContext.BaseDirectory;
                }

                var folder = Path.Combine(appData, "StyleWatcherWin", "logs");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "app.log");
                return file;
            }
            catch
            {
                // As a last resort, log next to the executable.
                try
                {
                    var folder = Path.Combine(AppContext.BaseDirectory, "logs");
                    Directory.CreateDirectory(folder);
                    return Path.Combine(folder, "app.log");
                }
                catch
                {
                    // Give up on logging path – caller will swallow errors.
                    return "app.log";
                }
            }
        }

        public static void LogError(Exception ex, string? context = null)
        {
            try
            {
                var path = GetLogFilePath();
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR {context ?? ""} {ex}\n";
                lock (_sync)
                {
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
                // Never throw from logger.
            }
        }

        public static void LogInfo(string message, string? context = null)
        {
            try
            {
                var path = GetLogFilePath();
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO {context ?? ""} {message}\n";
                lock (_sync)
                {
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
                // Never throw from logger.
            }
        }
    }
}
