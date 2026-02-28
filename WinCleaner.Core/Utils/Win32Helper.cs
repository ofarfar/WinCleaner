using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinCleaner.Utils
{
    /// <summary>Win32 API helpers for extended file operations.</summary>
    public static class Win32Helper
    {
        // Long path prefix to bypass MAX_PATH (260 chars) on Windows
        private const string LongPathPrefix = @"\\?\";
        private const int MaxPath = 260;

        /// <summary>Prefix a path with the long-path prefix when running on Windows.</summary>
        public static string ToLongPath(string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return path;
            if (path.StartsWith(LongPathPrefix, StringComparison.Ordinal)) return path;
            if (path.Length < MaxPath) return path;
            return LongPathPrefix + path;
        }

        /// <summary>Check whether the current process has administrator privileges.</summary>
        public static bool IsRunningAsAdmin()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if <paramref name="path"/> is inside a system-critical directory
        /// (%WINDIR% or %PROGRAMFILES%) and should not be deleted.
        /// </summary>
        public static bool IsSystemCriticalPath(string path)
        {
            var protectedRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            };

            foreach (var root in protectedRoots)
            {
                if (string.IsNullOrEmpty(root)) continue;
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>Returns the total and free disk space for the specified drive.</summary>
        public static (long TotalBytes, long FreeBytes) GetDriveSpace(string drivePath)
        {
            try
            {
                var info = new DriveInfo(drivePath);
                return (info.TotalSize, info.AvailableFreeSpace);
            }
            catch
            {
                return (0, 0);
            }
        }

        /// <summary>Enumerate all ready fixed drives on the system.</summary>
        public static IEnumerable<DriveInfo> GetFixedDrives()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    yield return drive;
            }
        }
    }
}
