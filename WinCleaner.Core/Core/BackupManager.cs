using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WinCleaner.Utils;

namespace WinCleaner.Core
{
    public class BackupManager
    {
        private const string MetadataEntryName = "__metadata__.json";
        private readonly string _backupRoot;

        public BackupManager()
        {
            _backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinCleaner", "Backups");
            Directory.CreateDirectory(_backupRoot);
        }

        /// <summary>Create a ZIP backup of <paramref name="filePaths"/> and return the restore-point ID.</summary>
        public async Task<string> CreateRestorePointAsync(
            IEnumerable<string> filePaths,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            var id = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..6];
            var zipPath = Path.Combine(_backupRoot, $"{id}.zip");

            await Task.Run(() =>
            {
                var files = filePaths.ToList();
                // metadata: entry name → original absolute path
                var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                int done = 0;
                for (int i = 0; i < files.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var filePath = files[i];
                    if (!File.Exists(filePath)) continue;
                    try
                    {
                        // Use a simple sequential entry name to avoid any path-encoding issues
                        var entryName = $"file_{i:D6}{Path.GetExtension(filePath)}";
                        zip.CreateEntryFromFile(filePath, entryName, CompressionLevel.Fastest);
                        metadata[entryName] = filePath;
                        AppLogger.Debug($"Backed up: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Backup skipped {filePath}: {ex.Message}");
                    }
                    progress?.Report(++done * 100 / files.Count);
                }

                // Write metadata as a JSON entry inside the ZIP
                var metaJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                var metaEntry = zip.CreateEntry(MetadataEntryName, CompressionLevel.Fastest);
                using var writer = new StreamWriter(metaEntry.Open(), Encoding.UTF8);
                writer.Write(metaJson);
            }, ct);

            AppLogger.Info($"Restore point created: {id}");
            return id;
        }

        /// <summary>Restore files from the specified restore-point ZIP.</summary>
        public async Task RestoreAsync(
            string restorePointId,
            IEnumerable<string>? filePaths = null,
            CancellationToken ct = default)
        {
            var zipPath = Path.Combine(_backupRoot, $"{restorePointId}.zip");
            if (!File.Exists(zipPath))
                throw new FileNotFoundException($"Restore point not found: {restorePointId}", zipPath);

            var filterSet = filePaths != null
                ? new HashSet<string>(filePaths, StringComparer.OrdinalIgnoreCase)
                : null;

            await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(zipPath);

                // Load metadata
                var metaEntry = zip.GetEntry(MetadataEntryName);
                if (metaEntry == null)
                {
                    AppLogger.Warn($"Restore point {restorePointId} has no metadata; skipping.");
                    return;
                }
                Dictionary<string, string> metadata;
                using (var reader = new StreamReader(metaEntry.Open(), Encoding.UTF8))
                    metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd())
                               ?? new Dictionary<string, string>();

                foreach (var entry in zip.Entries)
                {
                    if (entry.Name == MetadataEntryName) continue;
                    ct.ThrowIfCancellationRequested();

                    if (!metadata.TryGetValue(entry.FullName, out var originalPath)) continue;
                    if (filterSet != null && !filterSet.Contains(originalPath)) continue;

                    try
                    {
                        var destDir = Path.GetDirectoryName(originalPath)!;
                        Directory.CreateDirectory(destDir);
                        entry.ExtractToFile(originalPath, overwrite: true);
                        AppLogger.Info($"Restored: {originalPath}");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Restore failed for {entry.FullName}: {ex.Message}");
                    }
                }
            }, ct);
        }

        /// <summary>Delete restore points older than <paramref name="olderThan"/>.</summary>
        public async Task PurgeOldRestorePointsAsync(TimeSpan olderThan)
        {
            await Task.Run(() =>
            {
                var cutoff = DateTime.Now - olderThan;
                foreach (var zip in Directory.GetFiles(_backupRoot, "*.zip"))
                {
                    var fi = new FileInfo(zip);
                    if (fi.CreationTime < cutoff)
                    {
                        try
                        {
                            File.Delete(zip);
                            AppLogger.Info($"Purged old restore point: {fi.Name}");
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Warn($"Cannot purge {fi.Name}: {ex.Message}");
                        }
                    }
                }
            });
        }

        /// <summary>List all available restore-point IDs sorted newest-first.</summary>
        public List<(string Id, DateTime CreatedAt, long SizeBytes)> ListRestorePoints()
        {
            return Directory.GetFiles(_backupRoot, "*.zip")
                .Select(f =>
                {
                    var fi = new FileInfo(f);
                    return (Id: Path.GetFileNameWithoutExtension(f), CreatedAt: fi.CreationTime, SizeBytes: fi.Length);
                })
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }
    }
}
