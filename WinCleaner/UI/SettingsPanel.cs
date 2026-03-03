using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WinCleaner.Config;
using WinCleaner.Utils;

namespace WinCleaner.UI
{
    public class SettingsPanel : UserControl
    {
        private CheckBox _cbBackupBeforeClean = null!;
        private CheckBox _cbPreviewBeforeClean = null!;
        private CheckBox _cbScheduledClean = null!;
        private NumericUpDown _scheduleDays = null!;
        private NumericUpDown _maxBackupDays = null!;
        private NumericUpDown _largeFileMb = null!;
        private ComboBox _languageCombo = null!;
        private Button _saveBtn = null!;
        private Label _statusLabel = null!;

        public SettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            var title = new Label
            {
                Text = "⚙️ 设置",
                Dock = DockStyle.Top,
                Height = 50,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Padding = new Padding(10, 8, 0, 0)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(20, 10, 20, 10),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;

            // Language
            _languageCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            _languageCombo.Items.AddRange(new object[] { "zh-CN (简体中文)", "en-US (English)" });
            AddRow(table, row++, "界面语言:", _languageCombo);

            // Checkboxes
            _cbBackupBeforeClean = new CheckBox { Text = "清理前自动备份文件", AutoSize = true };
            AddRow(table, row++, "", _cbBackupBeforeClean);

            _cbPreviewBeforeClean = new CheckBox { Text = "清理前预览待删文件", AutoSize = true };
            AddRow(table, row++, "", _cbPreviewBeforeClean);

            _cbScheduledClean = new CheckBox { Text = "启用定时自动清理", AutoSize = true };
            AddRow(table, row++, "", _cbScheduledClean);

            // Schedule interval
            _scheduleDays = new NumericUpDown { Minimum = 1, Maximum = 365, Width = 80 };
            AddRow(table, row++, "自动清理间隔（天）:", _scheduleDays);

            // Max backup days
            _maxBackupDays = new NumericUpDown { Minimum = 1, Maximum = 365, Width = 80 };
            AddRow(table, row++, "备份保留天数:", _maxBackupDays);

            // Large file threshold
            _largeFileMb = new NumericUpDown { Minimum = 1, Maximum = 10240, Width = 80 };
            AddRow(table, row++, "大文件阈值（MB）:", _largeFileMb);

            // Log directory link
            var logDirLink = new LinkLabel { Text = AppLogger.GetLogDirectory(), AutoSize = true };
            logDirLink.LinkClicked += (_, _) =>
            {
                var dir = AppLogger.GetLogDirectory();
                if (Directory.Exists(dir))
                    System.Diagnostics.Process.Start("explorer.exe", dir);
            };
            AddRow(table, row++, "日志目录:", logDirLink);

            _saveBtn = new Button
            {
                Text = "💾 保存设置",
                Width = 120,
                Height = 34,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(20, 10, 0, 0)
            };
            _saveBtn.Click += (_, _) => SaveSettings();

            _statusLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(20, 4, 0, 0),
                ForeColor = Color.Green
            };

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(20, 0, 0, 0)
            };
            btnPanel.Controls.Add(_saveBtn);
            btnPanel.Controls.Add(_statusLabel);

            Controls.Add(btnPanel);
            Controls.Add(table);
            Controls.Add(title);
        }

        private static void AddRow(TableLayoutPanel table, int row, string labelText, Control control)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lbl = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0)
            };
            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private void LoadSettings()
        {
            var s = ConfigManager.Settings;
            _languageCombo.SelectedIndex = s.Language == "en-US" ? 1 : 0;
            _cbBackupBeforeClean.Checked = s.BackupBeforeClean;
            _cbPreviewBeforeClean.Checked = s.PreviewBeforeClean;
            _cbScheduledClean.Checked = s.ScheduledCleanEnabled;
            _scheduleDays.Value = Math.Clamp(s.ScheduledCleanIntervalDays, 1, 365);
            _maxBackupDays.Value = Math.Clamp(s.MaxBackupAgeDays, 1, 365);
            _largeFileMb.Value = Math.Clamp(s.LargeFileSizeThresholdMB, 1, 10240);
        }

        private void SaveSettings()
        {
            var s = ConfigManager.Settings;
            s.Language = _languageCombo.SelectedIndex == 1 ? "en-US" : "zh-CN";
            s.BackupBeforeClean = _cbBackupBeforeClean.Checked;
            s.PreviewBeforeClean = _cbPreviewBeforeClean.Checked;
            s.ScheduledCleanEnabled = _cbScheduledClean.Checked;
            s.ScheduledCleanIntervalDays = (int)_scheduleDays.Value;
            s.MaxBackupAgeDays = (int)_maxBackupDays.Value;
            s.LargeFileSizeThresholdMB = (long)_largeFileMb.Value;
            ConfigManager.Save();
            _statusLabel.Text = "✔ 已保存";
            AppLogger.Info("Settings saved.");
        }
    }
}
