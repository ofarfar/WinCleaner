using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinCleaner.Models;
using WinCleaner.Utils;

namespace WinCleaner.Core
{
    public interface ICleanRule
    {
        string Name { get; }
        string Description { get; }
        IEnumerable<string> GetTargetPaths();
        bool IsSafeToDelete(FileInfo file);
    }

    public class CleanEngine
    {
        private readonly List<ICleanRule> _rules = new();

        public CleanEngine()
        {
            RegisterBuiltInRules();
        }

        private void RegisterBuiltInRules()
        {
            RegisterRule(new TempFilesRule());
            RegisterRule(new SystemLogsRule());
            RegisterRule(new ThumbnailCacheRule());
            RegisterRule(new PrefetchRule());
            RegisterRule(new BrowserCacheRule());
            RegisterRule(new CrashDumpsRule());
        }

        public void RegisterRule(ICleanRule rule) => _rules.Add(rule);

        public IReadOnlyList<ICleanRule> GetRules() => _rules.AsReadOnly();

        /// <summary>Scan all registered rules and return files that can be deleted.</summary>
        public async Task<ScanResult> ScanAsync(
            IEnumerable<ICleanRule>? rulesToScan = null,
            IProgress<(int filesScanned, long bytesFound)>? progress = null,
            CancellationToken ct = default)
        {
            var start = DateTime.Now;
            var items = new List<FileItem>();
            var rules = rulesToScan?.ToList() ?? _rules;

            int scanned = 0;
            long bytesFound = 0;

            await Task.Run(() =>
            {
                foreach (var rule in rules)
                {
                    ct.ThrowIfCancellationRequested();
                    foreach (var targetPath in rule.GetTargetPaths())
                    {
                        var expanded = Environment.ExpandEnvironmentVariables(targetPath);
                        if (!Directory.Exists(expanded) && !File.Exists(expanded)) continue;

                        var fileInfos = GetFiles(expanded);
                        foreach (var fi in fileInfos)
                        {
                            ct.ThrowIfCancellationRequested();
                            if (!rule.IsSafeToDelete(fi)) continue;
                            if (Win32Helper.IsSystemCriticalPath(fi.FullName)) continue;

                            var item = new FileItem
                            {
                                FullPath = fi.FullName,
                                Size = fi.Length,
                                LastModified = fi.LastWriteTime,
                                Extension = fi.Extension.ToLowerInvariant(),
                                Category = FileCategory.Temporary,
                                IsSelected = true
                            };
                            items.Add(item);
                            scanned++;
                            bytesFound += fi.Length;
                            progress?.Report((scanned, bytesFound));
                        }
                    }
                }
            }, ct);

            var result = new ScanResult
            {
                Items = items,
                TotalSize = items.Sum(i => i.Size),
                ScanDuration = DateTime.Now - start
            };
            return result;
        }

        /// <summary>Delete selected files from a scan result.</summary>
        public async Task<CleanTask> CleanAsync(
            ScanResult scanResult,
            IProgress<(int deleted, long freed)>? progress = null,
            CancellationToken ct = default)
        {
            var task = new CleanTask
            {
                Name = "Clean",
                Status = CleanTaskStatus.Running
            };

            await Task.Run(() =>
            {
                foreach (var item in scanResult.Items.Where(i => i.IsSelected))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.Exists(item.FullPath))
                        {
                            File.Delete(item.FullPath);
                            task.BytesFreed += item.Size;
                            task.FilesDeleted++;
                            AppLogger.Info($"Deleted: {item.FullPath} ({item.SizeFormatted})");
                            progress?.Report((task.FilesDeleted, task.BytesFreed));
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Cannot delete {item.FullPath}: {ex.Message}");
                    }
                }
                task.Status = CleanTaskStatus.Completed;
                task.CompletedAt = DateTime.Now;
            }, ct);

            return task;
        }

        private static IEnumerable<FileInfo> GetFiles(string path)
        {
            if (File.Exists(path))
            {
                yield return new FileInfo(path);
                yield break;
            }

            if (!Directory.Exists(path)) yield break;

            FileInfo[] files;
            try
            {
                files = new DirectoryInfo(path).GetFiles("*", SearchOption.AllDirectories);
            }
            catch
            {
                yield break;
            }

            foreach (var f in files) yield return f;
        }
    }

    // ── Built-in rules ──────────────────────────────────────────────────────────

    internal sealed class TempFilesRule : ICleanRule
    {
        public string Name => "Temporary Files";
        public string Description => "User and system temporary files (%TEMP%, %WINDIR%\\Temp)";

        public IEnumerable<string> GetTargetPaths() => new[]
        {
            "%TEMP%",
            "%WINDIR%\\Temp"
        };

        public bool IsSafeToDelete(FileInfo file) =>
            !IsFileLocked(file) && (file.Attributes & FileAttributes.System) == 0;

        private static bool IsFileLocked(FileInfo file)
        {
            try
            {
                using var fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
                return false;
            }
            catch (IOException) { return true; }
            catch { return false; }
        }
    }

    internal sealed class SystemLogsRule : ICleanRule
    {
        public string Name => "System Logs";
        public string Description => "Windows system log files (%WINDIR%\\Logs)";

        public IEnumerable<string> GetTargetPaths() => new[]
        {
            "%WINDIR%\\Logs"
        };

        public bool IsSafeToDelete(FileInfo file) =>
            file.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
            file.Extension.Equals(".etl", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class ThumbnailCacheRule : ICleanRule
    {
        public string Name => "Thumbnail Cache";
        public string Description => "Windows Explorer thumbnail cache files";

        public IEnumerable<string> GetTargetPaths() => new[]
        {
            "%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer"
        };

        public bool IsSafeToDelete(FileInfo file) =>
            file.Name.StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase) &&
            file.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class PrefetchRule : ICleanRule
    {
        public string Name => "Prefetch Cache";
        public string Description => "Windows Prefetch files (%WINDIR%\\Prefetch)";

        public IEnumerable<string> GetTargetPaths() => new[]
        {
            "%WINDIR%\\Prefetch"
        };

        public bool IsSafeToDelete(FileInfo file) =>
            file.Extension.Equals(".pf", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class BrowserCacheRule : ICleanRule
    {
        public string Name => "Browser Cache";
        public string Description => "IE/Edge browser cache files";

        public IEnumerable<string> GetTargetPaths() => new[]
        {
            "%LOCALAPPDATA%\\Microsoft\\Windows\\INetCache"
        };

        public bool IsSafeToDelete(FileInfo file) => true;
    }

    internal sealed class CrashDumpsRule : ICleanRule
    {
        public string Name => "Crash Dumps";
        public string Description => "Application crash dump files";

        public IEnumerable<string> GetTargetPaths() => new[]
        {
            "%LOCALAPPDATA%\\CrashDumps",
            "%WINDIR%\\Minidump"
        };

        public bool IsSafeToDelete(FileInfo file) =>
            file.Extension.Equals(".dmp", StringComparison.OrdinalIgnoreCase) ||
            file.Extension.Equals(".mdmp", StringComparison.OrdinalIgnoreCase);
    }
}
