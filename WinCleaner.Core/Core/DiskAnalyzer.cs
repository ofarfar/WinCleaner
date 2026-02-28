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
    public class DiskAnalyzer
    {
        /// <summary>Build a tree of directory sizes rooted at <paramref name="rootPath"/>.</summary>
        public async Task<DiskTreeNode> AnalyzeAsync(
            string rootPath,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            return await Task.Run(() => BuildNode(rootPath, progress, ct), ct);
        }

        /// <summary>Enumerate all ready fixed drives and return basic usage info.</summary>
        public List<DiskDriveInfo> GetDriveInfos()
        {
            var list = new List<DiskDriveInfo>();
            foreach (var drive in Win32Helper.GetFixedDrives())
            {
                list.Add(new DiskDriveInfo
                {
                    DriveLetter = drive.RootDirectory.FullName,
                    Label = drive.VolumeLabel,
                    TotalSize = drive.TotalSize,
                    FreeSpace = drive.AvailableFreeSpace
                });
            }
            return list;
        }

        private static DiskTreeNode BuildNode(string path, IProgress<string>? progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                return new DiskTreeNode
                {
                    Name = fi.Name,
                    FullPath = path,
                    Size = fi.Length,
                    IsDirectory = false
                };
            }

            var node = new DiskTreeNode
            {
                Name = Path.GetFileName(path) is { Length: > 0 } n ? n : path,
                FullPath = path,
                IsDirectory = true
            };

            try
            {
                var dir = new DirectoryInfo(path);
                progress?.Report(path);

                // Files directly in this directory
                foreach (var file in dir.GetFiles())
                {
                    ct.ThrowIfCancellationRequested();
                    node.Children.Add(new DiskTreeNode
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        Size = file.Length,
                        IsDirectory = false
                    });
                    node.Size += file.Length;
                }

                // Recurse into subdirectories
                foreach (var sub in dir.GetDirectories())
                {
                    ct.ThrowIfCancellationRequested();
                    var child = BuildNode(sub.FullName, progress, ct);
                    node.Children.Add(child);
                    node.Size += child.Size;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug($"Cannot access {path}: {ex.Message}");
            }

            return node;
        }
    }
}
