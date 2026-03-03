# 🧹 WinCleaner

**Windows 垃圾清理与文件整合工具** — A safe, efficient, and visual junk-cleaning and file-organisation tool for Windows.

| | |
|---|---|
| **Platform** | Windows 7 SP1 and above (Win7 / Win8 / Win10 / Win11) |
| **Runtime** | .NET 8 (Windows Desktop) |
| **UI** | WinForms |
| **Language** | C# |

---

## Features

| Module | Description |
|---|---|
| 🗑️ Junk Scan & Clean | Scan temp files, system cache, log files, browser cache, crash dumps; one-click or selective clean |
| 📁 File Consolidation | Auto-organise files by type / date / size; preview before move; undo support |
| 🔁 Duplicate Detection | 3-phase algorithm: group by size → partial MD5 → full MD5 |
| 💾 Disk Analysis | Recursive directory size tree with pie chart visualisation |
| 🛡️ Backup & Restore | ZIP restore-points created before each clean; one-click restore |
| ⏰ Scheduler | Timer-based auto-clean with configurable interval |
| 📋 Audit Log | Full NLog-powered log to `%LOCALAPPDATA%\WinCleaner\Logs` |
| ⚙️ Settings | Persist all preferences to `Config/AppConfig.json` |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (includes .NET 8 Windows Desktop Runtime)
- Windows OS **or** a Linux/macOS machine for building/testing the core library

### Clone

```bash
git clone https://github.com/ofarfar/WinCleaner.git
cd WinCleaner
```

### Build

```bash
# Full solution (all three projects)
dotnet build WinCleaner.slnx
```

### Run (Windows only)

```bash
dotnet run --project WinCleaner/WinCleaner.csproj
```

### Test

```bash
# Tests target net8.0 (cross-platform — runs on Linux CI too)
dotnet test WinCleaner.Tests/WinCleaner.Tests.csproj
```

---

## Project Layout

```
WinCleaner/
├── WinCleaner.Core/          # Cross-platform business logic (.NET 8)
│   ├── Core/
│   │   ├── CleanEngine.cs        # Junk scan + clean, pluggable ICleanRule
│   │   ├── FileConsolidator.cs   # File organisation with undo
│   │   ├── DuplicateFinder.cs    # 3-phase duplicate detection
│   │   ├── DiskAnalyzer.cs       # Recursive disk tree + drive info
│   │   ├── BackupManager.cs      # ZIP restore-points
│   │   └── SchedulerService.cs   # Scheduled auto-clean
│   ├── Models/                   # FileItem, ScanResult, CleanTask, DiskModels
│   ├── Utils/                    # HashHelper, AppLogger, Win32Helper, RegistryHelper
│   └── Config/                   # AppConfig.json + ConfigManager
│
├── WinCleaner/               # WinForms UI shell (net8.0-windows)
│   ├── UI/
│   │   ├── MainForm.cs
│   │   ├── ScanPanel.cs
│   │   ├── FileOrganizerPanel.cs
│   │   ├── DiskAnalysisPanel.cs
│   │   └── SettingsPanel.cs
│   └── Program.cs
│
├── WinCleaner.Tests/         # NUnit 3 tests (net8.0, cross-platform)
│   ├── CleanEngineTests.cs
│   ├── DuplicateFinderTests.cs
│   ├── FileConsolidatorTests.cs
│   ├── DiskAnalyzerTests.cs
│   ├── BackupManagerTests.cs
│   ├── HashHelperTests.cs
│   └── ModelTests.cs
│
└── WinCleaner.slnx           # Solution file
```

---

## Extending with a Custom Clean Rule

```csharp
using WinCleaner.Core;

public class OldDownloadsRule : ICleanRule
{
    public string Name => "Old Downloads";
    public string Description => "Downloads not accessed for 90+ days";

    public IEnumerable<string> GetTargetPaths()
        => new[] { "%USERPROFILE%\\Downloads" };

    public bool IsSafeToDelete(FileInfo file)
        => (DateTime.Now - file.LastAccessTime).TotalDays > 90;
}

// Register and use
var engine = new CleanEngine();
engine.RegisterRule(new OldDownloadsRule());
var result = await engine.ScanAsync(new[] { new OldDownloadsRule() });
await engine.CleanAsync(result);
```

---

## Configuration

Edit `WinCleaner/Config/AppConfig.json` (copied to the output directory on build):

```json
{
  "Language": "zh-CN",
  "BackupBeforeClean": true,
  "PreviewBeforeClean": true,
  "ScheduledCleanEnabled": false,
  "ScheduledCleanIntervalDays": 7,
  "MaxBackupAgeDays": 30,
  "LargeFileSizeThresholdMB": 100,
  "CleanRules": {
    "CleanTempFiles": true,
    "CleanRecycleBin": true,
    "CleanBrowserCache": true,
    "CleanCrashDumps": true
  }
}
```

---

## CI

GitHub Actions runs on every push and pull-request to `main`:

- Build all three projects
- Run the full test suite (40 tests)

See [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

---

## License

[LICENSE](LICENSE)
