namespace ResolutionAssistant;

internal sealed class MainForm : DarkForm
{
    private AppConfig config;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip trayMenu = new();
    private readonly Label current = Theme.Label("正在读取显示设置…", 23, true);
    private readonly Label status = Theme.Label("就绪 · 选择预设即可切换", 9);
    private readonly Label automationStatus = Theme.Label("自动切换：未启用", 9);
    private readonly Label count = Theme.Label("", 11, true);
    private readonly FlowLayoutPanel cards = new();
    private readonly Button applySelected = Theme.Button("应用所选预设", true);
    private readonly Label selectedSummary = Theme.Label("选择一个预设开始", 10, true);
    private readonly Label ruleTarget = Theme.Label("", 10, true);
    private readonly Label runningRule = Theme.Label("", 10, true);
    private readonly Label stoppedRule = Theme.Label("", 10, true);
    private readonly Label ruleBadge = Theme.Label("", 8);
    private readonly ToolTip hints = new();
    private Guid? selectedPresetId;
    private readonly HotkeyService hotkeys;
    private readonly ProcessMonitor processMonitor;
    private bool exiting;
    private bool switching;
    private bool settingsOpen;
    private bool initialBackground;
    private bool resourcesDisposed;
    private readonly Icon appIcon;

    public MainForm(AppConfig config, bool background)
    {
        this.config = config;
        initialBackground = background;
        Theme.Apply(this);
        Text = "分辨率助手";
        Name = "MainForm";
        AccessibleName = "分辨率助手";
        ClientSize = new Size(1160, 790);
        MinimumSize = new Size(980, 700);
        appIcon = Theme.CreateIcon();
        Icon = appIcon;
        BuildLayout();
        tray = new NotifyIcon { Text = "分辨率助手 · 快速切换", Icon = appIcon, ContextMenuStrip = trayMenu, Visible = true };
        Theme.StyleMenu(trayMenu);
        tray.DoubleClick += (_, _) => ShowWindow();
        hotkeys = new HotkeyService(Handle);
        var error = hotkeys.Apply(config.Presets);
        RenderPresets();
        RefreshCurrent();
        if (error != null) SetStatus(error, true);
        FormClosing += (_, e) =>
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };
        Shown += (_, _) =>
        {
            if (initialBackground) { initialBackground = false; Hide(); }
        };
        Activated += (_, _) => RefreshCurrent();
        processMonitor = new ProcessMonitor(OnProcessObservation);
        processMonitor.Configure(config.AutoSwitch);
    }

    protected override void SetVisibleCore(bool value)
    {
        if (initialBackground && value) { _ = Handle; base.SetVisibleCore(false); return; }
        base.SetVisibleCore(value);
    }

    private void BuildLayout()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 174));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Content.Controls.Add(shell);

        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = Color.FromArgb(11, 17, 18), Padding = new Padding(18, 26, 18, 18),
            ColumnCount = 1, RowCount = 6, Margin = Padding.Empty
        };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        for (int i = 0; i < 3; i++) sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        shell.Controls.Add(sidebar, 0, 0);
        var brand = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        var mark = Theme.Label("R /", 27, true);
        mark.ForeColor = Theme.Accent;
        brand.Controls.Add(mark);
        brand.Controls.Add(Theme.Label("分辨率助手", 13, true));
        var englishBrand = Theme.Label("RESOLUTION STUDIO", 7);
        englishBrand.ForeColor = Theme.Muted;
        brand.Controls.Add(englishBrand);
        sidebar.Controls.Add(brand, 0, 0);
        var home = Theme.Button("◈   控制中心");
        home.Dock = DockStyle.Fill;
        home.BackColor = Theme.AccentSoft;
        home.ForeColor = Theme.Accent;
        home.Click += (_, _) => ShowWindow();
        sidebar.Controls.Add(home, 0, 1);
        var settings = Theme.Button("▤   预设管理");
        settings.Name = "OpenSettings";
        settings.AccessibleName = "设置与预设";
        settings.Dock = DockStyle.Fill;
        settings.Click += (_, _) => OpenSettings();
        sidebar.Controls.Add(settings, 0, 2);
        var automatic = Theme.Button("◇   自动切换");
        automatic.Name = "OpenAutomation";
        automatic.AccessibleName = "程序自动切换设置";
        automatic.Dock = DockStyle.Fill;
        automatic.Click += (_, _) => OpenSettings(tab: 1);
        sidebar.Controls.Add(automatic, 0, 3);
        var version = Theme.Label($"本地运行 · 后台常驻\nv{Application.ProductVersion.Split('+')[0]}", 8);
        version.ForeColor = Theme.Muted;
        version.Dock = DockStyle.Fill;
        version.TextAlign = ContentAlignment.BottomLeft;
        sidebar.Controls.Add(version, 0, 5);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 18, 26, 16), ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        shell.Controls.Add(root, 1, 0);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        var title = Theme.Label("显示控制中心", 22, true);
        title.Margin = Padding.Empty;
        header.Controls.Add(title, 0, 0);
        var subtitle = Theme.Label("办公与游戏，自由切换你的显示节奏。", 9);
        subtitle.ForeColor = Theme.Muted;
        header.Controls.Add(subtitle, 0, 1);
        var refresh = Theme.Button("刷新状态");
        refresh.Name = "RefreshDisplay";
        refresh.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        refresh.Click += (_, _) => { RefreshCurrent(); SetStatus("已读取当前显示设置"); };
        header.Controls.Add(refresh, 1, 0);
        header.SetRowSpan(refresh, 2);
        root.Controls.Add(header, 0, 0);

        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 262));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(workspace, 0, 1);
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 216));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        workspace.Controls.Add(left, 0, 0);
        var hero = new ModeHero { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(24, 26, 20, 20) };
        var heroContent = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent };
        heroContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heroContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        heroContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        heroContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        heroContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var modeTitle = Theme.Label("当前显示模式  /  PRIMARY DISPLAY", 8);
        modeTitle.ForeColor = Theme.Muted;
        heroContent.Controls.Add(modeTitle, 0, 0);
        current.AutoSize = false;
        current.Dock = DockStyle.Fill;
        current.Name = "CurrentMode";
        current.AutoEllipsis = true;
        heroContent.Controls.Add(current, 0, 1);
        var heroHint = Theme.Label("用合适的比例，进入你的节奏。", 9);
        heroHint.ForeColor = Theme.Muted;
        heroContent.Controls.Add(heroHint, 0, 2);
        var modeScope = Theme.Label("分辨率 / 刷新率     ·     当前会话生效", 8);
        modeScope.ForeColor = Theme.Success;
        modeScope.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        heroContent.Controls.Add(modeScope, 0, 3);
        hero.Controls.Add(heroContent);
        left.Controls.Add(hero, 0, 0);
        var section = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 17, 0, 0), Margin = Padding.Empty };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        count.Margin = Padding.Empty;
        section.Controls.Add(count, 0, 0);
        var add = Theme.Button("＋ 新建预设");
        add.Height = 30;
        add.Width = 100;
        add.Name = "NewPreset";
        add.AccessibleName = "新建预设";
        add.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        add.Click += (_, _) => OpenSettings(true);
        section.Controls.Add(add, 1, 0);
        left.Controls.Add(section, 0, 1);
        cards.Dock = DockStyle.Fill;
        cards.WrapContents = true;
        cards.AutoScroll = true;
        cards.Margin = Padding.Empty;
        cards.Resize += (_, _) => ResizeCards();
        Theme.StyleNativeScrollbars(cards);
        left.Controls.Add(cards, 0, 2);

        var monitor = SurfacePanel(new Padding(18));
        monitor.Margin = new Padding(18, 0, 0, 0);
        workspace.Controls.Add(monitor, 1, 0);
        var monitorLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 12 };
        monitorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int[] heights = [32, 44, 72, 16, 22, 38, 22, 38, 48, 20];
        foreach (int height in heights) monitorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        monitorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        monitorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        var monitorHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        monitorHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        monitorHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        monitorHeader.Controls.Add(Theme.Label("程序自动切换", 11, true), 0, 0);
        ruleBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        monitorHeader.Controls.Add(ruleBadge, 1, 0);
        monitorLayout.Controls.Add(monitorHeader, 0, 0);
        SetPanelLabel(ruleTarget);
        monitorLayout.Controls.Add(ruleTarget, 0, 1);
        SetPanelLabel(automationStatus);
        automationStatus.Name = "AutomationStatus";
        monitorLayout.Controls.Add(automationStatus, 0, 2);
        monitorLayout.Controls.Add(Divider(), 0, 3);
        var runCaption = Theme.Label("程序运行时应用", 8);
        runCaption.ForeColor = Theme.Muted;
        monitorLayout.Controls.Add(runCaption, 0, 4);
        SetPanelLabel(runningRule);
        monitorLayout.Controls.Add(runningRule, 0, 5);
        var stopCaption = Theme.Label("全部退出后恢复", 8);
        stopCaption.ForeColor = Theme.Muted;
        monitorLayout.Controls.Add(stopCaption, 0, 6);
        SetPanelLabel(stoppedRule);
        monitorLayout.Controls.Add(stoppedRule, 0, 7);
        var configure = Theme.Button("配置自动切换  ›");
        configure.Dock = DockStyle.Fill;
        configure.Name = "ConfigureAutomation";
        configure.Click += (_, _) => OpenSettings(tab: 1);
        monitorLayout.Controls.Add(configure, 0, 8);
        monitorLayout.Controls.Add(Divider(), 0, 9);
        var monitorHint = Theme.Label("只在程序状态变化时切换\n手动操作不会被持续覆盖", 8.5f);
        monitorHint.ForeColor = Theme.Muted;
        monitorHint.Dock = DockStyle.Fill;
        monitorHint.AutoSize = false;
        monitorLayout.Controls.Add(monitorHint, 0, 10);
        var local = Theme.Label("WINDOWS / 主显示器", 7.5f);
        local.ForeColor = Theme.Muted;
        monitorLayout.Controls.Add(local, 0, 11);
        monitor.Controls.Add(monitorLayout);

        var actionBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 18, 0, 0), Margin = Padding.Empty };
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
        actionBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        actionBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        selectedSummary.AutoSize = false;
        selectedSummary.Dock = DockStyle.Fill;
        selectedSummary.AutoEllipsis = true;
        actionBar.Controls.Add(selectedSummary, 0, 0);
        status.AutoSize = false;
        status.Dock = DockStyle.Fill;
        status.AutoEllipsis = true;
        status.Name = "Status";
        actionBar.Controls.Add(status, 0, 1);
        applySelected.Name = "ApplySelected";
        applySelected.AccessibleName = "应用所选预设";
        applySelected.Size = new Size(214, 52);
        applySelected.Font = Theme.Font(12, FontStyle.Bold);
        applySelected.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        applySelected.Click += (_, _) =>
        {
            var preset = config.Presets.FirstOrDefault(p => p.Id == selectedPresetId);
            if (preset != null) Switch(preset);
        };
        actionBar.Controls.Add(applySelected, 1, 0);
        actionBar.SetRowSpan(applySelected, 2);
        root.Controls.Add(actionBar, 0, 2);
    }

    private static void SetPanelLabel(Label label)
    {
        label.AutoSize = false;
        label.Dock = DockStyle.Fill;
        label.AutoEllipsis = true;
    }

    private static Panel Divider() => new() { BackColor = Theme.Border, Height = 1, Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 0) };

    private static Panel SurfacePanel(Padding padding)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = padding };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        return panel;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ResizeCards();
    }

    private void RenderPresets()
    {
        cards.SuspendLayout();
        while (cards.Controls.Count > 0) cards.Controls[0].Dispose();
        count.Text = $"我的预设  /  {config.Presets.Count:00}";
        if (config.Presets.Count == 0)
        {
            var empty = Theme.Label("还没有预设\n点击「新建预设」，保存你的第一个显示模式。", 11);
            empty.Padding = new Padding(12, 24, 0, 0);
            cards.Controls.Add(empty);
        }
        if (!config.Presets.Any(p => p.Id == selectedPresetId)) selectedPresetId = config.Presets.FirstOrDefault()?.Id;
        for (int index = 0; index < config.Presets.Count; index++)
        {
            var preset = config.Presets[index];
            var card = new PresetTile(preset, index);
            card.Click += (_, _) => Switch(preset);
            cards.Controls.Add(card);
        }
        UpdateSelection();
        UpdateRuleSummary();
        ResizeCards();
        cards.ResumeLayout();
        trayMenu.Items.Clear();
        trayMenu.Items.Add("打开分辨率助手", null, (_, _) => ShowWindow());
        trayMenu.Items.Add(new ToolStripSeparator());
        foreach (var preset in config.Presets)
        {
            var item = trayMenu.Items.Add($"{preset.Name}  ·  {preset.ModeLabel}", null, (_, _) => Switch(preset));
            if (item is ToolStripMenuItem menu && preset.VirtualKey != 0) menu.ShortcutKeyDisplayString = preset.ShortcutLabel;
        }
        if (config.Presets.Count == 0) trayMenu.Items.Add(new ToolStripMenuItem("暂无预设") { Enabled = false });
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("设置与预设", null, (_, _) => OpenSettings());
        trayMenu.Items.Add("退出", null, (_, _) => { exiting = true; Close(); });
    }

    private void ResizeCards()
    {
        int gap = Math.Max(8, 12 * DeviceDpi / 96);
        int columns = cards.ClientSize.Width >= 550 * DeviceDpi / 96 ? 2 : 1;
        int width = Math.Max(180, (cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth) / columns - gap);
        foreach (Control control in cards.Controls)
        {
            if (control is not PresetTile) continue;
            control.Size = new Size(width, 166 * DeviceDpi / 96);
            control.Margin = new Padding(0, 0, gap, gap);
        }
    }

    private void UpdateSelection()
    {
        foreach (Control control in cards.Controls)
            if (control is PresetTile tile) tile.Selected = tile.PresetId == selectedPresetId;
        var preset = config.Presets.FirstOrDefault(p => p.Id == selectedPresetId);
        applySelected.Enabled = preset != null;
        selectedSummary.Text = preset is null ? "还没有预设 · 点击新建开始" : $"{preset.Name}  /  {preset.ModeLabel}";
        hints.SetToolTip(selectedSummary, selectedSummary.Text);
    }

    private void UpdateRuleSummary()
    {
        var rule = config.AutoSwitch;
        ruleBadge.Text = rule.Enabled ? "已启用" : "未启用";
        ruleBadge.ForeColor = rule.Enabled ? Theme.Success : Theme.Muted;
        ruleTarget.Text = string.IsNullOrWhiteSpace(rule.ExecutablePath) ? "尚未选择程序" : Path.GetFileName(rule.ExecutablePath);
        hints.SetToolTip(ruleTarget, rule.ExecutablePath);
        runningRule.Text = config.Presets.FirstOrDefault(p => p.Id == rule.RunningPresetId)?.Name ?? "未选择预设";
        stoppedRule.Text = config.Presets.FirstOrDefault(p => p.Id == rule.StoppedPresetId)?.Name ?? "未选择预设";
        hints.SetToolTip(runningRule, runningRule.Text);
        hints.SetToolTip(stoppedRule, stoppedRule.Text);
    }

    private void Switch(Preset preset)
    {
        if (switching) return;
        switching = true;
        selectedPresetId = preset.Id;
        UpdateSelection();
        try
        {
            var result = DisplayService.Apply(preset);
            RefreshCurrent();
            SetStatus($"{preset.Name}：{result.Message}", !result.Success);
            if (!Visible)
            {
                tray.BalloonTipTitle = result.Success ? "显示模式已切换" : "未能切换显示模式";
                tray.BalloonTipText = $"{preset.Name}\n{result.Message}";
                tray.BalloonTipIcon = result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning;
                tray.ShowBalloonTip(3500);
            }
        }
        catch (Exception ex) { SetStatus($"切换失败：{ex.Message}", true); }
        finally { switching = false; }
    }

    private void RefreshCurrent()
    {
        try { current.Text = DisplayService.CurrentMode(); }
        catch (Exception ex) { current.Text = "无法读取显示模式"; SetStatus(ex.Message, true); }
    }

    private void SetStatus(string text, bool error = false)
    {
        status.Text = text;
        status.ForeColor = error ? Theme.Error : Theme.Muted;
        hints.SetToolTip(status, text);
    }

    private void OnProcessObservation(ProcessObservation observation, bool? switchToRunning)
    {
        if (automationStatus.Text != observation.Message) automationStatus.Text = observation.Message;
        automationStatus.ForeColor = config.AutoSwitch.Enabled && observation.IsRunning is null
            ? Theme.Error : observation.IsRunning == true ? Theme.Success : Theme.Muted;
        hints.SetToolTip(automationStatus, observation.Message);
        if (switchToRunning is null) return;
        var presetId = switchToRunning.Value ? config.AutoSwitch.RunningPresetId : config.AutoSwitch.StoppedPresetId;
        var preset = config.Presets.FirstOrDefault(p => p.Id == presetId);
        if (preset is null)
        {
            SetStatus("自动切换失败：所选预设已不存在，请在设置中重新选择。", true);
            return;
        }
        Switch(preset);
    }

    private void OpenSettings(bool add = false, int tab = 0)
    {
        if (settingsOpen) return;
        ShowWindow();
        settingsOpen = true;
        try
        {
            using var dialog = new SettingsForm(config, StartupService.IsEnabled(), SaveSettings, tab);
            if (add)
            {
                dialog.Shown += (_, _) =>
                {
                    var button = dialog.Controls.Find("AddPreset", true).OfType<Button>().FirstOrDefault();
                    button?.PerformClick();
                };
            }
            dialog.ShowDialog(this);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "设置", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { settingsOpen = false; }
    }

    private string? SaveSettings(AppConfig draft, bool autoStart)
    {
        var error = hotkeys.Apply(draft.Presets);
        if (error != null) return error;
        bool previousStartup;
        try { previousStartup = StartupService.IsEnabled(); }
        catch (Exception ex)
        {
            var rollback = hotkeys.Apply(config.Presets);
            return $"无法读取开机自启设置：{ex.Message}" + (rollback is null ? "" : $"\n{rollback}");
        }
        try
        {
            StartupService.SetEnabled(autoStart);
            ConfigStore.Save(draft);
        }
        catch (Exception ex)
        {
            string message = $"保存失败：{ex.Message}";
            try { StartupService.SetEnabled(previousStartup); }
            catch (Exception rollback) { message += $"\n恢复开机自启设置失败：{rollback.Message}"; }
            var rollbackError = hotkeys.Apply(config.Presets);
            if (rollbackError != null) message += $"\n{rollbackError}";
            return message;
        }
        config = draft.Copy();
        RenderPresets();
        SetStatus("设置已保存 · 预设、快捷键与程序检测已同步");
        processMonitor.Configure(config.AutoSwitch);
        return null;
    }

    public void ShowWindow()
    {
        initialBackground = false;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && hotkeys != null && hotkeys.TryGetPreset(m.WParam.ToInt32(), out var id))
        {
            var preset = config.Presets.FirstOrDefault(p => p.Id == id);
            if (preset != null) Switch(preset);
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Release HWND-bound hotkeys before Form destroys its native window.
        processMonitor.Dispose();
        hotkeys.Dispose();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !resourcesDisposed)
        {
            processMonitor?.Dispose();
            hotkeys?.Dispose();
            tray.Visible = false;
            tray.Dispose();
            trayMenu.Dispose();
            appIcon.Dispose();
            hints.Dispose();
            resourcesDisposed = true;
        }
        base.Dispose(disposing);
    }
}

