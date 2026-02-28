using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinCleaner.Core;
using WinCleaner.Models;

namespace WinCleaner.UI
{
    public class DiskAnalysisPanel : UserControl
    {
        public event EventHandler<string>? StatusChanged;

        private readonly DiskAnalyzer _analyzer = new();
        private CancellationTokenSource? _cts;

        private ComboBox _driveCombo = null!;
        private Button _analyzeBtn = null!;
        private Button _cancelBtn = null!;
        private ProgressBar _progressBar = null!;
        private TreeView _diskTree = null!;
        private Panel _chartPanel = null!;
        private Label _summaryLabel = null!;

        private List<DiskDriveInfo> _drives = new();
        private DiskTreeNode? _rootNode;

        public DiskAnalysisPanel()
        {
            InitializeComponent();
            LoadDrives();
        }

        private void InitializeComponent()
        {
            var title = new Label
            {
                Text = "💾 磁盘分析",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Padding = new Padding(10, 8, 0, 0)
            };

            // Top bar
            var topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(10, 6, 0, 0)
            };
            _driveCombo = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _analyzeBtn = new Button
            {
                Text = "🔍 分析",
                Width = 80,
                Height = 32,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _cancelBtn = new Button
            {
                Text = "✖ 取消",
                Width = 70,
                Height = 32,
                BackColor = Color.Gray,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _analyzeBtn.Click += async (_, _) => await AnalyzeDriveAsync();
            _cancelBtn.Click += (_, _) => _cts?.Cancel();
            topBar.Controls.AddRange(new Control[] { new Label { Text = "选择磁盘:", AutoSize = true, Padding = new Padding(0, 6, 4, 0) }, _driveCombo, _analyzeBtn, _cancelBtn });

            _progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 6, Visible = false, Style = ProgressBarStyle.Marquee };
            _summaryLabel = new Label { Dock = DockStyle.Top, Height = 26, Padding = new Padding(10, 4, 0, 0) };

            // Split: tree on left, chart on right
            var splitter = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 400 };

            _diskTree = new TreeView { Dock = DockStyle.Fill };
            _diskTree.AfterSelect += OnTreeNodeSelected;

            _chartPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.WhiteSmoke };
            _chartPanel.Paint += OnChartPanelPaint;

            splitter.Panel1.Controls.Add(_diskTree);
            splitter.Panel2.Controls.Add(_chartPanel);

            Controls.Add(splitter);
            Controls.Add(_summaryLabel);
            Controls.Add(_progressBar);
            Controls.Add(topBar);
            Controls.Add(title);
        }

        private void LoadDrives()
        {
            _drives = _analyzer.GetDriveInfos();
            foreach (var d in _drives)
                _driveCombo.Items.Add($"{d.DriveLetter} [{d.Label}]");
            if (_driveCombo.Items.Count > 0)
                _driveCombo.SelectedIndex = 0;
        }

        private async Task AnalyzeDriveAsync()
        {
            if (_driveCombo.SelectedIndex < 0) return;
            var drive = _drives[_driveCombo.SelectedIndex];

            _cts = new CancellationTokenSource();
            _analyzeBtn.Enabled = false;
            _cancelBtn.Enabled = true;
            _progressBar.Visible = true;
            _diskTree.Nodes.Clear();

            var prog = new Progress<string>(p => StatusChanged?.Invoke(this, $"分析: {p}"));

            try
            {
                _rootNode = await _analyzer.AnalyzeAsync(drive.DriveLetter, prog, _cts.Token);
                PopulateTree(_rootNode);
                _summaryLabel.Text = $"驱动器 {drive.DriveLetter}  总计: {drive.TotalSize / (1024.0 * 1024 * 1024):F1} GB  " +
                                     $"已用: {drive.UsedSpace / (1024.0 * 1024 * 1024):F1} GB  " +
                                     $"空闲: {drive.FreeSpace / (1024.0 * 1024 * 1024):F1} GB  ({drive.UsedPercent:F1}% 已用)";
                StatusChanged?.Invoke(this, "磁盘分析完成");
                _chartPanel.Invalidate();
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke(this, "分析已取消");
            }
            finally
            {
                _analyzeBtn.Enabled = true;
                _cancelBtn.Enabled = false;
                _progressBar.Visible = false;
            }
        }

        private void PopulateTree(DiskTreeNode node)
        {
            _diskTree.BeginUpdate();
            _diskTree.Nodes.Clear();
            var root = BuildTreeNode(node);
            _diskTree.Nodes.Add(root);
            root.Expand();
            _diskTree.EndUpdate();
        }

        private static TreeNode BuildTreeNode(DiskTreeNode node)
        {
            var tn = new TreeNode($"{node.Name}  [{node.SizeFormatted}]") { Tag = node };
            // Only add top-level children to avoid huge trees
            foreach (var child in node.Children)
            {
                var childNode = new TreeNode($"{child.Name}  [{child.SizeFormatted}]") { Tag = child };
                tn.Nodes.Add(childNode);
            }
            return tn;
        }

        private void OnTreeNodeSelected(object? sender, TreeViewEventArgs e)
        {
            _chartPanel.Invalidate();
        }

        private void OnChartPanelPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = _chartPanel.ClientRectangle;
            g.Clear(Color.WhiteSmoke);

            var selectedNode = _diskTree.SelectedNode?.Tag as DiskTreeNode
                               ?? _rootNode;
            if (selectedNode == null || selectedNode.Children.Count == 0) return;

            // Simple pie chart
            var pieRect = new Rectangle(rect.X + 20, rect.Y + 40, Math.Min(rect.Width - 40, rect.Height - 80), Math.Min(rect.Width - 40, rect.Height - 80));
            if (pieRect.Width <= 0 || pieRect.Height <= 0) return;

            long total = selectedNode.Size;
            if (total == 0) return;

            var colors = new[] { Color.SteelBlue, Color.Coral, Color.SeaGreen, Color.Goldenrod, Color.MediumPurple, Color.Tomato, Color.CadetBlue };
            float startAngle = 0;
            int ci = 0;
            foreach (var child in selectedNode.Children)
            {
                float sweep = (float)(child.Size * 360.0 / total);
                using var brush = new SolidBrush(colors[ci % colors.Length]);
                g.FillPie(brush, pieRect, startAngle, sweep);
                startAngle += sweep;
                ci++;
            }

            // Legend
            int ly = pieRect.Bottom + 10;
            ci = 0;
            foreach (var child in selectedNode.Children)
            {
                if (ly > rect.Bottom - 20) break;
                using var brush = new SolidBrush(colors[ci % colors.Length]);
                g.FillRectangle(brush, rect.X + 20, ly, 14, 14);
                g.DrawString($"{child.Name}  {child.SizeFormatted}", Font, Brushes.Black, rect.X + 40, ly);
                ly += 18;
                ci++;
            }
        }
    }
}
