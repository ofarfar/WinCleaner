using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinCleaner.Core;
using WinCleaner.Models;

namespace WinCleaner.UI
{
    public class FileOrganizerPanel : UserControl
    {
        public event EventHandler<string>? StatusChanged;

        private readonly FileConsolidator _consolidator = new();
        private ConsolidationPlan? _currentPlan;
        private CancellationTokenSource? _cts;

        private TextBox _sourceDirBox = null!;
        private TextBox _targetDirBox = null!;
        private RadioButton _rbByType = null!;
        private RadioButton _rbByDate = null!;
        private RadioButton _rbBySize = null!;
        private Button _analyzeBtn = null!;
        private Button _executeBtn = null!;
        private Button _undoBtn = null!;
        private Button _cancelBtn = null!;
        private TreeView _planTree = null!;
        private Label _summaryLabel = null!;
        private ProgressBar _progressBar = null!;

        public FileOrganizerPanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            var title = new Label
            {
                Text = "📁 文件整合",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Padding = new Padding(10, 8, 0, 0)
            };

            // Options panel (top)
            var optionsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 130,
                ColumnCount = 2,
                Padding = new Padding(10, 4, 10, 4)
            };
            optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 85));

            _sourceDirBox = new TextBox { Dock = DockStyle.Fill };
            _targetDirBox = new TextBox { Dock = DockStyle.Fill };

            var browseSrc = new Button { Text = "浏览", Width = 60 };
            browseSrc.Click += (_, _) => BrowseDir(_sourceDirBox);
            var browseDst = new Button { Text = "浏览", Width = 60 };
            browseDst.Click += (_, _) => BrowseDir(_targetDirBox);

            var srcRow = new Panel { Dock = DockStyle.Fill };
            srcRow.Controls.Add(browseSrc);
            srcRow.Controls.Add(_sourceDirBox);
            browseSrc.Dock = DockStyle.Right;
            _sourceDirBox.Dock = DockStyle.Fill;

            var dstRow = new Panel { Dock = DockStyle.Fill };
            dstRow.Controls.Add(browseDst);
            dstRow.Controls.Add(_targetDirBox);
            browseDst.Dock = DockStyle.Right;
            _targetDirBox.Dock = DockStyle.Fill;

            optionsPanel.Controls.Add(new Label { Text = "源目录:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            optionsPanel.Controls.Add(srcRow, 1, 0);
            optionsPanel.Controls.Add(new Label { Text = "目标目录:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            optionsPanel.Controls.Add(dstRow, 1, 1);

            _rbByType = new RadioButton { Text = "按文件类型", Checked = true, AutoSize = true };
            _rbByDate = new RadioButton { Text = "按日期", AutoSize = true };
            _rbBySize = new RadioButton { Text = "按大小", AutoSize = true };
            var strategyPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            strategyPanel.Controls.AddRange(new Control[] { new Label { Text = "整合策略:", AutoSize = true, Padding = new Padding(0, 4, 8, 0) }, _rbByType, _rbByDate, _rbBySize });
            optionsPanel.Controls.Add(strategyPanel, 1, 2);
            optionsPanel.Controls.Add(new Label(), 0, 2);

            // Buttons
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 4, 0, 0) };
            _analyzeBtn = MakeButton("🔍 分析预览", Color.FromArgb(0, 120, 212));
            _executeBtn = MakeButton("▶ 执行整合", Color.FromArgb(16, 124, 16));
            _undoBtn = MakeButton("↩ 撤销", Color.FromArgb(100, 100, 100));
            _cancelBtn = MakeButton("✖ 取消", Color.Gray);
            _executeBtn.Enabled = false;
            _cancelBtn.Enabled = false;

            _analyzeBtn.Click += async (_, _) => await AnalyzeAsync();
            _executeBtn.Click += async (_, _) => await ExecuteAsync();
            _undoBtn.Click += async (_, _) => await UndoAsync();
            _cancelBtn.Click += (_, _) => _cts?.Cancel();
            btnPanel.Controls.AddRange(new Control[] { _analyzeBtn, _executeBtn, _undoBtn, _cancelBtn });

            _progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 6, Visible = false, Style = ProgressBarStyle.Marquee };
            _summaryLabel = new Label { Dock = DockStyle.Top, Height = 26, Padding = new Padding(10, 4, 0, 0) };

            // Plan tree
            _planTree = new TreeView { Dock = DockStyle.Fill };

            Controls.Add(_planTree);
            Controls.Add(_summaryLabel);
            Controls.Add(_progressBar);
            Controls.Add(btnPanel);
            Controls.Add(optionsPanel);
            Controls.Add(title);
        }

        private async Task AnalyzeAsync()
        {
            if (!ValidatePaths()) return;
            _cts = new CancellationTokenSource();
            SetBusy(true);
            _planTree.Nodes.Clear();

            var options = new ConsolidationOptions
            {
                TargetDirectory = _targetDirBox.Text,
                GroupByExtension = _rbByType.Checked,
                GroupByDate = _rbByDate.Checked,
                GroupBySize = _rbBySize.Checked
            };

            try
            {
                _currentPlan = await _consolidator.AnalyzeAsync(_sourceDirBox.Text, options, _cts.Token);
                PopulateTree(_currentPlan);
                _executeBtn.Enabled = _currentPlan.Moves.Count > 0;
                _summaryLabel.Text = $"分析完成：共 {_currentPlan.Moves.Count} 个文件待整合";
                StatusChanged?.Invoke(this, _summaryLabel.Text);
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke(this, "分析已取消");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ExecuteAsync()
        {
            if (_currentPlan == null) return;
            _cts = new CancellationTokenSource();
            SetBusy(true);

            var prog = new Progress<(int processed, long bytes)>(p =>
                StatusChanged?.Invoke(this, $"已处理 {p.processed} 个文件"));

            try
            {
                await _consolidator.ExecuteAsync(_currentPlan, copyMode: false, prog, _cts.Token);
                MessageBox.Show("文件整合完成！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _planTree.Nodes.Clear();
                _currentPlan = null;
                _executeBtn.Enabled = false;
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke(this, "整合已取消");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task UndoAsync()
        {
            await _consolidator.UndoLastConsolidationAsync();
            MessageBox.Show("撤销完成。", "撤销", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PopulateTree(ConsolidationPlan plan)
        {
            _planTree.BeginUpdate();
            _planTree.Nodes.Clear();
            foreach (var move in plan.Moves)
            {
                var node = new TreeNode(Path.GetFileName(move.SourcePath))
                {
                    ToolTipText = $"{move.SourcePath}\n→ {move.DestinationPath}"
                };
                _planTree.Nodes.Add(node);
            }
            _planTree.EndUpdate();
        }

        private bool ValidatePaths()
        {
            if (string.IsNullOrWhiteSpace(_sourceDirBox.Text) || !Directory.Exists(_sourceDirBox.Text))
            {
                MessageBox.Show("请选择有效的源目录。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(_targetDirBox.Text))
            {
                MessageBox.Show("请选择目标目录。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private void BrowseDir(TextBox target)
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
                target.Text = dlg.SelectedPath;
        }

        private void SetBusy(bool busy)
        {
            _analyzeBtn.Enabled = !busy;
            _executeBtn.Enabled = !busy && (_currentPlan?.Moves.Count ?? 0) > 0;
            _cancelBtn.Enabled = busy;
            _progressBar.Visible = busy;
        }

        private static Button MakeButton(string text, Color back) =>
            new() { Text = text, Width = 100, Height = 34, BackColor = back, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    }
}
