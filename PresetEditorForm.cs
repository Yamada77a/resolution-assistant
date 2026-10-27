using System.Globalization;

namespace ResolutionAssistant;

internal sealed class PresetEditorForm : Form
{
    private readonly Preset original;
    private Preset result;
    private readonly TextBox nameBox = new() { Name = "PresetName" };
    private readonly TextBox widthBox = new() { Name = "PresetWidth" };
    private readonly TextBox heightBox = new() { Name = "PresetHeight" };
    private readonly TextBox refreshBox = new() { Name = "PresetRefresh" };
    private readonly CheckBox ctrlBox = new() { Name = "HotkeyCtrl", Text = "Ctrl", AutoSize = true };
    private readonly CheckBox altBox = new() { Name = "HotkeyAlt", Text = "Alt", AutoSize = true };
    private readonly CheckBox shiftBox = new() { Name = "HotkeyShift", Text = "Shift", AutoSize = true };
    private readonly CheckBox winBox = new() { Name = "HotkeyWin", Text = "Win", AutoSize = true };
    private readonly ComboBox keyBox = new() { Name = "HotkeyKey", DropDownStyle = ComboBoxStyle.DropDownList };

    public Preset Result => result.Copy();

    public PresetEditorForm(Preset? preset = null)
    {
        original = preset?.Copy() ?? new Preset();
        result = original.Copy();
        Theme.Apply(this);
        Text = preset is null ? "添加预设" : "编辑预设";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(600, 650);
        MinimumSize = new Size(560, 660);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 6
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        var heading = Theme.Label(Text, 18, true);
        heading.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(heading, 0, 0);
        var intro = Theme.Label("输入自定义参数；仅应用于 Windows 主显示器。", 9.5f);
        intro.ForeColor = Theme.Muted;
        intro.Dock = DockStyle.Fill;
        intro.Margin = new Padding(0, 0, 0, 20);
        layout.Controls.Add(intro, 0, 1);

        var fields = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 5,
            Margin = Padding.Empty,
            TabIndex = 0
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(fields, 0, "预设名称", nameBox);
        AddField(fields, 1, "宽度（像素）", widthBox);
        AddField(fields, 2, "高度（像素）", heightBox);
        AddField(fields, 3, "刷新率（Hz）", refreshBox);

        nameBox.Text = original.Name;
        widthBox.Text = original.Width.ToString(CultureInfo.InvariantCulture);
        heightBox.Text = original.Height.ToString(CultureInfo.InvariantCulture);
        refreshBox.Text = original.RefreshRate.ToString(CultureInfo.InvariantCulture);

        var hotkeyLabel = Theme.Label("全局快捷键");
        hotkeyLabel.Anchor = AnchorStyles.Left;
        hotkeyLabel.Margin = new Padding(0, 8, 18, 12);
        fields.Controls.Add(hotkeyLabel, 0, 4);
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var shortcutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 4, 0, 12),
            TabIndex = 4
        };
        shortcutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shortcutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shortcutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var modifiers = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 6),
            TabIndex = 0
        };
        var modifierBoxes = new[] { ctrlBox, altBox, shiftBox, winBox };
        for (int index = 0; index < modifierBoxes.Length; index++)
        {
            var box = modifierBoxes[index];
            box.TabIndex = index;
            box.AccessibleName = $"快捷键修饰键 {box.Text}";
            box.Margin = new Padding(0, 0, 14, 4);
            modifiers.Controls.Add(box);
        }
        shortcutPanel.Controls.Add(modifiers, 0, 0);
        keyBox.Dock = DockStyle.Fill;
        keyBox.Margin = Padding.Empty;
        keyBox.TabIndex = 1;
        keyBox.AccessibleName = "快捷键主键";
        PopulateKeys(original.VirtualKey);
        ctrlBox.Checked = (original.Modifiers & 2) != 0;
        altBox.Checked = (original.Modifiers & 1) != 0;
        shiftBox.Checked = (original.Modifiers & 4) != 0;
        winBox.Checked = (original.Modifiers & 8) != 0;
        keyBox.SelectedIndexChanged += (_, _) => UpdateModifierState();
        UpdateModifierState();
        shortcutPanel.Controls.Add(keyBox, 0, 1);
        fields.Controls.Add(shortcutPanel, 1, 4);
        layout.Controls.Add(fields, 0, 2);

        var explanation = Theme.Label("选择“未设置”可取消快捷键。快捷键在后台全局生效；冲突会在保存设置时提示。", 9);
        explanation.ForeColor = Theme.Muted;
        explanation.Dock = DockStyle.Fill;
        explanation.Margin = new Padding(0, 2, 0, 12);
        layout.Controls.Add(explanation, 0, 3);
        var warning = Theme.Label("宽度、高度和刷新率仅检查是否为 uint 整数（允许 0），不检查硬件支持情况。", 9);
        warning.ForeColor = Theme.Muted;
        warning.Dock = DockStyle.Fill;
        warning.Margin = new Padding(0, 8, 0, 16);
        layout.Controls.Add(warning, 0, 4);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty,
            TabIndex = 1
        };
        var cancelButton = Theme.Button("取消");
        cancelButton.Name = "CancelPreset";
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.AccessibleName = "取消编辑预设";
        cancelButton.TabIndex = 1;
        cancelButton.Margin = new Padding(10, 0, 0, 0);
        var saveButton = Theme.Button("确定", true);
        saveButton.Name = "SavePreset";
        saveButton.AccessibleName = "确定预设编辑";
        saveButton.TabIndex = 0;
        saveButton.Margin = Padding.Empty;
        saveButton.Click += (_, _) => SavePreset();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(saveButton);
        layout.Controls.Add(buttons, 0, 5);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Shown += (_, _) =>
        {
            nameBox.Focus();
            nameBox.SelectAll();
        };
    }

    private static void AddField(TableLayoutPanel fields, int row, string caption, TextBox box)
    {
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = Theme.Label(caption);
        label.Anchor = AnchorStyles.Left;
        label.Margin = new Padding(0, 7, 18, 14);
        fields.Controls.Add(label, 0, row);
        box.Dock = DockStyle.Fill;
        box.Margin = new Padding(0, 3, 0, 12);
        box.TabIndex = row;
        box.AccessibleName = caption;
        fields.Controls.Add(box, 1, row);
    }

    private void PopulateKeys(uint selectedKey)
    {
        keyBox.Items.Add(new KeyChoice(0, "未设置"));
        for (uint key = (uint)Keys.A; key <= (uint)Keys.Z; key++)
            keyBox.Items.Add(new KeyChoice(key, ((char)key).ToString()));
        for (uint key = (uint)Keys.D0; key <= (uint)Keys.D9; key++)
            keyBox.Items.Add(new KeyChoice(key, ((char)key).ToString()));
        for (uint key = (uint)Keys.F1; key <= (uint)Keys.F24; key++)
            keyBox.Items.Add(new KeyChoice(key, ((Keys)key).ToString()));
        Keys[] additional =
        [
            Keys.Space, Keys.Tab, Keys.Enter, Keys.Escape, Keys.Back,
            Keys.Insert, Keys.Delete, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown,
            Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Pause, Keys.PrintScreen,
            Keys.CapsLock, Keys.NumLock, Keys.Scroll,
            Keys.NumPad0, Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4,
            Keys.NumPad5, Keys.NumPad6, Keys.NumPad7, Keys.NumPad8, Keys.NumPad9,
            Keys.Add, Keys.Subtract, Keys.Multiply, Keys.Divide, Keys.Decimal,
            Keys.OemMinus, Keys.Oemplus, Keys.Oemcomma, Keys.OemPeriod,
            Keys.OemQuestion, Keys.OemSemicolon, Keys.OemQuotes,
            Keys.OemOpenBrackets, Keys.OemCloseBrackets, Keys.OemPipe, Keys.Oemtilde
        ];
        foreach (var key in additional)
            keyBox.Items.Add(new KeyChoice((uint)key, key.ToString()));
        var selectedIndex = -1;
        for (int index = 0; index < keyBox.Items.Count; index++)
        {
            if (keyBox.Items[index] is KeyChoice choice && choice.Value == selectedKey)
            {
                selectedIndex = index;
                break;
            }
        }
        if (selectedIndex < 0)
        {
            var text = Enum.GetName(typeof(Keys), (Keys)selectedKey) ?? $"按键 0x{selectedKey:X2}";
            selectedIndex = keyBox.Items.Add(new KeyChoice(selectedKey, text));
        }
        keyBox.SelectedIndex = selectedIndex;
    }

    private void UpdateModifierState()
    {
        var enabled = keyBox.SelectedItem is KeyChoice { Value: not 0 };
        ctrlBox.Enabled = altBox.Enabled = shiftBox.Enabled = winBox.Enabled = enabled;
        if (!enabled)
            ctrlBox.Checked = altBox.Checked = shiftBox.Checked = winBox.Checked = false;
    }

    private void SavePreset()
    {
        var name = nameBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowInputError("请填写预设名称。", nameBox);
            return;
        }
        if (!TryReadNumber(widthBox, "宽度", out var width) ||
            !TryReadNumber(heightBox, "高度", out var height) ||
            !TryReadNumber(refreshBox, "刷新率", out var refreshRate))
            return;

        var edited = original.Copy();
        edited.Name = name;
        edited.Width = width;
        edited.Height = height;
        edited.RefreshRate = refreshRate;
        edited.VirtualKey = (keyBox.SelectedItem as KeyChoice)?.Value ?? 0;
        edited.Modifiers = edited.VirtualKey == 0 ? 0u :
            (ctrlBox.Checked ? 2u : 0u) | (altBox.Checked ? 1u : 0u) |
            (shiftBox.Checked ? 4u : 0u) | (winBox.Checked ? 8u : 0u);
        result = edited;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryReadNumber(TextBox box, string caption, out uint value)
    {
        if (uint.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;
        ShowInputError($"{caption}必须是 0 到 4294967295 的整数。", box);
        return false;
    }

    private void ShowInputError(string message, TextBox box)
    {
        MessageBox.Show(this, message, "请检查输入", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        box.Focus();
        box.SelectAll();
    }

    private sealed record KeyChoice(uint Value, string Caption)
    {
        public override string ToString() => Caption;
    }
}
