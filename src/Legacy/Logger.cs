using System;
using System.IO;
using System.Text;

namespace P7SExtractor
{
    public static class Logger
    {
        private static readonly object Sync = new();

        public static string LogsFolder
            => Path.Combine(AppContext.BaseDirectory, "Logs");

        public static void LogInfo(string action, string? filePath = null, string? detectedType = null, string? signatureStatus = null, string? message = null)
            => Write("INFO", action, filePath, detectedType, signatureStatus, message, null);

        public static void LogWarning(string action, string? filePath = null, string? detectedType = null, string? signatureStatus = null, string? message = null)
            => Write("WARN", action, filePath, detectedType, signatureStatus, message, null);

        public static void LogError(string action, Exception ex, string? filePath = null, string? detectedType = null, string? signatureStatus = null, string? message = null)
            => Write("ERROR", action, filePath, detectedType, signatureStatus, message, ex);

        private static void Write(string level, string action, string? filePath, string? detectedType, string? signatureStatus, string? message, Exception? ex)
        {
            try
            {
                Directory.CreateDirectory(LogsFolder);

                string logFile = Path.Combine(LogsFolder, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                var sb = new StringBuilder();
                sb.Append('[').Append(timestamp).Append("] [").Append(level).Append("] ");
                sb.Append(action);

                if (!string.IsNullOrWhiteSpace(filePath))
                    sb.Append(" | file=").Append(filePath);

                if (!string.IsNullOrWhiteSpace(detectedType))
                    sb.Append(" | type=").Append(detectedType);

                if (!string.IsNullOrWhiteSpace(signatureStatus))
                    sb.Append(" | signature=").Append(signatureStatus);

                if (!string.IsNullOrWhiteSpace(message))
                    sb.Append(" | msg=").Append(message);

                if (ex != null)
                {
                    sb.AppendLine();
                    sb.Append(ex);
                }

                string line = sb.ToString();

                lock (Sync)
                {
                    File.AppendAllText(logFile, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}
