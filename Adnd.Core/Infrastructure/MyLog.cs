using System;
using System.IO;

namespace Adnd.Core.Infrastructure
{

    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }

    public static class Log
    {
        private static readonly object _lock = new object();
        private static string? _logFilePath;

        /// <summary>
        /// Path to the log file. If not set, defaults to "app.log" in the application's base directory.
        /// </summary>
        public static string LogFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_logFilePath))
                {
                    // Default to the requested central log folder
                    var defaultDir = @"C:\dev\logs";
                    _logFilePath = Path.Combine(defaultDir, "app.log");
                }
                return _logFilePath!;
            }
            set => _logFilePath = value;
        }

        /// <summary>
        /// Write a log entry to the logfile. Thread-safe and tolerant of IO errors.
        /// </summary>
        /// <param name="level">Log level</param>
        /// <param name="message">Log message</param>
        public static void Write(LogLevel level, string message)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            lock (_lock)
            {
                try
                {
                    var folder = Path.GetDirectoryName(LogFilePath);
                    if (!string.IsNullOrEmpty(folder))
                        Directory.CreateDirectory(folder);

                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
                catch
                {
                    // Intentionally swallow exceptions to avoid bringing down the app when logging fails.
                    // In production you may want to handle this differently (fallback, event log, etc.).
                }
            }
        }
    }
}

