using System;
using System.Collections.Generic;

namespace WinCleaner.Models
{
    public class DiskTreeNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool IsDirectory { get; set; }
        public List<DiskTreeNode> Children { get; set; } = new();

        public string SizeFormatted
        {
            get
            {
                if (Size < 1024) return $"{Size} B";
                if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
                if (Size < 1024 * 1024 * 1024) return $"{Size / (1024.0 * 1024):F1} MB";
                return $"{Size / (1024.0 * 1024 * 1024):F2} GB";
            }
        }
    }

    public class DiskDriveInfo
    {
        public string DriveLetter { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long FreeSpace { get; set; }
        public long UsedSpace => TotalSize - FreeSpace;
        public double UsedPercent => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;
    }

    public class ConsolidationOptions
    {
        public bool GroupByExtension { get; set; } = true;
        public bool GroupByDate { get; set; }
        public bool GroupBySize { get; set; }
        public long LargeFileSizeThreshold { get; set; } = 100 * 1024 * 1024; // 100 MB
        public string TargetDirectory { get; set; } = string.Empty;
    }

    public class ConsolidationPlan
    {
        public List<ConsolidationMove> Moves { get; set; } = new();
        public string SourceDirectory { get; set; } = string.Empty;
        public ConsolidationOptions Options { get; set; } = new();
    }

    public class ConsolidationMove
    {
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}
