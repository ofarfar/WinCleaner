using System;
using System.Runtime.InteropServices;

namespace WinCleaner.Utils
{
    /// <summary>Helper for reading/writing Windows registry values.</summary>
    public static class RegistryHelper
    {
        /// <summary>Read a string value from the registry. Returns null on non-Windows platforms or if the key is missing.</summary>
        public static string? ReadString(string keyPath, string valueName)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath, writable: false);
                return key?.GetValue(valueName)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Write a string value to the registry (requires appropriate permissions).</summary>
        public static bool WriteString(string keyPath, string valueName, string value)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath, writable: true);
                if (key == null) return false;
                key.SetValue(valueName, value, Microsoft.Win32.RegistryValueKind.String);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Check if the application is configured to run at Windows startup.</summary>
        public static bool IsStartupEnabled(string appName)
        {
            const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            return ReadString(runKey, appName) != null;
        }

        /// <summary>Enable or disable running the application at Windows startup.</summary>
        public static bool SetStartupEnabled(string appName, string exePath, bool enable)
        {
            const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            if (enable) return WriteString(runKey, appName, $"\"{exePath}\"");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(runKey, writable: true);
                key?.DeleteValue(appName, throwOnMissingValue: false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
