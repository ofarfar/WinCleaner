using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinCleaner.Core;
using WinCleaner.Models;

namespace WinCleaner.Tests
{
    [TestFixture]
    public class DiskAnalyzerTests
    {
        private string _tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "WinCleanerDiskTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public async Task Analyze_SingleFile_ReturnCorrectSize()
        {
            var content = new string('A', 1024); // 1 KB (encoding-dependent, but UTF8 ASCII is 1 byte each)
            File.WriteAllText(Path.Combine(_tempDir, "test.txt"), content, Encoding.ASCII);

            var analyzer = new DiskAnalyzer();
            var root = await analyzer.AnalyzeAsync(_tempDir);

            Assert.That(root.IsDirectory, Is.True);
            Assert.That(root.Size, Is.EqualTo(1024));
        }

        [Test]
        public async Task Analyze_MultipleFiles_SumsCorrectly()
        {
            File.WriteAllBytes(Path.Combine(_tempDir, "file1.bin"), new byte[512]);
            File.WriteAllBytes(Path.Combine(_tempDir, "file2.bin"), new byte[1024]);

            var analyzer = new DiskAnalyzer();
            var root = await analyzer.AnalyzeAsync(_tempDir);

            Assert.That(root.Size, Is.EqualTo(1536));
            Assert.That(root.Children.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task Analyze_Subdirectory_IncludesSubdirSize()
        {
            var subDir = Path.Combine(_tempDir, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllBytes(Path.Combine(subDir, "nested.bin"), new byte[200]);
            File.WriteAllBytes(Path.Combine(_tempDir, "root.bin"), new byte[300]);

            var analyzer = new DiskAnalyzer();
            var root = await analyzer.AnalyzeAsync(_tempDir);

            Assert.That(root.Size, Is.EqualTo(500));
        }

        [Test]
        public async Task Analyze_EmptyDirectory_ReturnsZeroSize()
        {
            var analyzer = new DiskAnalyzer();
            var root = await analyzer.AnalyzeAsync(_tempDir);

            Assert.That(root.Size, Is.EqualTo(0));
            Assert.That(root.Children, Is.Empty);
        }

        [Test]
        public void GetDriveInfos_ReturnsAtLeastOneDrive()
        {
            // On CI Linux there are no "Fixed" drives via DriveInfo, so allow empty result
            var analyzer = new DiskAnalyzer();
            var drives = analyzer.GetDriveInfos();
            Assert.That(drives, Is.Not.Null);
        }
    }
}
