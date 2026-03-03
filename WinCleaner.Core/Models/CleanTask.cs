using System;
using System.Collections.Generic;

namespace WinCleaner.Models
{
    public enum CleanTaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    public class CleanTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> TargetPaths { get; set; } = new();
        public CleanTaskStatus Status { get; set; } = CleanTaskStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }
        public long BytesFreed { get; set; }
        public int FilesDeleted { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
