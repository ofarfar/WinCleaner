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
    public class FileConsolidatorTests
    {
        private string _sourceDir = null!;
        private string _targetDir = null!;

        [SetUp]
        public void SetUp()
        {
            var root = Path.Combine(Path.GetTempPath(), "WinCleanerConsolidatorTests_" + Guid.NewGuid().ToString("N"));
            _sourceDir = Path.Combine(root, "Source");
            _targetDir = Path.Combine(root, "Target");
            Directory.CreateDirectory(_sourceDir);
            Directory.CreateDirectory(_targetDir);
        }

        [TearDown]
        public void TearDown()
        {
            var root = Path.GetDirectoryName(_sourceDir)!;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        [Test]
        public async Task Analyze_ByExtension_CreatesMoves()
        {
            WriteFile("photo.jpg", "jpeg data");
            WriteFile("document.pdf", "pdf data");
            WriteFile("video.mp4", "mp4 data");

            var options = new ConsolidationOptions
            {
                TargetDirectory = _targetDir,
                GroupByExtension = true
            };

            var consolidator = new FileConsolidator();
            var plan = await consolidator.AnalyzeAsync(_sourceDir, options);

            Assert.That(plan.Moves.Count, Is.EqualTo(3));
            Assert.That(plan.Moves.Any(m => m.DestinationPath.Contains("Images")), Is.True);
            Assert.That(plan.Moves.Any(m => m.DestinationPath.Contains("Documents")), Is.True);
            Assert.That(plan.Moves.Any(m => m.DestinationPath.Contains("Videos")), Is.True);
        }

        [Test]
        public async Task Analyze_ByDate_CreatesMoves()
        {
            WriteFile("file1.txt", "content");

            var options = new ConsolidationOptions
            {
                TargetDirectory = _targetDir,
                GroupByDate = true,
                GroupByExtension = false
            };

            var consolidator = new FileConsolidator();
            var plan = await consolidator.AnalyzeAsync(_sourceDir, options);

            Assert.That(plan.Moves.Count, Is.EqualTo(1));
            // Destination should contain year/month subfolder
            var move = plan.Moves[0];
            Assert.That(move.DestinationPath, Does.StartWith(_targetDir));
        }

        [Test]
        public async Task Execute_CopyMode_LeavesSourceFiles()
        {
            WriteFile("copy.jpg", "copy content");

            var options = new ConsolidationOptions
            {
                TargetDirectory = _targetDir,
                GroupByExtension = true
            };

            var consolidator = new FileConsolidator();
            var plan = await consolidator.AnalyzeAsync(_sourceDir, options);
            await consolidator.ExecuteAsync(plan, copyMode: true);

            Assert.That(File.Exists(Path.Combine(_sourceDir, "copy.jpg")), Is.True, "Source should still exist in copy mode");
            Assert.That(plan.Moves.Any(m => File.Exists(m.DestinationPath)), Is.True, "Destination file should exist");
        }

        [Test]
        public async Task Execute_MoveMode_RemovesSourceFiles()
        {
            WriteFile("move.pdf", "move content");

            var options = new ConsolidationOptions
            {
                TargetDirectory = _targetDir,
                GroupByExtension = true
            };

            var consolidator = new FileConsolidator();
            var plan = await consolidator.AnalyzeAsync(_sourceDir, options);
            await consolidator.ExecuteAsync(plan, copyMode: false);

            Assert.That(File.Exists(Path.Combine(_sourceDir, "move.pdf")), Is.False, "Source should be moved");
        }

        [Test]
        public async Task UndoLastConsolidation_RestoresFiles()
        {
            WriteFile("undo.txt", "undo me");

            var options = new ConsolidationOptions
            {
                TargetDirectory = _targetDir,
                GroupByExtension = true
            };

            var consolidator = new FileConsolidator();
            var plan = await consolidator.AnalyzeAsync(_sourceDir, options);
            await consolidator.ExecuteAsync(plan, copyMode: false);

            Assert.That(File.Exists(Path.Combine(_sourceDir, "undo.txt")), Is.False);

            await consolidator.UndoLastConsolidationAsync();

            Assert.That(File.Exists(Path.Combine(_sourceDir, "undo.txt")), Is.True, "File should be restored after undo");
        }

        private void WriteFile(string name, string content)
        {
            File.WriteAllText(Path.Combine(_sourceDir, name), content, Encoding.UTF8);
        }
    }
}
