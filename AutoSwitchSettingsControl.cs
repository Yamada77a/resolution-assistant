namespace ResolutionAssistant;

internal sealed class AutoSwitchSettingsControl : UserControl
{
    private readonly List<Preset> presets;
    private readonly CheckBox enabledBox = new DarkCheckBox();
    private readonly TextBox pathBox = new();
    private readonly ComboBox runningPresetBox = new DarkComboBox();
    private readonly ComboBox stoppedPresetBox = new DarkComboBox();
    private readonly Label selectionNote = Theme.Label(string.Empty, 9);

    public AutoSwitchSettingsControl(List<Preset> presets, AutoSwitchRule rule)
    {
        this.presets = presets;
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;
        AutoScroll = true;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 10
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < layout.RowCount; row++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        var heading = Theme.Label("让程序启动与退出，自动切换预设", 16, true);
        heading.Dock = DockStyle.Fill;
        heading.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(heading, 0, 0);
        var description = Theme.Label("仅切换 Windows 主显示器。选择一个程序的完整 EXE 路径，再指定运行和全部退出时使用的预设。", 9.5f);
        description.ForeColor = Theme.Muted;
        description.Dock = DockStyle.Fill;
        description.Margin = new Padding(0, 0, 0, 18);
        layout.Controls.Add(description, 0, 1);

        enabledBox.Name = "AutoSwitchEnabled";
        enabledBox.Text = "启用程序自动切换";
        enabledBox.AccessibleName = enabledBox.Text;
        enabledBox.AutoSize = true;
        enabledBox.Checked = rule.Enabled;
        enabledBox.Margin = new Padding(0, 0, 0, 18);
        layout.Controls.Add(enabledBox, 0, 2);

        var pathLabel = Theme.Label("程序 EXE 路径", 10, true);
        pathLabel.Margin = new Padding(0, 0, 0, 6);
        layout.Controls.Add(pathLabel, 0, 3);
        var pathLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 16)
        };
        pathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathBox.Name = "AutoSwitchPath";
        pathBox.AccessibleName = "自动切换程序 EXE 完整路径";
        pathBox.Text = rule.ExecutablePath;
        pathBox.Dock = DockStyle.Fill;
        pathBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        pathBox.Margin = new Padding(0, 4, 10, 4);
        var browseButton = Theme.Button("浏览...");
        browseButton.Name = "BrowseProgram";
        browseButton.AccessibleName = "浏览并选择程序 EXE";
        browseButton.Margin = Padding.Empty;
        browseButton.Click += (_, _) => BrowseProgram();
        pathLayout.Controls.Add(pathBox, 0, 0);
        pathLayout.Controls.Add(browseButton, 1, 0);
        layout.Controls.Add(pathLayout, 0, 4);

        var choices = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8)
        };
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        choices.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        choices.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var runningLabel = Theme.Label("程序运行时", 10, true);
        runningLabel.Margin = new Padding(0, 0, 12, 6);
        var stoppedLabel = Theme.Label("程序全部退出后", 10, true);
        stoppedLabel.Margin = new Padding(0, 0, 0, 6);
        choices.Controls.Add(runningLabel, 0, 0);
        choices.Controls.Add(stoppedLabel, 1, 0);
        ConfigurePresetBox(runningPresetBox, "RunningPreset", "程序运行时的预设");
        ConfigurePresetBox(stoppedPresetBox, "StoppedPreset", "程序全部退出后的预设");
        runningPresetBox.Margin = new Padding(0, 0, 12, 0);
        stoppedPresetBox.Margin = Padding.Empty;
        choices.Controls.Add(runningPresetBox, 0, 1);
        choices.Controls.Add(stoppedPresetBox, 1, 1);
        layout.Controls.Add(choices, 0, 5);

        selectionNote.Dock = DockStyle.Fill;
        selectionNote.ForeColor = Theme.Muted;
        selectionNote.Margin = new Padding(0, 0, 0, 16);
        layout.Controls.Add(selectionNote, 0, 6);
        var behavior = Theme.Label(
            "• 每约 1 秒按完整路径精确检测，不按程序名称匹配。\n" +
            "• 启用或打开助手时，程序已运行也会应用运行预设；最后一个实例退出时才应用退出预设。\n" +
            "• 仅在状态变化时切换一次，不持续强制模式；手动切换和全局快捷键仍可照常使用。\n" +
            "• 权限不足或检测失败不会被当作程序退出；禁用监测或退出助手不会改变分辨率。", 9);
        behavior.ForeColor = Theme.Muted;
        behavior.Dock = DockStyle.Fill;
        behavior.Margin = new Padding(0, 0, 0, 16);
        layout.Controls.Add(behavior, 0, 7);
        var warning = Theme.Label("注意：不校验显示器是否支持预设模式，切换失败或画面异常时不会自动恢复。请先确认预设适合你的主显示器。", 9);
        warning.ForeColor = Theme.Accent;
        warning.BackColor = Theme.WarningSurface;
        warning.Padding = new Padding(12);
        warning.Dock = DockStyle.Fill;
        warning.Margin = Padding.Empty;
        layout.Controls.Add(warning, 0, 8);
        var draftNote = Theme.Label("修改仅在保存设置后生效；取消或关闭本窗口不影响当前监测。", 9);
        draftNote.ForeColor = Theme.Muted;
        draftNote.Dock = DockStyle.Fill;
        draftNote.Margin = new Padding(0, 12, 0, 0);
        layout.Controls.Add(draftNote, 0, 9);

        // Only absent IDs get initial suggestions. A deleted configured ID remains visibly missing.
        SetChoices(runningPresetBox, rule.RunningPresetId ?? presets.ElementAtOrDefault(1)?.Id ?? presets.FirstOrDefault()?.Id);
        SetChoices(stoppedPresetBox, rule.StoppedPresetId ?? presets.FirstOrDefault()?.Id);
        enabledBox.CheckedChanged += (_, _) => UpdateSelectionNote();
        runningPresetBox.SelectedIndexChanged += (_, _) => UpdateSelectionNote();
        stoppedPresetBox.SelectedIndexChanged += (_, _) => UpdateSelectionNote();
        UpdateSelectionNote();
        Theme.StyleInputs(this);
    }

    private static void ConfigurePresetBox(ComboBox box, string name, string accessibleName)
    {
        box.Name = name;
        box.AccessibleName = accessibleName;
        box.DropDownStyle = ComboBoxStyle.DropDownList;
        box.Dock = DockStyle.Fill;
        box.IntegralHeight = false;
        box.DropDownHeight = 240;
    }

    private void BrowseProgram()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择要监测的程序",
            Filter = "可执行程序 (*.exe)|*.exe",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            pathBox.Text = dialog.FileName;
    }

    public void RefreshPresets()
    {
        var runningId = SelectedId(runningPresetBox);
        var stoppedId = SelectedId(stoppedPresetBox);
        SetChoices(runningPresetBox, runningId);
        SetChoices(stoppedPresetBox, stoppedId);
        UpdateSelectionNote();
    }

    private void SetChoices(ComboBox box, Guid? selectedId)
    {
        box.BeginUpdate();
        try
        {
            box.Items.Clear();
            box.Items.Add(new PresetChoice(null, "请选择预设"));
            foreach (var preset in presets)
                box.Items.Add(new PresetChoice(preset.Id, $"{preset.Name}  ·  {preset.ModeLabel}"));
            var selectedIndex = selectedId.HasValue ? presets.FindIndex(preset => preset.Id == selectedId.Value) + 1 : 0;
            if (selectedId.HasValue && selectedIndex == 0)
            {
                box.Items.Add(new PresetChoice(selectedId, "已删除的预设（请重新选择）"));
                selectedIndex = box.Items.Count - 1;
            }
            box.SelectedIndex = selectedIndex;
            box.DropDownWidth = Math.Max(box.Width, box.Items.Cast<PresetChoice>()
                .Max(choice => TextRenderer.MeasureText(choice.Label, box.Font).Width) + SystemInformation.VerticalScrollBarWidth + 20);
        }
        finally
        {
            box.EndUpdate();
        }
    }

    private static Guid? SelectedId(ComboBox box) => (box.SelectedItem as PresetChoice)?.Id;

    private bool HasPreset(Guid? id) => id.HasValue && presets.Any(preset => preset.Id == id.Value);

    private void UpdateSelectionNote()
    {
        bool complete = HasPreset(SelectedId(runningPresetBox)) && HasPreset(SelectedId(stoppedPresetBox));
        selectionNote.Text = complete
            ? "预设名称与模式会随“预设与启动”页中的修改同步更新。"
            : "预设未选择或已被删除：请重新选择两个预设，或禁用自动切换后保存。";
        selectionNote.ForeColor = !complete && enabledBox.Checked ? Theme.Accent : Theme.Muted;
    }

    public string? CollectRule(out AutoSwitchRule rule)
    {
        rule = new AutoSwitchRule
        {
            Enabled = enabledBox.Checked,
            ExecutablePath = pathBox.Text.Trim(),
            RunningPresetId = SelectedId(runningPresetBox),
            StoppedPresetId = SelectedId(stoppedPresetBox)
        };
        if (!rule.Enabled)
            return null;
        if (rule.ExecutablePath.Length == 0 || !Path.IsPathFullyQualified(rule.ExecutablePath))
        {
            pathBox.Focus();
            return "启用自动切换前，请填写完整的程序 EXE 路径（例如 C:\\Games\\Game.exe）。";
        }
        try
        {
            rule.ExecutablePath = Path.GetFullPath(rule.ExecutablePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or System.Security.SecurityException)
        {
            pathBox.Focus();
            return $"程序路径无效：{ex.Message}";
        }
        if (!string.Equals(Path.GetExtension(rule.ExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            pathBox.Focus();
            return "自动切换程序的完整路径必须以 .exe 结尾。";
        }
        if (!HasPreset(rule.RunningPresetId) || !HasPreset(rule.StoppedPresetId))
        {
            (!HasPreset(rule.RunningPresetId) ? runningPresetBox : stoppedPresetBox).Focus();
            return "运行时或全部退出后的预设未选择或已被删除。请重新选择现有预设，或禁用自动切换后保存。";
        }
        pathBox.Text = rule.ExecutablePath;
        return null;
    }

    private sealed record PresetChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }
}
