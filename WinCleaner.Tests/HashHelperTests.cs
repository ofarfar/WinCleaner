using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinCleaner.Utils;

namespace WinCleaner.Tests
{
    [TestFixture]
    public class HashHelperTests
    {
        private string _tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "WinCleanerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public void ComputeMd5_SameContent_ReturnsSameHash()
        {
            var path1 = CreateFile("a.txt", "hello world");
            var path2 = CreateFile("b.txt", "hello world");

            Assert.That(HashHelper.ComputeMd5(path1), Is.EqualTo(HashHelper.ComputeMd5(path2)));
        }

        [Test]
        public void ComputeMd5_DifferentContent_ReturnsDifferentHash()
        {
            var path1 = CreateFile("c.txt", "hello world");
            var path2 = CreateFile("d.txt", "hello WORLD");

            Assert.That(HashHelper.ComputeMd5(path1), Is.Not.EqualTo(HashHelper.ComputeMd5(path2)));
        }

        [Test]
        public void ComputeMd5_ReturnsLowercaseHex()
        {
            var path = CreateFile("e.txt", "test");
            var hash = HashHelper.ComputeMd5(path);
            Assert.That(hash, Does.Match("^[0-9a-f]{32}$"));
        }

        [Test]
        public void ComputeSha256_ReturnsCorrectLength()
        {
            var path = CreateFile("f.txt", "test data");
            var hash = HashHelper.ComputeSha256(path);
            Assert.That(hash.Length, Is.EqualTo(64)); // SHA256 = 32 bytes = 64 hex chars
        }

        [Test]
        public void ComputePartialMd5_LargerThanBuffer_DoesNotThrow()
        {
            // Write 8KB of data, partial should only read 4KB
            var bigContent = new string('x', 8192);
            var path = CreateFile("g.txt", bigContent);
            Assert.DoesNotThrow(() => HashHelper.ComputePartialMd5(path, 4096));
        }

        [Test]
        public void ComputePartialMd5_SamePrefix_ReturnsSameHash()
        {
            var content = new string('z', 8192);
            var path1 = CreateFile("h1.txt", content);
            // Same first 4KB, different afterwards
            var path2 = CreateFile("h2.txt", content[..4096] + new string('a', 4096));

            var hash1 = HashHelper.ComputePartialMd5(path1, 4096);
            var hash2 = HashHelper.ComputePartialMd5(path2, 4096);
            Assert.That(hash1, Is.EqualTo(hash2));
        }

        private string CreateFile(string name, string content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content, Encoding.UTF8);
            return path;
        }
    }
}
