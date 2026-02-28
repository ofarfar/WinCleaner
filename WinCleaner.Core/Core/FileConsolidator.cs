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
    public class FileConsolidator
    {
        // Built-in file-type → extension mapping
        private static readonly Dictionary<string, HashSet<string>> _typeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Images"]      = new(StringComparer.OrdinalIgnoreCase) { ".jpg",".jpeg",".png",".gif",".bmp",".webp",".svg",".tiff",".ico" },
            ["Videos"]      = new(StringComparer.OrdinalIgnoreCase) { ".mp4",".avi",".mkv",".mov",".wmv",".flv",".m4v",".3gp" },
            ["Documents"]   = new(StringComparer.OrdinalIgnoreCase) { ".doc",".docx",".pdf",".xls",".xlsx",".ppt",".pptx",".txt",".odt",".rtf" },
            ["Audio"]       = new(StringComparer.OrdinalIgnoreCase) { ".mp3",".wav",".flac",".aac",".ogg",".wma",".m4a" },
            ["Archives"]    = new(StringComparer.OrdinalIgnoreCase) { ".zip",".rar",".7z",".tar",".gz",".bz2",".xz" },
            ["Executables"] = new(StringComparer.OrdinalIgnoreCase) { ".exe",".msi",".bat",".cmd",".ps1" },
            ["Code"]        = new(StringComparer.OrdinalIgnoreCase) { ".cs",".py",".js",".ts",".java",".cpp",".c",".h",".go",".rs",".json",".xml" },
        };

        // Stores the last executed plan for undo support
        private ConsolidationPlan? _lastPlan;

        /// <summary>
        /// Analyse <paramref name="sourceDir"/> and return a consolidation plan without moving files.
        /// Only files directly in <paramref name="sourceDir"/> are analysed (non-recursive).
        /// </summary>
        public async Task<ConsolidationPlan> AnalyzeAsync(
            string sourceDir,
            ConsolidationOptions options,
            CancellationToken ct = default)
        {
            var plan = new ConsolidationPlan
            {
                SourceDirectory = sourceDir,
                Options = options
            };

            await Task.Run(() =>
            {
                if (!Directory.Exists(sourceDir)) return;

                var files = Directory.EnumerateFiles(sourceDir, "*", SearchOption.TopDirectoryOnly);
                foreach (var filePath in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var fi = new FileInfo(filePath);
                    var dest = BuildDestinationPath(fi, options);
                    if (dest == null) continue;

                    plan.Moves.Add(new ConsolidationMove
                    {
                        SourcePath = filePath,
                        DestinationPath = dest,
                        Size = fi.Length
                    });
                }
            }, ct);

            return plan;
        }

        /// <summary>Execute the consolidation plan (move or copy files).</summary>
        public async Task ExecuteAsync(
            ConsolidationPlan plan,
            bool copyMode,
            IProgress<(int processed, long bytes)>? progress = null,
            CancellationToken ct = default)
        {
            _lastPlan = plan;
            int processed = 0;
            long bytes = 0;

            await Task.Run(() =>
            {
                foreach (var move in plan.Moves)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var destDir = Path.GetDirectoryName(move.DestinationPath)!;
                        Directory.CreateDirectory(destDir);

                        var dest = GetUniqueDestPath(move.DestinationPath);

                        if (copyMode)
                            File.Copy(move.SourcePath, dest, overwrite: false);
                        else
                            File.Move(move.SourcePath, dest);

                        bytes += move.Size;
                        processed++;
                        AppLogger.Info($"{(copyMode ? "Copied" : "Moved")}: {move.SourcePath} → {dest}");
                        progress?.Report((processed, bytes));
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Cannot process {move.SourcePath}: {ex.Message}");
                    }
                }
            }, ct);
        }

        /// <summary>Undo the last consolidation by moving files back to their original locations.</summary>
        public async Task UndoLastConsolidationAsync()
        {
            if (_lastPlan == null) return;

            var plan = _lastPlan;
            _lastPlan = null;

            await Task.Run(() =>
            {
                foreach (var move in plan.Moves)
                {
                    try
                    {
                        if (File.Exists(move.DestinationPath) && !File.Exists(move.SourcePath))
                        {
                            var srcDir = Path.GetDirectoryName(move.SourcePath)!;
                            Directory.CreateDirectory(srcDir);
                            File.Move(move.DestinationPath, move.SourcePath);
                            AppLogger.Info($"Undone: {move.DestinationPath} → {move.SourcePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Undo failed for {move.DestinationPath}: {ex.Message}");
                    }
                }
            });
        }

        private static string? BuildDestinationPath(FileInfo fi, ConsolidationOptions options)
        {
            if (string.IsNullOrEmpty(options.TargetDirectory)) return null;

            string subFolder;

            if (options.GroupByExtension)
            {
                subFolder = GetTypeFolder(fi.Extension);
            }
            else if (options.GroupByDate)
            {
                subFolder = Path.Combine(fi.LastWriteTime.Year.ToString(),
                                         fi.LastWriteTime.Month.ToString("D2"));
            }
            else if (options.GroupBySize)
            {
                subFolder = fi.Length >= options.LargeFileSizeThreshold ? "LargeFiles" : "SmallFiles";
            }
            else
            {
                return null;
            }

            return Path.Combine(options.TargetDirectory, subFolder, fi.Name);
        }

        private static string GetTypeFolder(string extension)
        {
            foreach (var kv in _typeMap)
                if (kv.Value.Contains(extension))
                    return kv.Key;
            return "Other";
        }

        private static string GetUniqueDestPath(string dest)
        {
            if (!File.Exists(dest)) return dest;
            var dir = Path.GetDirectoryName(dest)!;
            var name = Path.GetFileNameWithoutExtension(dest);
            var ext = Path.GetExtension(dest);
            int i = 1;
            string candidate;
            do { candidate = Path.Combine(dir, $"{name}_{i++}{ext}"); }
            while (File.Exists(candidate));
            return candidate;
        }
    }
}
