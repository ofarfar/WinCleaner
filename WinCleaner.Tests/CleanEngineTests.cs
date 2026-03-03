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
    public class CleanEngineTests
    {
        private string _tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "WinCleanerEngineTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public void GetRules_ReturnsBuiltInRules()
        {
            var engine = new CleanEngine();
            var rules = engine.GetRules();
            Assert.That(rules.Count, Is.GreaterThan(0));
        }

        [Test]
        public void RegisterRule_AddsCustomRule()
        {
            var engine = new CleanEngine();
            int initial = engine.GetRules().Count;
            engine.RegisterRule(new TestCleanRule(_tempDir));
            Assert.That(engine.GetRules().Count, Is.EqualTo(initial + 1));
        }

        [Test]
        public async Task Scan_CustomRule_FindsFiles()
        {
            // Create some files in our temp dir
            WriteFile("junk1.tmp", "trash");
            WriteFile("junk2.tmp", "trash2");
            WriteFile("keep.important", "keep this");

            var engine = new CleanEngine();
            // Scan only our custom rule (ignores built-in rules that require system dirs)
            var rule = new TestCleanRule(_tempDir);
            var result = await engine.ScanAsync(new[] { rule });

            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.TotalSize, Is.GreaterThan(0));
        }

        [Test]
        public async Task Clean_DeletesSelectedFiles()
        {
            WriteFile("delete_me.tmp", "delete");
            WriteFile("keep_me.tmp", "keep");

            var engine = new CleanEngine();
            var rule = new TestCleanRule(_tempDir);
            var scanResult = await engine.ScanAsync(new[] { rule });

            // Deselect keep_me.tmp
            foreach (var item in scanResult.Items)
                item.IsSelected = item.FileName == "delete_me.tmp";

            var task = await engine.CleanAsync(scanResult);

            Assert.That(task.FilesDeleted, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(_tempDir, "delete_me.tmp")), Is.False);
            Assert.That(File.Exists(Path.Combine(_tempDir, "keep_me.tmp")), Is.True);
        }

        [Test]
        public async Task Scan_EmptyDirectory_ReturnsEmptyScanResult()
        {
            var engine = new CleanEngine();
            var rule = new TestCleanRule(_tempDir);
            var result = await engine.ScanAsync(new[] { rule });
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.TotalSize, Is.EqualTo(0));
        }

        private void WriteFile(string name, string content)
        {
            File.WriteAllText(Path.Combine(_tempDir, name), content, Encoding.UTF8);
        }

        /// <summary>A minimal test clean rule that targets only .tmp files in the specified directory.</summary>
        private sealed class TestCleanRule : ICleanRule
        {
            private readonly string _dir;
            public TestCleanRule(string dir) => _dir = dir;
            public string Name => "Test Rule";
            public string Description => "Targets .tmp files in test directory";
            public System.Collections.Generic.IEnumerable<string> GetTargetPaths() => new[] { _dir };
            public bool IsSafeToDelete(FileInfo file) =>
                file.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase);
        }
    }
}
