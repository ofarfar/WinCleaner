using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace WinCleaner.Config
{
    public class CleanRulesConfig
    {
        public bool CleanTempFiles { get; set; } = true;
        public bool CleanRecycleBin { get; set; } = true;
        public bool CleanSystemLogs { get; set; } = false;
        public bool CleanThumbnailCache { get; set; } = true;
        public bool CleanPrefetch { get; set; } = false;
        public bool CleanBrowserCache { get; set; } = true;
        public bool CleanWindowsUpdateCache { get; set; } = false;
        public bool CleanCrashDumps { get; set; } = true;
        public bool CleanInstallerPatchCache { get; set; } = false;
    }

    public class AppSettings
    {
        public string Language { get; set; } = "zh-CN";
        public bool EnableScanOnStartup { get; set; } = false;
        public bool ScheduledCleanEnabled { get; set; } = false;
        public int ScheduledCleanIntervalDays { get; set; } = 7;
        public bool BackupBeforeClean { get; set; } = true;
        public int MaxBackupAgeDays { get; set; } = 30;
        public bool PreviewBeforeClean { get; set; } = true;
        public long LargeFileSizeThresholdMB { get; set; } = 100;
        public CleanRulesConfig CleanRules { get; set; } = new();
        public Dictionary<string, List<string>> FileTypeMappings { get; set; } = new();
    }

    public static class ConfigManager
    {
        private static readonly string _configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Config", "AppConfig.json");

        private static AppSettings? _settings;

        public static AppSettings Settings => _settings ??= Load();

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                }
            }
            catch
            {
                _settings = new AppSettings();
            }
            return _settings;
        }

        public static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath)!;
                Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(_settings ?? new AppSettings(), Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Utils.AppLogger.Error("Failed to save configuration", ex);
            }
        }

        public static void Reload() => _settings = null;
    }
}
