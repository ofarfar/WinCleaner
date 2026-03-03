using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WinCleaner.Utils
{
    public static class HashHelper
    {
        /// <summary>Compute MD5 hash of a file.</summary>
        public static string ComputeMd5(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = md5.ComputeHash(stream);
            return BytesToHex(hash);
        }

        /// <summary>Compute SHA256 hash of a file.</summary>
        public static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha.ComputeHash(stream);
            return BytesToHex(hash);
        }

        /// <summary>Compute MD5 hash of the first <paramref name="byteCount"/> bytes of a file.</summary>
        public static string ComputePartialMd5(string filePath, int byteCount = 4096)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var buffer = new byte[Math.Min(byteCount, stream.Length)];
            int read = stream.Read(buffer, 0, buffer.Length);
            var hash = md5.ComputeHash(buffer, 0, read);
            return BytesToHex(hash);
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
