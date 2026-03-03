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
    public class DuplicateFinder
    {
        /// <summary>
        /// Find duplicate files in <paramref name="directories"/> using a three-phase algorithm:
        /// 1. Group by size, 2. Group by partial MD5 (first 4 KB), 3. Group by full MD5.
        /// </summary>
        public async Task<Dictionary<string, List<FileItem>>> FindDuplicatesAsync(
            IEnumerable<string> directories,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            var result = new Dictionary<string, List<FileItem>>(StringComparer.Ordinal);

            await Task.Run(() =>
            {
                // Phase 1: collect all files and group by size
                progress?.Report("Phase 1: grouping by file size...");
                var allFiles = new List<FileInfo>();
                foreach (var dir in directories)
                {
                    if (!Directory.Exists(dir)) continue;
                    try
                    {
                        allFiles.AddRange(
                            new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories));
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Cannot enumerate {dir}: {ex.Message}");
                    }
                }

                var bySize = allFiles
                    .GroupBy(f => f.Length)
                    .Where(g => g.Count() > 1)
                    .ToList();

                // Phase 2: partial hash (first 4 KB)
                progress?.Report("Phase 2: computing partial hashes...");
                var candidates = new List<FileInfo>();
                foreach (var group in bySize)
                {
                    ct.ThrowIfCancellationRequested();
                    var byPartial = group
                        .GroupBy(f => TryHash(f.FullName, partial: true))
                        .Where(g => g.Key != null && g.Count() > 1);
                    foreach (var pg in byPartial)
                        candidates.AddRange(pg);
                }

                // Phase 3: full MD5
                progress?.Report("Phase 3: computing full MD5 hashes...");
                var byFullHash = candidates
                    .GroupBy(f => TryHash(f.FullName, partial: false))
                    .Where(g => g.Key != null && g.Count() > 1);

                foreach (var group in byFullHash)
                {
                    ct.ThrowIfCancellationRequested();
                    var hash = group.Key!;
                    result[hash] = group.Select(f => new FileItem
                    {
                        FullPath = f.FullName,
                        Size = f.Length,
                        LastModified = f.LastWriteTime,
                        Extension = f.Extension.ToLowerInvariant(),
                        Md5Hash = hash
                    }).ToList();
                }

            }, ct);

            return result;
        }

        /// <summary>Return the N largest files under <paramref name="root"/>.</summary>
        public List<FileItem> GetTopNLargestFiles(DiskTreeNode root, int n)
        {
            var all = new List<FileItem>();
            CollectFiles(root, all);
            return all.OrderByDescending(f => f.Size).Take(n).ToList();
        }

        private static void CollectFiles(DiskTreeNode node, List<FileItem> list)
        {
            if (!node.IsDirectory)
            {
                list.Add(new FileItem
                {
                    FullPath = node.FullPath,
                    Size = node.Size,
                    Extension = Path.GetExtension(node.FullPath).ToLowerInvariant()
                });
            }
            foreach (var child in node.Children)
                CollectFiles(child, list);
        }

        private static string? TryHash(string path, bool partial)
        {
            try
            {
                return partial
                    ? HashHelper.ComputePartialMd5(path)
                    : HashHelper.ComputeMd5(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
