using System;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace WinCleaner.Utils
{
    public static class AppLogger
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        static AppLogger()
        {
            ConfigureLogging();
        }

        private static void ConfigureLogging()
        {
            var config = new LoggingConfiguration();

            // Console target
            var consoleTarget = new ConsoleTarget("console")
            {
                Layout = "${longdate} [${level:uppercase=true}] ${message} ${exception:format=tostring}"
            };

            // File target
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinCleaner", "Logs");
            Directory.CreateDirectory(logDir);

            var fileTarget = new FileTarget("file")
            {
                FileName = Path.Combine(logDir, "wincleaner-${shortdate}.log"),
                Layout = "${longdate} [${level:uppercase=true}] ${callsite} ${message} ${exception:format=tostring}",
                ArchiveAboveSize = 5 * 1024 * 1024, // 5 MB
                MaxArchiveFiles = 10,
                ArchiveNumbering = ArchiveNumberingMode.Rolling
            };

            config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);
            LogManager.Configuration = config;
        }

        public static void Info(string message) => _logger.Info(message);
        public static void Debug(string message) => _logger.Debug(message);
        public static void Warn(string message) => _logger.Warn(message);
        public static void Error(string message, Exception? ex = null)
        {
            if (ex != null) _logger.Error(ex, message);
            else _logger.Error(message);
        }
        public static void Fatal(string message, Exception? ex = null)
        {
            if (ex != null) _logger.Fatal(ex, message);
            else _logger.Fatal(message);
        }

        public static string GetLogDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinCleaner", "Logs");
        }
    }
}
