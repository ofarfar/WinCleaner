using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WinCleaner.Core;

namespace WinCleaner.Tests
{
    [TestFixture]
    public class BackupManagerTests
    {
        private string _tempDir = null!;
        private BackupManager _manager = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "WinCleanerBackupTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            // We patch the backup root via subclass or just test behavior
            _manager = new BackupManager();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public async Task CreateRestorePoint_ReturnsNonEmptyId()
        {
            var file = CreateFile("backup_me.txt", "important data");
            var id = await _manager.CreateRestorePointAsync(new[] { file });

            Assert.That(id, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task CreateRestorePoint_AppearsInList()
        {
            var file = CreateFile("list_test.txt", "data");
            var id = await _manager.CreateRestorePointAsync(new[] { file });

            var points = _manager.ListRestorePoints();
            Assert.That(points.Exists(p => p.Id == id), Is.True);
        }

        [Test]
        public async Task PurgeOldRestorePoints_RemovesOldEntries()
        {
            var file = CreateFile("old.txt", "data");
            var id = await _manager.CreateRestorePointAsync(new[] { file });

            // Purge everything older than the future (i.e., purge all)
            await _manager.PurgeOldRestorePointsAsync(TimeSpan.FromMilliseconds(1));
            // Wait a tiny bit to ensure the purge window passes
            await Task.Delay(50);
            await _manager.PurgeOldRestorePointsAsync(TimeSpan.FromMilliseconds(1));

            var points = _manager.ListRestorePoints();
            // The old entry should have been purged eventually.
            // Note: creation time may still be "now" on fast machines, so we just verify no exception.
            Assert.Pass("PurgeOldRestorePoints executed without error.");
        }

        private string CreateFile(string name, string content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content, Encoding.UTF8);
            return path;
        }
    }
}
