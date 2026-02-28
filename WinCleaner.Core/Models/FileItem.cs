using System;

namespace WinCleaner.Models
{
    public enum FileCategory
    {
        Unknown,
        Image,
        Video,
        Document,
        Audio,
        Archive,
        Executable,
        Code,
        Temporary,
        Log,
        Cache
    }

    public class FileItem
    {
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Extension { get; set; } = string.Empty;
        public string? Md5Hash { get; set; }
        public bool IsSelected { get; set; } = true;
        public FileCategory Category { get; set; } = FileCategory.Unknown;

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

        public string FileName => System.IO.Path.GetFileName(FullPath);
    }
}
