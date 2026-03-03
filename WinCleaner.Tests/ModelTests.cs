using System;
using WinCleaner.Models;

namespace WinCleaner.Tests
{
    [TestFixture]
    public class ModelTests
    {
        [Test]
        public void FileItem_SizeFormatted_Bytes()
        {
            var item = new FileItem { Size = 512 };
            Assert.That(item.SizeFormatted, Does.Contain("B"));
        }

        [Test]
        public void FileItem_SizeFormatted_Kilobytes()
        {
            var item = new FileItem { Size = 2048 };
            Assert.That(item.SizeFormatted, Does.Contain("KB"));
        }

        [Test]
        public void FileItem_SizeFormatted_Megabytes()
        {
            var item = new FileItem { Size = 5 * 1024 * 1024 };
            Assert.That(item.SizeFormatted, Does.Contain("MB"));
        }

        [Test]
        public void FileItem_SizeFormatted_Gigabytes()
        {
            var item = new FileItem { Size = 2L * 1024 * 1024 * 1024 };
            Assert.That(item.SizeFormatted, Does.Contain("GB"));
        }

        [Test]
        public void FileItem_FileName_ReturnsBaseName()
        {
            var path = Path.Combine("Users", "Test", "file.txt");
            var item = new FileItem { FullPath = path };
            Assert.That(item.FileName, Is.EqualTo("file.txt"));
        }

        [Test]
        public void ScanResult_TotalCount_MatchesItemsCount()
        {
            var result = new ScanResult();
            result.Items.Add(new FileItem { Size = 100 });
            result.Items.Add(new FileItem { Size = 200 });
            Assert.That(result.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public void ScanResult_TotalSizeFormatted_NonEmpty()
        {
            var result = new ScanResult { TotalSize = 1024 * 1024 };
            Assert.That(result.TotalSizeFormatted, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void CleanTask_DefaultStatus_IsPending()
        {
            var task = new CleanTask();
            Assert.That(task.Status, Is.EqualTo(CleanTaskStatus.Pending));
        }

        [Test]
        public void DiskTreeNode_SizeFormatted_CorrectUnit()
        {
            var node = new DiskTreeNode { Size = 1024 * 1024 * 50 }; // 50 MB
            Assert.That(node.SizeFormatted, Does.Contain("MB"));
        }

        [Test]
        public void DiskDriveInfo_UsedSpace_CalculatedCorrectly()
        {
            var info = new DiskDriveInfo { TotalSize = 1000, FreeSpace = 400 };
            Assert.That(info.UsedSpace, Is.EqualTo(600));
        }

        [Test]
        public void DiskDriveInfo_UsedPercent_CalculatedCorrectly()
        {
            var info = new DiskDriveInfo { TotalSize = 1000, FreeSpace = 500 };
            Assert.That(info.UsedPercent, Is.EqualTo(50.0).Within(0.01));
        }
    }
}
