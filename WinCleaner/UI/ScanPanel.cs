using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinCleaner.Core;
using WinCleaner.Models;

namespace WinCleaner.UI
{
    public class ScanPanel : UserControl
    {
        public event EventHandler<string>? StatusChanged;

        private readonly CleanEngine _engine = new();
        private ScanResult? _lastScanResult;
        private CancellationTokenSource? _cts;

        private CheckedListBox _rulesListBox = null!;
        private Button _scanBtn = null!;
        private Button _cleanBtn = null!;
        private Button _cancelBtn = null!;
        private ProgressBar _progressBar = null!;
        private ListView _fileListView = null!;
        private Label _summaryLabel = null!;

        public ScanPanel()
        {
            InitializeComponent();
            LoadRules();
        }

        private void InitializeComponent()
        {
            var titleLabel = new Label
            {
                Text = "🗑️ 垃圾清理",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Padding = new Padding(10, 8, 0, 0)
            };

            // Left options panel
            var leftPanel = new Panel { Dock = DockStyle.Left, Width = 240, Padding = new Padding(10) };

            var rulesLabel = new Label { Text = "选择清理类别:", Dock = DockStyle.Top, Height = 24 };
            _rulesListBox = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true
            };
            leftPanel.Controls.Add(_rulesListBox);
            leftPanel.Controls.Add(rulesLabel);

            // Button strip
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(6, 4, 0, 0),
                FlowDirection = FlowDirection.LeftToRight
            };
            _scanBtn = CreateButton("🔍 扫描", Color.FromArgb(0, 120, 212));
            _cleanBtn = CreateButton("🗑️ 清理", Color.FromArgb(196, 43, 28));
            _cancelBtn = CreateButton("✖ 取消", Color.Gray);
            _cleanBtn.Enabled = false;
            _cancelBtn.Enabled = false;
            _scanBtn.Click += async (_, _) => await StartScanAsync();
            _cleanBtn.Click += async (_, _) => await StartCleanAsync();
            _cancelBtn.Click += (_, _) => _cts?.Cancel();
            btnPanel.Controls.AddRange(new Control[] { _scanBtn, _cleanBtn, _cancelBtn });

            _progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 6, Style = ProgressBarStyle.Marquee };
            _progressBar.Visible = false;

            _summaryLabel = new Label
            {
                Text = "请选择清理类别后点击\"扫描\"",
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(6, 4, 0, 0)
            };

            // File list
            _fileListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                GridLines = true
            };
            _fileListView.Columns.Add("文件名", 300);
            _fileListView.Columns.Add("大小", 80);
            _fileListView.Columns.Add("修改时间", 140);
            _fileListView.Columns.Add("路径", 400);
            _fileListView.ItemChecked += (_, _) => UpdateSummary();

            var rightPanel = new Panel { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(_fileListView);
            rightPanel.Controls.Add(_summaryLabel);
            rightPanel.Controls.Add(_progressBar);
            rightPanel.Controls.Add(btnPanel);

            Controls.Add(rightPanel);
            Controls.Add(leftPanel);
            Controls.Add(titleLabel);
        }

        private void LoadRules()
        {
            foreach (var rule in _engine.GetRules())
            {
                _rulesListBox.Items.Add(rule, isChecked: true);
            }
        }

        private async Task StartScanAsync()
        {
            _cts = new CancellationTokenSource();
            SetBusy(true);
            _fileListView.Items.Clear();
            _summaryLabel.Text = "扫描中...";

            var selectedRules = _rulesListBox.CheckedItems.Cast<ICleanRule>().ToList();
            var progress = new Progress<(int files, long bytes)>(p =>
            {
                StatusChanged?.Invoke(this, $"已扫描 {p.files} 个文件，发现 {FormatSize(p.bytes)}");
            });

            try
            {
                _lastScanResult = await _engine.ScanAsync(selectedRules, progress, _cts.Token);
                PopulateListView(_lastScanResult);
                _cleanBtn.Enabled = _lastScanResult.Items.Count > 0;
                StatusChanged?.Invoke(this, $"扫描完成：{_lastScanResult.TotalCount} 个文件，共 {_lastScanResult.TotalSizeFormatted}");
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke(this, "扫描已取消");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task StartCleanAsync()
        {
            if (_lastScanResult == null) return;

            // Sync selection from ListView back to FileItems
            for (int i = 0; i < _fileListView.Items.Count; i++)
                _lastScanResult.Items[i].IsSelected = _fileListView.Items[i].Checked;

            var selected = _lastScanResult.Items.Where(x => x.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("请至少选择一个文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"确定要删除 {selected.Count} 个文件（共 {FormatSize(selected.Sum(f => f.Size))}）吗？此操作不可撤销！",
                "确认清理",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            _cts = new CancellationTokenSource();
            SetBusy(true);

            var cleanProgress = new Progress<(int deleted, long freed)>(p =>
            {
                StatusChanged?.Invoke(this, $"已清理 {p.deleted} 个文件，释放 {FormatSize(p.freed)}");
            });

            try
            {
                var task = await _engine.CleanAsync(_lastScanResult, cleanProgress, _cts.Token);
                MessageBox.Show(
                    $"清理完成！\n删除文件：{task.FilesDeleted} 个\n释放空间：{FormatSize(task.BytesFreed)}",
                    "清理结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _fileListView.Items.Clear();
                _lastScanResult = null;
                _cleanBtn.Enabled = false;
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke(this, "清理已取消");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void PopulateListView(ScanResult result)
        {
            _fileListView.BeginUpdate();
            _fileListView.Items.Clear();
            foreach (var item in result.Items)
            {
                var lvi = new ListViewItem(item.FileName) { Checked = item.IsSelected, Tag = item };
                lvi.SubItems.Add(item.SizeFormatted);
                lvi.SubItems.Add(item.LastModified.ToString("yyyy-MM-dd HH:mm"));
                lvi.SubItems.Add(item.FullPath);
                _fileListView.Items.Add(lvi);
            }
            _fileListView.EndUpdate();
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (_lastScanResult == null) return;
            long total = 0;
            int count = 0;
            foreach (ListViewItem lvi in _fileListView.Items)
            {
                if (lvi.Checked && lvi.Tag is FileItem fi)
                {
                    total += fi.Size;
                    count++;
                }
            }
            _summaryLabel.Text = $"已选择 {count} 个文件，共 {FormatSize(total)}（扫描总计 {_lastScanResult.TotalCount} 个，{_lastScanResult.TotalSizeFormatted}）";
        }

        private void SetBusy(bool busy)
        {
            _scanBtn.Enabled = !busy;
            _cleanBtn.Enabled = !busy && _lastScanResult?.Items.Count > 0;
            _cancelBtn.Enabled = busy;
            _progressBar.Visible = busy;
        }

        private static Button CreateButton(string text, Color backColor)
        {
            return new Button
            {
                Text = text,
                Width = 90,
                Height = 34,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f)
            };
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
