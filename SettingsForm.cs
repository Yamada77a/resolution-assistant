using System.Globalization;

namespace ResolutionAssistant;

internal sealed class SettingsForm : Form
{
    private readonly AppConfig draft;
    private readonly Func<AppConfig, bool, string?> save;
    private readonly DataGridView grid = new();
    private readonly CheckBox autoStartBox = new();
    private readonly Label listStatus = Theme.Label(string.Empty, 9);
    private readonly Button editButton = Theme.Button("编辑");
    private readonly Button deleteButton = Theme.Button("删除");
    private readonly Button upButton = Theme.Button("上移 ↑");
    private readonly Button downButton = Theme.Button("下移 ↓");

    public SettingsForm(AppConfig config, bool autoStart, Func<AppConfig, bool, string?> save)
    {
        draft = config.Copy();
        this.save = save;
        Theme.Apply(this);
        Text = "预设与启动设置";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        ClientSize = new Size(880, 670);
        MinimumSize = new Size(780, 620);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 9
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int row = 3; row < 9; row++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        var heading = Theme.Label("管理你的分辨率预设", 18, true);
        heading.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(heading, 0, 0);
        var description = Theme.Label("固定目标：Windows 主显示器。列表顺序也用于主窗口和托盘菜单。", 9.5f);
        description.ForeColor = Theme.Muted;
        description.Dock = DockStyle.Fill;
        description.Margin = new Padding(0, 0, 0, 18);
        layout.Controls.Add(description, 0, 1);

        ConfigureGrid();
        layout.Controls.Add(grid, 0, 2);
        listStatus.ForeColor = Theme.Muted;
        listStatus.Dock = DockStyle.Fill;
        listStatus.Margin = new Padding(0, 8, 0, 10);
        layout.Controls.Add(listStatus, 0, 3);

        var tools = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 16),
            TabIndex = 1
        };
        var addButton = Theme.Button("添加预设", true);
        addButton.Name = "AddPreset";
        editButton.Name = "EditPreset";
        deleteButton.Name = "DeletePreset";
        upButton.Name = "MovePresetUp";
        downButton.Name = "MovePresetDown";
        addButton.AccessibleName = "添加分辨率预设";
        editButton.AccessibleName = "编辑选中的预设";
        deleteButton.AccessibleName = "删除选中的预设";
        upButton.AccessibleName = "上移选中的预设";
        downButton.AccessibleName = "下移选中的预设";
        Button[] toolButtons = [addButton, editButton, deleteButton, upButton, downButton];
        for (int index = 0; index < toolButtons.Length; index++)
        {
            var button = toolButtons[index];
            button.TabIndex = index;
            button.Width = 108;
            button.Margin = new Padding(0, 0, 10, 6);
            tools.Controls.Add(button);
        }
        addButton.Click += (_, _) => AddPreset();
        editButton.Click += (_, _) => EditPreset();
        deleteButton.Click += (_, _) => DeletePreset();
        upButton.Click += (_, _) => MovePreset(-1);
        downButton.Click += (_, _) => MovePreset(1);
        layout.Controls.Add(tools, 0, 4);

        autoStartBox.Text = "开机后在托盘启动";
        autoStartBox.Name = "StartupToggle";
        autoStartBox.Checked = autoStart;
        autoStartBox.AutoSize = true;
        autoStartBox.TabIndex = 2;
        autoStartBox.AccessibleName = "开机后在托盘启动";
        autoStartBox.Margin = new Padding(0, 0, 0, 12);
        layout.Controls.Add(autoStartBox, 0, 5);
        var hotkeyNote = Theme.Label("快捷键为全局快捷键；保存设置时检查冲突。取消或关闭本窗口不会应用修改。", 9);
        hotkeyNote.ForeColor = Theme.Muted;
        hotkeyNote.Dock = DockStyle.Fill;
        hotkeyNote.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(hotkeyNote, 0, 6);

        var warning = Theme.Label("注意：可保存任意数值（含 0），不校验显示器支持情况。切换不受支持的模式可能失败或造成画面异常；本程序不会自动恢复，请谨慎操作。", 9);
        warning.ForeColor = Color.FromArgb(153, 87, 25);
        warning.BackColor = Color.FromArgb(255, 247, 230);
        warning.Padding = new Padding(12);
        warning.Dock = DockStyle.Fill;
        warning.Margin = new Padding(0, 0, 0, 18);
        layout.Controls.Add(warning, 0, 7);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty,
            TabIndex = 3
        };
        var cancelButton = Theme.Button("取消");
        cancelButton.Name = "CancelSettings";
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.AccessibleName = "取消设置修改";
        cancelButton.TabIndex = 1;
        cancelButton.Margin = new Padding(10, 0, 0, 0);
        var saveButton = Theme.Button("保存设置", true);
        saveButton.Name = "SaveSettings";
        saveButton.AccessibleName = "保存预设和启动设置";
        saveButton.TabIndex = 0;
        saveButton.Margin = Padding.Empty;
        saveButton.Click += (_, _) => SaveSettings();
        footer.Controls.Add(cancelButton);
        footer.Controls.Add(saveButton);
        layout.Controls.Add(footer, 0, 8);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        ReloadRows(draft.Presets.FirstOrDefault()?.Id);
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.Name = "PresetList";
        grid.Margin = Padding.Empty;
        grid.TabIndex = 0;
        grid.AccessibleName = "分辨率预设列表";
        grid.AccessibleDescription = "选择预设后可编辑、删除或调整顺序，双击可编辑。";
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AllowUserToOrderColumns = false;
        grid.AutoGenerateColumns = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.RowHeadersVisible = false;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.BackgroundColor = Theme.Surface;
        grid.GridColor = Theme.Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Background,
            ForeColor = Theme.Muted,
            SelectionBackColor = Theme.Background,
            SelectionForeColor = Theme.Muted,
            Padding = new Padding(10, 8, 10, 8),
            Font = Font
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Surface,
            ForeColor = Theme.Ink,
            SelectionBackColor = Color.FromArgb(230, 237, 255),
            SelectionForeColor = Theme.Ink,
            Padding = new Padding(10, 8, 10, 8),
            Font = Font,
            WrapMode = DataGridViewTriState.True
        };
        AddColumn("Name", "名称", 29, 130);
        AddColumn("Resolution", "分辨率", 25, 140);
        AddColumn("RefreshRate", "刷新率", 16, 95);
        AddColumn("Hotkey", "快捷键", 30, 160);
        grid.SelectionChanged += (_, _) => UpdateSelectionActions();
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                EditPreset();
        };
    }

    private void AddColumn(string name, string caption, float weight, int minimumWidth)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = caption,
            FillWeight = weight,
            MinimumWidth = minimumWidth,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
    }

    private int SelectedIndex => grid.CurrentRow?.Tag is Guid id
        ? draft.Presets.FindIndex(preset => preset.Id == id)
        : -1;

    private void ReloadRows(Guid? selectedId)
    {
        grid.Rows.Clear();
        foreach (var preset in draft.Presets)
        {
            var index = grid.Rows.Add(
                preset.Name,
                $"{preset.Width.ToString(CultureInfo.InvariantCulture)} × {preset.Height.ToString(CultureInfo.InvariantCulture)}",
                $"{preset.RefreshRate.ToString(CultureInfo.InvariantCulture)} Hz",
                preset.ShortcutLabel);
            grid.Rows[index].Tag = preset.Id;
        }
        grid.ClearSelection();
        if (grid.Rows.Count > 0)
        {
            var selectedIndex = selectedId.HasValue
                ? draft.Presets.FindIndex(preset => preset.Id == selectedId.Value)
                : 0;
            if (selectedIndex < 0)
                selectedIndex = 0;
            grid.CurrentCell = grid.Rows[selectedIndex].Cells[0];
            grid.Rows[selectedIndex].Selected = true;
            grid.FirstDisplayedScrollingRowIndex = selectedIndex;
        }
        else
        {
            grid.CurrentCell = null;
        }
        listStatus.Text = draft.Presets.Count == 0
            ? "暂无预设。点击“添加预设”创建你的第一个自定义模式。"
            : $"共 {draft.Presets.Count} 个预设 · 双击编辑，使用上移 / 下移调整顺序";
        UpdateSelectionActions();
    }

    private void UpdateSelectionActions()
    {
        var index = SelectedIndex;
        editButton.Enabled = deleteButton.Enabled = index >= 0;
        upButton.Enabled = index > 0;
        downButton.Enabled = index >= 0 && index < draft.Presets.Count - 1;
    }

    private void AddPreset()
    {
        using var editor = new PresetEditorForm();
        if (editor.ShowDialog(this) != DialogResult.OK)
            return;
        var preset = editor.Result;
        draft.Presets.Add(preset);
        ReloadRows(preset.Id);
    }

    private void EditPreset()
    {
        var index = SelectedIndex;
        if (index < 0)
            return;
        using var editor = new PresetEditorForm(draft.Presets[index]);
        if (editor.ShowDialog(this) != DialogResult.OK)
            return;
        var preset = editor.Result;
        draft.Presets[index] = preset;
        ReloadRows(preset.Id);
    }

    private void DeletePreset()
    {
        var index = SelectedIndex;
        if (index < 0)
            return;
        var preset = draft.Presets[index];
        if (MessageBox.Show(this, $"确定删除预设“{preset.Name}”吗？\n保存设置后才会生效。", "删除预设",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;
        draft.Presets.RemoveAt(index);
        var nextId = draft.Presets.Count == 0 ? (Guid?)null : draft.Presets[Math.Min(index, draft.Presets.Count - 1)].Id;
        ReloadRows(nextId);
    }

    private void MovePreset(int offset)
    {
        var index = SelectedIndex;
        var destination = index + offset;
        if (index < 0 || destination < 0 || destination >= draft.Presets.Count)
            return;
        var preset = draft.Presets[index];
        draft.Presets.RemoveAt(index);
        draft.Presets.Insert(destination, preset);
        ReloadRows(preset.Id);
    }

    private void SaveSettings()
    {
        var error = save(draft, autoStartBox.Checked);
        if (error is not null)
        {
            MessageBox.Show(this, error, "无法保存设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
