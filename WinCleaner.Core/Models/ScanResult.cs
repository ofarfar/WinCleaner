using System;
using System.Collections.Generic;

namespace WinCleaner.Models
{
    public class ScanResult
    {
        public List<FileItem> Items { get; set; } = new();
        public long TotalSize { get; set; }
        public int TotalCount => Items.Count;
        public TimeSpan ScanDuration { get; set; }
        public Dictionary<FileCategory, long> SizeByCategory { get; set; } = new();
        public DateTime ScannedAt { get; set; } = DateTime.Now;

        public string TotalSizeFormatted
        {
            get
            {
                if (TotalSize < 1024) return $"{TotalSize} B";
                if (TotalSize < 1024 * 1024) return $"{TotalSize / 1024.0:F1} KB";
                if (TotalSize < 1024 * 1024 * 1024) return $"{TotalSize / (1024.0 * 1024):F1} MB";
                return $"{TotalSize / (1024.0 * 1024 * 1024):F2} GB";
            }
        }
    }
}
