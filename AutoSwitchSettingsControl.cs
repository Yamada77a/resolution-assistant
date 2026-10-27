namespace ResolutionAssistant;

internal sealed class AutoSwitchSettingsControl : UserControl
{
    private readonly List<Preset> presets;
    private readonly AutoSwitchSettings draft;
    private readonly CheckBox enabledBox = new DarkCheckBox();
    private readonly Label libraryStatus = Theme.Label(string.Empty, 9);
    private readonly Panel cardsViewport = new();
    private readonly TableLayoutPanel cards = new();

    public AutoSwitchSettingsControl(List<Preset> presets, AutoSwitchSettings settings)
    {
        this.presets = presets;
        draft = settings.Copy();
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        var heading = Theme.Label("游戏卡片 · 自动切换", 16, true);
        heading.Dock = DockStyle.Fill;
        heading.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(heading, 0, 0);
        var description = Theme.Label(
            "按从上到下的优先级，使用首个正在运行且已启用的游戏卡片。游戏间直接接棒；所有已启用目标都确认退出后，才使用最后生效卡片的退出预设。", 9.5f);
        description.ForeColor = Theme.Muted;
        description.Dock = DockStyle.Fill;
        description.Margin = new Padding(0, 0, 0, 12);
        layout.Controls.Add(description, 0, 1);

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        enabledBox.Name = "AutoSwitchEnabled";
        enabledBox.Text = "启用游戏自动切换";
        enabledBox.AccessibleName = enabledBox.Text;
        enabledBox.AutoSize = true;
        enabledBox.Anchor = AnchorStyles.Left;
        enabledBox.Checked = draft.Enabled;
        enabledBox.Margin = new Padding(0, 0, 12, 0);
        enabledBox.CheckedChanged += (_, _) => UpdateLibraryStatus();
        toolbar.Controls.Add(enabledBox, 0, 0);
        var addButton = Theme.Button("添加游戏", true);
        addButton.Name = "AddGameRule";
        addButton.AccessibleName = "添加游戏卡片";
        addButton.Margin = Padding.Empty;
        addButton.Click += (_, _) => AddRule();
        toolbar.Controls.Add(addButton, 1, 0);
        libraryStatus.Dock = DockStyle.Fill;
        libraryStatus.ForeColor = Theme.Muted;
        libraryStatus.Margin = new Padding(0, 6, 0, 0);
        toolbar.Controls.Add(libraryStatus, 0, 1);
        toolbar.SetColumnSpan(libraryStatus, 2);
        layout.Controls.Add(toolbar, 0, 2);

        cardsViewport.Dock = DockStyle.Fill;
        cardsViewport.AutoScroll = true;
        cardsViewport.BackColor = Theme.Background;
        cardsViewport.Margin = Padding.Empty;
        cardsViewport.AccessibleName = "可滚动的游戏卡片库";
        Theme.StyleNativeScrollbars(cardsViewport);
        cards.Name = "RuleCards";
        cards.AccessibleName = "游戏自动切换卡片列表，靠前的优先";
        cards.Dock = DockStyle.Top;
        cards.AutoSize = true;
        cards.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        cards.ColumnCount = 1;
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cards.Margin = Padding.Empty;
        cardsViewport.Controls.Add(cards);
        layout.Controls.Add(cardsViewport, 0, 3);

        var note = Theme.Label(
            "点击卡片编辑；上移 / 下移调整优先级。预设名称随预设页同步。\n" +
            "检测不确定时保持当前状态；手动切换仍可使用，停用卡片或总开关不会应用退出预设。\n" +
            "仅作用于主显示器，不校验模式支持情况且不会自动恢复。所有修改须“保存设置”才生效，取消全部丢弃。", 8.5f);
        note.ForeColor = Theme.Muted;
        note.Dock = DockStyle.Fill;
        note.Margin = new Padding(0, 10, 0, 0);
        layout.Controls.Add(note, 0, 4);
        RebuildCards();
        Theme.StyleInputs(this);
    }

    public void RefreshPresets() => RebuildCards();

    private void UpdateLibraryStatus()
    {
        int enabledCount = draft.Rules.Count(rule => rule.Enabled);
        libraryStatus.Text = $"{draft.Rules.Count} 张卡片 · {enabledCount} 张已启用" +
            (enabledBox.Checked ? " · 从上到下优先" : " · 总开关已暂停，仍可编辑卡片");
    }


    private void RebuildCards(Guid? focusId = null)
    {
        int scrollY = -cardsViewport.AutoScrollPosition.Y;
        cards.SuspendLayout();
        foreach (Control control in cards.Controls.Cast<Control>().ToArray())
            control.Dispose();
        cards.Controls.Clear();
        cards.RowStyles.Clear();
        cards.RowCount = Math.Max(1, draft.Rules.Count);
        if (draft.Rules.Count == 0)
        {
            var empty = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Theme.Surface,
                Padding = new Padding(22),
                Margin = Padding.Empty
            };
            empty.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var title = Theme.Label("你的游戏库还是空的", 13, true);
            title.Dock = DockStyle.Fill;
            title.Margin = new Padding(0, 0, 0, 8);
            var hint = Theme.Label("点击“添加游戏”，为常玩的游戏设置运行与退出预设。程序位置仅在编辑窗口中配置。", 9.5f);
            hint.ForeColor = Theme.Muted;
            hint.Dock = DockStyle.Fill;
            hint.Margin = Padding.Empty;
            empty.Controls.Add(title, 0, 0);
            empty.Controls.Add(hint, 0, 1);
            cards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cards.Controls.Add(empty, 0, 0);
            ScaleNewCard(empty);
        }
        else
        {
            for (int index = 0; index < draft.Rules.Count; index++)
            {
                var card = CreateCard(draft.Rules[index], index);
                cards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                cards.Controls.Add(card, 0, index);
                ScaleNewCard(card);
            }
        }
        cards.ResumeLayout(true);
        Theme.StyleInputs(cards);
        UpdateLibraryStatus();
        cardsViewport.AutoScrollPosition = new Point(0, scrollY);
        if (focusId.HasValue)
        {
            var target = cards.Controls.Find($"EditRule_{focusId.Value:N}", true).FirstOrDefault();
            if (target is not null)
            {
                cardsViewport.ScrollControlIntoView(target);
                target.Focus();
            }
        }
    }

    private void ScaleNewCard(Control card)
    {
        if (IsHandleCreated && DeviceDpi != 96)
            card.Scale(new SizeF(DeviceDpi / 96f, DeviceDpi / 96f));
    }

    private Control CreateCard(AutoSwitchRule rule, int index)
    {
        string id = rule.Id.ToString("N");
        var card = new TableLayoutPanel
        {
            Name = $"Rule_{id}",
            AccessibleName = $"优先级 {index + 1}，{rule.Name}，{(rule.Enabled ? "已启用" : "已暂停")}",
            AccessibleRole = AccessibleRole.Grouping,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 4,
            BackColor = Theme.Surface,
            Padding = new Padding(14, 12, 14, 10),
            Margin = new Padding(0, 0, 0, 10),
            Cursor = Cursors.Hand
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int row = 0; row < card.RowCount; row++)
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.Click += (_, _) => EditRule(rule.Id);
        var name = Theme.Label(rule.Name, 12, true);
        name.Name = $"Rule_{id}_Name";
        name.Dock = DockStyle.Fill;
        name.Margin = new Padding(0, 0, 12, 5);
        name.Click += (_, _) => EditRule(rule.Id);
        card.Controls.Add(name, 0, 0);
        var toggle = new DarkCheckBox
        {
            Name = $"ToggleRule_{id}",
            Text = rule.Enabled ? "已启用" : "已暂停",
            AccessibleName = $"启用游戏卡片 {rule.Name}",
            AutoSize = true,
            Checked = rule.Enabled,
            Margin = new Padding(0, 0, 0, 5),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        toggle.CheckedChanged += (_, _) =>
        {
            rule.Enabled = toggle.Checked;
            RebuildCards(rule.Id);
        };
        card.Controls.Add(toggle, 1, 0);
        bool needsConfiguration = AutoSwitchRuleEditorForm.ValidateRule(rule, presets, out _) is not null;
        var priority = Theme.Label($"优先级 {index + 1}" + (needsConfiguration ? "  ·  待配置，请点击编辑" : "  ·  配置完整"), 8.5f);
        priority.Name = $"Rule_{id}_Priority";
        priority.ForeColor = needsConfiguration ? Theme.Accent : Theme.Muted;
        priority.Dock = DockStyle.Fill;
        priority.Margin = new Padding(0, 0, 0, 7);
        priority.Click += (_, _) => EditRule(rule.Id);
        card.Controls.Add(priority, 0, 1);
        card.SetColumnSpan(priority, 2);
        var choices = Theme.Label($"运行  →  {PresetName(rule.RunningPresetId)}\n退出  →  {PresetName(rule.StoppedPresetId)}", 9);
        choices.Name = $"Rule_{id}_Presets";
        choices.ForeColor = Theme.Muted;
        choices.Dock = DockStyle.Fill;
        choices.Margin = new Padding(0, 0, 0, 10);
        choices.Click += (_, _) => EditRule(rule.Id);
        card.Controls.Add(choices, 0, 2);
        card.SetColumnSpan(choices, 2);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty
        };
        actions.Click += (_, _) => EditRule(rule.Id);
        AddCardAction(actions, "编辑卡片", $"EditRule_{id}", $"编辑游戏卡片 {rule.Name}", () => EditRule(rule.Id));
        AddCardAction(actions, "上移 ↑", $"MoveRuleUp_{id}", $"提高 {rule.Name} 的优先级", () => MoveRule(rule.Id, -1), index > 0);
        AddCardAction(actions, "下移 ↓", $"MoveRuleDown_{id}", $"降低 {rule.Name} 的优先级", () => MoveRule(rule.Id, 1), index < draft.Rules.Count - 1);
        AddCardAction(actions, "删除", $"DeleteRule_{id}", $"删除游戏卡片 {rule.Name}", () => DeleteRule(rule.Id));
        card.Controls.Add(actions, 0, 3);
        card.SetColumnSpan(actions, 2);
        return card;
    }

    private static void AddCardAction(FlowLayoutPanel actions, string text, string name, string accessibleName, Action action, bool enabled = true)
    {
        var button = Theme.Button(text);
        button.Name = name;
        button.AccessibleName = accessibleName;
        button.Width = 94;
        button.Enabled = enabled;
        button.Margin = new Padding(0, 0, 8, 2);
        button.Click += (_, _) => action();
        actions.Controls.Add(button);
    }

    private string PresetName(Guid? id) => !id.HasValue
        ? "未选择预设"
        : presets.FirstOrDefault(preset => preset.Id == id.Value)?.Name ?? "已删除的预设（需重新选择）";

    private void AddRule()
    {
        var rule = new AutoSwitchRule
        {
            RunningPresetId = presets.ElementAtOrDefault(1)?.Id ?? presets.FirstOrDefault()?.Id,
            StoppedPresetId = presets.FirstOrDefault()?.Id
        };
        using var editor = new AutoSwitchRuleEditorForm(presets, rule);
        if (editor.ShowDialog(this) != DialogResult.OK)
            return;
        draft.Rules.Add(editor.Result);
        RebuildCards(rule.Id);
    }

    private void EditRule(Guid id)
    {
        int index = draft.Rules.FindIndex(rule => rule.Id == id);
        if (index < 0)
            return;
        using var editor = new AutoSwitchRuleEditorForm(presets, draft.Rules[index]);
        if (editor.ShowDialog(this) != DialogResult.OK)
            return;
        draft.Rules[index] = editor.Result;
        RebuildCards(id);
    }

    private void DeleteRule(Guid id)
    {
        var rule = draft.Rules.FirstOrDefault(item => item.Id == id);
        if (rule is null || MessageBox.Show(this, $"确定删除游戏卡片“{rule.Name}”吗？\n保存设置后才会生效。", "删除游戏卡片",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;
        draft.Rules.Remove(rule);
        RebuildCards();
    }

    private void MoveRule(Guid id, int offset)
    {
        int index = draft.Rules.FindIndex(rule => rule.Id == id);
        int destination = index + offset;
        if (index < 0 || destination < 0 || destination >= draft.Rules.Count)
            return;
        var rule = draft.Rules[index];
        draft.Rules.RemoveAt(index);
        draft.Rules.Insert(destination, rule);
        RebuildCards(id);
    }

    public string? CollectSettings(out AutoSwitchSettings settings)
    {
        settings = draft.Copy();
        settings.Enabled = enabledBox.Checked;
        if (!settings.Enabled)
            return null;
        foreach (var rule in settings.Rules.Where(rule => rule.Enabled))
        {
            var error = AutoSwitchRuleEditorForm.ValidateRule(rule, presets, out var normalizedPath);
            if (error is not null)
            {
                var card = cards.Controls.Find($"EditRule_{rule.Id:N}", true).FirstOrDefault();
                if (card is not null)
                {
                    cardsViewport.ScrollControlIntoView(card);
                    card.Focus();
                }
                return $"游戏卡片“{rule.Name}”需要配置：{error}\n请编辑此卡片，或暂停卡片 / 关闭总开关后保存。";
            }
            rule.ExecutablePath = normalizedPath;
        }
        return null;
    }
}
