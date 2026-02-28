using System;
using System.Drawing;
using System.Windows.Forms;
using WinCleaner.Core;
using WinCleaner.Config;

namespace WinCleaner.UI
{
    public class MainForm : Form
    {
        private Panel _navPanel = null!;
        private Panel _contentPanel = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _statusLabel = null!;

        private ScanPanel _scanPanel = null!;
        private FileOrganizerPanel _fileOrganizerPanel = null!;
        private DiskAnalysisPanel _diskAnalysisPanel = null!;
        private SettingsPanel _settingsPanel = null!;

        private readonly SchedulerService _scheduler = new();

        public MainForm()
        {
            InitializeComponent();
            InitializeScheduler();
            ShowPanel(_scanPanel);
        }

        private void InitializeComponent()
        {
            Text = "🧹 WinCleaner";
            Size = new Size(980, 680);
            MinimumSize = new Size(800, 560);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            // Nav panel (left sidebar)
            _navPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 180,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            // Content panel (right area)
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // Status bar
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Ready");
            _statusStrip.Items.Add(_statusLabel);

            // Create content panels
            _scanPanel = new ScanPanel { Dock = DockStyle.Fill };
            _fileOrganizerPanel = new FileOrganizerPanel { Dock = DockStyle.Fill };
            _diskAnalysisPanel = new DiskAnalysisPanel { Dock = DockStyle.Fill };
            _settingsPanel = new SettingsPanel { Dock = DockStyle.Fill };

            _scanPanel.StatusChanged += (_, msg) => SetStatus(msg);
            _fileOrganizerPanel.StatusChanged += (_, msg) => SetStatus(msg);
            _diskAnalysisPanel.StatusChanged += (_, msg) => SetStatus(msg);

            // Build nav buttons
            AddNavButton("🗑️  垃圾清理", () => ShowPanel(_scanPanel));
            AddNavButton("📁  文件整合", () => ShowPanel(_fileOrganizerPanel));
            AddNavButton("💾  磁盘分析", () => ShowPanel(_diskAnalysisPanel));
            AddNavButton("⚙️  设置", () => ShowPanel(_settingsPanel));

            Controls.Add(_contentPanel);
            Controls.Add(_navPanel);
            Controls.Add(_statusStrip);
        }

        private void AddNavButton(string text, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 50,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0),
                Font = new Font("Segoe UI", 10f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, _) => onClick();
            // Insert at top so first-added appears at top after dock layout
            _navPanel.Controls.Add(btn);

            // Title label at very top
            if (_navPanel.Controls.Count == 1)
            {
                var title = new Label
                {
                    Text = "WinCleaner",
                    Dock = DockStyle.Top,
                    Height = 60,
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(30, 30, 30),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 14f, FontStyle.Bold)
                };
                _navPanel.Controls.Add(title);
            }
        }

        private void ShowPanel(Control panel)
        {
            _contentPanel.Controls.Clear();
            _contentPanel.Controls.Add(panel);
            panel.BringToFront();
        }

        private void SetStatus(string message)
        {
            if (InvokeRequired)
                Invoke(() => _statusLabel.Text = message);
            else
                _statusLabel.Text = message;
        }

        private void InitializeScheduler()
        {
            var settings = ConfigManager.Settings;
            if (settings.ScheduledCleanEnabled)
            {
                _scheduler.CleanTriggered += async (_, _) =>
                {
                    // Scheduled clean runs silently (without preview) as a background maintenance task.
                    // If PreviewBeforeClean is enabled, notify the user instead of running automatically.
                    if (settings.PreviewBeforeClean)
                    {
                        SetStatus($"定时清理已触发（{DateTime.Now:HH:mm}）— 请手动执行清理以预览文件");
                        return;
                    }
                    var engine = new CleanEngine();
                    var result = await engine.ScanAsync();
                    await engine.CleanAsync(result);
                    SetStatus($"定时清理完成（{DateTime.Now:HH:mm}）");
                };
                _scheduler.Start(TimeSpan.FromDays(settings.ScheduledCleanIntervalDays));
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _scheduler.Dispose();
            base.OnFormClosed(e);
        }
    }
}
