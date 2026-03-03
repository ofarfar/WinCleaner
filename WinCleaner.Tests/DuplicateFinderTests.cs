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
    public class DuplicateFinderTests
    {
        private string _tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "WinCleanerDupTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public async Task FindDuplicates_IdenticalFiles_ReturnsThem()
        {
            WriteFile("a.txt", "same content");
            WriteFile("b.txt", "same content");
            WriteFile("c.txt", "different content");

            var finder = new DuplicateFinder();
            var result = await finder.FindDuplicatesAsync(new[] { _tempDir });

            Assert.That(result.Count, Is.EqualTo(1));
            var dupes = result.Values.First();
            Assert.That(dupes.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task FindDuplicates_NoDuplicates_ReturnsEmpty()
        {
            WriteFile("x.txt", "content one");
            WriteFile("y.txt", "content two");
            WriteFile("z.txt", "content three");

            var finder = new DuplicateFinder();
            var result = await finder.FindDuplicatesAsync(new[] { _tempDir });

            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task FindDuplicates_EmptyDirectory_ReturnsEmpty()
        {
            var finder = new DuplicateFinder();
            var result = await finder.FindDuplicatesAsync(new[] { _tempDir });
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task FindDuplicates_MultipleDuplicateGroups_ReturnsAllGroups()
        {
            WriteFile("g1a.txt", "group one");
            WriteFile("g1b.txt", "group one");
            WriteFile("g2a.txt", "group two");
            WriteFile("g2b.txt", "group two");
            WriteFile("unique.txt", "unique");

            var finder = new DuplicateFinder();
            var result = await finder.FindDuplicatesAsync(new[] { _tempDir });

            Assert.That(result.Count, Is.EqualTo(2));
            foreach (var group in result.Values)
                Assert.That(group.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetTopNLargestFiles_ReturnsSortedBySize()
        {
            var root = new DiskTreeNode
            {
                Name = "root",
                FullPath = _tempDir,
                IsDirectory = true,
                Children = new System.Collections.Generic.List<DiskTreeNode>
                {
                    new() { Name = "small.txt", FullPath = Path.Combine(_tempDir, "small.txt"), Size = 100, IsDirectory = false },
                    new() { Name = "large.txt", FullPath = Path.Combine(_tempDir, "large.txt"), Size = 5000, IsDirectory = false },
                    new() { Name = "medium.txt", FullPath = Path.Combine(_tempDir, "medium.txt"), Size = 1000, IsDirectory = false },
                }
            };

            var finder = new DuplicateFinder();
            var top2 = finder.GetTopNLargestFiles(root, 2);

            Assert.That(top2.Count, Is.EqualTo(2));
            Assert.That(top2[0].Size, Is.EqualTo(5000));
            Assert.That(top2[1].Size, Is.EqualTo(1000));
        }

        private void WriteFile(string name, string content)
        {
            File.WriteAllText(Path.Combine(_tempDir, name), content, Encoding.UTF8);
        }
    }
}
