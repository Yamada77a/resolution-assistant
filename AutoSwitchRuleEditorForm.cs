namespace ResolutionAssistant;

internal sealed class AutoSwitchRuleEditorForm : DarkForm
{
    private readonly List<Preset> presets;
    private readonly AutoSwitchRule original;
    private AutoSwitchRule result;
    private readonly TextBox nameBox = new() { Name = "RuleName" };
    private readonly TextBox pathBox = new() { Name = "AutoSwitchPath" };
    private readonly CheckBox enabledBox = new DarkCheckBox { Name = "RuleEnabled" };
    private readonly ComboBox runningPresetBox = new DarkComboBox { Name = "RunningPreset" };
    private readonly ComboBox stoppedPresetBox = new DarkComboBox { Name = "StoppedPreset" };
    private readonly Label selectionNote = Theme.Label(string.Empty, 9);

    public AutoSwitchRule Result => result.Copy();

    public AutoSwitchRuleEditorForm(List<Preset> presets, AutoSwitchRule rule)
    {
        this.presets = presets;
        original = rule.Copy();
        result = original.Copy();
        Theme.Apply(this);
        Name = "AutoSwitchRuleEditor";
        Text = "编辑游戏卡片";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ShowIcon = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(680, 610);
        MinimumSize = new Size(560, 480);

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 1,
            RowCount = 2
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Content.Controls.Add(outer);
        var viewport = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Margin = new Padding(0, 0, 0, 14)
        };
        Theme.StyleNativeScrollbars(viewport);
        outer.Controls.Add(viewport, 0, 0);
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 10,
            Margin = Padding.Empty
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < fields.RowCount; row++)
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        viewport.Controls.Add(fields);
        var heading = Theme.Label("为游戏安排专属预设", 17, true);
        heading.Dock = DockStyle.Fill;
        heading.Margin = new Padding(0, 0, 0, 7);
        fields.Controls.Add(heading, 0, 0);
        var intro = Theme.Label("卡片只展示游戏名称；程序位置仅在此编辑。保存卡片后，还需在外层点击“保存设置”才会生效。", 9.5f);
        intro.ForeColor = Theme.Muted;
        intro.Dock = DockStyle.Fill;
        intro.Margin = new Padding(0, 0, 0, 18);
        fields.Controls.Add(intro, 0, 1);

        AddFieldLabel(fields, "游戏名称", 2);
        nameBox.Text = original.Name;
        nameBox.AccessibleName = "游戏卡片名称";
        nameBox.Dock = DockStyle.Fill;
        nameBox.Margin = new Padding(0, 0, 0, 14);
        fields.Controls.Add(nameBox, 0, 3);
        AddFieldLabel(fields, "程序 EXE 完整路径", 4);
        var pathLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        pathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pathBox.Text = original.ExecutablePath;
        pathBox.AccessibleName = "游戏程序 EXE 完整路径";
        pathBox.Dock = DockStyle.Fill;
        pathBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        pathBox.Margin = new Padding(0, 4, 10, 4);
        pathLayout.Controls.Add(pathBox, 0, 0);
        var browseButton = Theme.Button("浏览…");
        browseButton.Name = "BrowseProgram";
        browseButton.AccessibleName = "浏览并选择游戏程序 EXE";
        browseButton.Margin = Padding.Empty;
        browseButton.Click += (_, _) => BrowseProgram();
        pathLayout.Controls.Add(browseButton, 1, 0);
        fields.Controls.Add(pathLayout, 0, 5);
        var pathNote = Theme.Label("按完整位置精确匹配，不会匹配其他目录的同名程序；同一游戏的最后一个实例退出后才视为退出。", 8.5f);
        pathNote.ForeColor = Theme.Muted;
        pathNote.Dock = DockStyle.Fill;
        pathNote.Margin = new Padding(0, 0, 0, 16);
        fields.Controls.Add(pathNote, 0, 6);
        enabledBox.Text = "启用这张游戏卡片";
        enabledBox.AccessibleName = enabledBox.Text;
        enabledBox.AutoSize = true;
        enabledBox.Checked = original.Enabled;
        enabledBox.Margin = new Padding(0, 0, 0, 16);
        fields.Controls.Add(enabledBox, 0, 7);

        var choices = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        choices.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        choices.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var runningLabel = Theme.Label("游戏运行时", 10, true);
        runningLabel.Dock = DockStyle.Fill;
        runningLabel.Margin = new Padding(0, 0, 12, 7);
        var stoppedLabel = Theme.Label("所有目标退出后", 10, true);
        stoppedLabel.Dock = DockStyle.Fill;
        stoppedLabel.Margin = new Padding(0, 0, 0, 7);
        choices.Controls.Add(runningLabel, 0, 0);
        choices.Controls.Add(stoppedLabel, 1, 0);
        ConfigurePresetBox(runningPresetBox, "游戏运行时的预设");
        ConfigurePresetBox(stoppedPresetBox, "所有已启用目标退出后使用的预设");
        runningPresetBox.Margin = new Padding(0, 0, 12, 0);
        stoppedPresetBox.Margin = Padding.Empty;
        choices.Controls.Add(runningPresetBox, 0, 1);
        choices.Controls.Add(stoppedPresetBox, 1, 1);
        fields.Controls.Add(choices, 0, 8);
        selectionNote.Dock = DockStyle.Fill;
        selectionNote.Margin = new Padding(0, 0, 0, 4);
        fields.Controls.Add(selectionNote, 0, 9);
        // Keep missing and unselected references unchanged; only newly added cards get suggestions.
        SetChoices(runningPresetBox, original.RunningPresetId);
        SetChoices(stoppedPresetBox, original.StoppedPresetId);
        enabledBox.CheckedChanged += (_, _) => UpdateSelectionNote();
        runningPresetBox.SelectedIndexChanged += (_, _) => UpdateSelectionNote();
        stoppedPresetBox.SelectedIndexChanged += (_, _) => UpdateSelectionNote();
        UpdateSelectionNote();

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty
        };
        var cancelButton = Theme.Button("取消");
        cancelButton.Name = "CancelRule";
        cancelButton.AccessibleName = "取消卡片修改";
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Margin = new Padding(10, 0, 0, 0);
        cancelButton.TabIndex = 1;
        var saveButton = Theme.Button("保存卡片", true);
        saveButton.Name = "SaveRule";
        saveButton.AccessibleName = "保存卡片到设置草稿";
        saveButton.Margin = Padding.Empty;
        saveButton.TabIndex = 0;
        saveButton.Click += (_, _) => SaveRule();
        footer.Controls.Add(cancelButton);
        footer.Controls.Add(saveButton);
        outer.Controls.Add(footer, 0, 1);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Theme.StyleInputs(Content);
        Shown += (_, _) =>
        {
            nameBox.Focus();
            nameBox.SelectAll();
        };
    }

    private static void AddFieldLabel(TableLayoutPanel fields, string text, int row)
    {
        var label = Theme.Label(text, 10, true);
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 0, 0, 7);
        fields.Controls.Add(label, 0, row);
    }

    private static void ConfigurePresetBox(ComboBox box, string accessibleName)
    {
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
            Title = "选择要监测的游戏程序",
            Filter = "可执行程序 (*.exe)|*.exe",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            pathBox.Text = dialog.FileName;
    }

    private void SetChoices(ComboBox box, Guid? selectedId)
    {
        box.Items.Add(new PresetChoice(null, "请选择预设"));
        foreach (var preset in presets)
            box.Items.Add(new PresetChoice(preset.Id, $"{preset.Name}  ·  {preset.ModeLabel}"));
        int selectedIndex = selectedId.HasValue ? presets.FindIndex(preset => preset.Id == selectedId.Value) + 1 : 0;
        if (selectedId.HasValue && selectedIndex == 0)
        {
            box.Items.Add(new PresetChoice(selectedId, "已删除的预设（请重新选择）"));
            selectedIndex = box.Items.Count - 1;
        }
        box.SelectedIndex = selectedIndex;
        box.DropDownWidth = Math.Max(box.Width, box.Items.Cast<PresetChoice>()
            .Max(choice => TextRenderer.MeasureText(choice.Label, box.Font).Width) + SystemInformation.VerticalScrollBarWidth + 20);
    }

    private static Guid? SelectedId(ComboBox box) => (box.SelectedItem as PresetChoice)?.Id;

    private bool HasPreset(Guid? id) => id.HasValue && presets.Any(preset => preset.Id == id.Value);

    private void UpdateSelectionNote()
    {
        bool complete = HasPreset(SelectedId(runningPresetBox)) && HasPreset(SelectedId(stoppedPresetBox));
        selectionNote.Text = !enabledBox.Checked
            ? "卡片已暂停：可以保留未完成的配置，重新启用前请补全。取消不会保留此次编辑。"
            : complete
                ? "仅当此卡片最后生效且所有已启用目标均已退出时，才应用它的退出预设。"
                : "预设未选择或已删除：请重新选择，或暂停卡片以保留未完成的配置。";
        selectionNote.ForeColor = enabledBox.Checked && !complete ? Theme.Accent : Theme.Muted;
    }

    private void SaveRule()
    {
        var candidate = original.Copy();
        candidate.Name = nameBox.Text.Trim();
        candidate.ExecutablePath = pathBox.Text.Trim();
        candidate.Enabled = enabledBox.Checked;
        candidate.RunningPresetId = SelectedId(runningPresetBox);
        candidate.StoppedPresetId = SelectedId(stoppedPresetBox);
        string? error = string.IsNullOrWhiteSpace(candidate.Name) ? "请填写游戏名称。" : null;
        string normalizedPath = string.Empty;
        if (error is null && candidate.Enabled)
            error = ValidateRule(candidate, presets, out normalizedPath);
        if (error is not null)
        {
            MessageBox.Show(this, error, "无法保存卡片", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (candidate.Name.Length == 0)
                nameBox.Focus();
            else if (normalizedPath.Length == 0)
                pathBox.Focus();
            else
                (!HasPreset(candidate.RunningPresetId) ? runningPresetBox : stoppedPresetBox).Focus();
            return;
        }
        if (candidate.Enabled)
            candidate.ExecutablePath = normalizedPath;
        result = candidate;
        DialogResult = DialogResult.OK;
        Close();
    }

    internal static string? ValidateRule(AutoSwitchRule rule, List<Preset> presets, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(rule.Name))
            return "请填写游戏名称。";
        string path = rule.ExecutablePath.Trim();
        if (path.Length == 0 || !Path.IsPathFullyQualified(path))
            return "请填写完整的程序 EXE 路径。";
        try
        {
            normalizedPath = Path.GetFullPath(path);
            if (!string.Equals(Path.GetExtension(normalizedPath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = string.Empty;
                return "程序的完整路径必须以 .exe 结尾。";
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or System.Security.SecurityException)
        {
            normalizedPath = string.Empty;
            return "程序路径无效，请在编辑窗口重新选择 EXE。";
        }
        if (!rule.RunningPresetId.HasValue || !presets.Any(preset => preset.Id == rule.RunningPresetId.Value) ||
            !rule.StoppedPresetId.HasValue || !presets.Any(preset => preset.Id == rule.StoppedPresetId.Value))
            return "运行或退出预设未选择或已被删除，请重新选择现有预设。";
        return null;
    }

    private sealed record PresetChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }
}
