namespace ResolutionAssistant;

internal sealed class MainForm : Form
{
    private AppConfig config;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip trayMenu = new();
    private readonly Label current = Theme.Label("正在读取显示设置…", 19, true);
    private readonly Label status = Theme.Label("就绪 · 选择预设即可切换", 9);
    private readonly Label count = Theme.Label("", 11, true);
    private readonly FlowLayoutPanel cards = new();
    private readonly HotkeyService hotkeys;
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
        ClientSize = new Size(880, 650);
        MinimumSize = new Size(680, 540);
        appIcon = Theme.CreateIcon();
        Icon = appIcon;
        BuildLayout();
        tray = new NotifyIcon { Text = "分辨率助手 · 快速切换", Icon = appIcon, ContextMenuStrip = trayMenu, Visible = true };
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
    }

    protected override void SetVisibleCore(bool value)
    {
        if (initialBackground && value) { _ = Handle; base.SetVisibleCore(false); return; }
        base.SetVisibleCore(value);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 28, 16), ColumnCount = 1, RowCount = 6 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 114));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        var title = Theme.Label("分辨率助手", 23, true);
        title.Margin = Padding.Empty;
        header.Controls.Add(title, 0, 0);
        var subtitle = Theme.Label("让每一种使用场景，都有合适的显示模式。", 9);
        subtitle.ForeColor = Theme.Muted;
        header.Controls.Add(subtitle, 0, 1);
        var settings = Theme.Button("设置与预设");
        settings.Name = "OpenSettings";
        settings.AccessibleName = "设置与预设";
        settings.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        settings.Click += (_, _) => OpenSettings();
        header.Controls.Add(settings, 1, 0);
        header.SetRowSpan(settings, 2);
        root.Controls.Add(header, 0, 0);

        var active = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Ink, Padding = new Padding(22, 14, 22, 14), Margin = new Padding(0, 8, 0, 0) };
        var activeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2 };
        activeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        activeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        var activeTitle = Theme.Label("当前显示模式  /  主显示器", 9);
        activeTitle.ForeColor = Color.FromArgb(174, 191, 218);
        activeLayout.Controls.Add(activeTitle, 0, 0);
        current.ForeColor = Color.White;
        current.Name = "CurrentMode";
        current.AutoSize = false;
        current.Dock = DockStyle.Fill;
        activeLayout.Controls.Add(current, 0, 1);
        var refresh = Theme.Button("刷新状态");
        refresh.Width = 100;
        refresh.Click += (_, _) => { RefreshCurrent(); SetStatus("已读取当前显示设置"); };
        activeLayout.Controls.Add(refresh, 1, 1);
        active.Controls.Add(activeLayout);
        root.Controls.Add(active, 0, 1);

        var section = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 20, 0, 0) };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        count.Margin = Padding.Empty;
        section.Controls.Add(count, 0, 0);
        var add = Theme.Button("＋ 新建预设", true);
        add.Height = 30;
        add.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        add.Name = "NewPreset";
        add.AccessibleName = "新建预设";
        add.Click += (_, _) => OpenSettings(true);
        section.Controls.Add(add, 1, 0);
        root.Controls.Add(section, 0, 2);
        cards.Dock = DockStyle.Fill;
        cards.FlowDirection = FlowDirection.LeftToRight;
        cards.WrapContents = true;
        cards.AutoScroll = true;
        cards.Margin = Padding.Empty;
        cards.Resize += (_, _) => ResizeCards();
        root.Controls.Add(cards, 0, 3);
        status.AutoSize = false;
        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.AutoEllipsis = true;
        status.Name = "Status";
        root.Controls.Add(status, 0, 4);
        var footer = Theme.Label("关闭窗口后继续驻留托盘  ·  自定义预设不受设备支持列表限制", 8.5f);
        footer.ForeColor = Theme.Muted;
        root.Controls.Add(footer, 0, 5);
    }

    private void RenderPresets()
    {
        cards.SuspendLayout();
        while (cards.Controls.Count > 0) cards.Controls[0].Dispose();
        count.Text = $"快速切换  ·  {config.Presets.Count} 个预设";
        if (config.Presets.Count == 0)
        {
            var empty = Theme.Label("还没有预设\n点击「新建预设」，保存你的第一个显示模式。", 11);
            empty.Padding = new Padding(12, 24, 0, 0);
            cards.Controls.Add(empty);
        }
        foreach (var preset in config.Presets)
        {
            var card = new PresetButton(preset) { Margin = new Padding(0, 0, 12, 12) };
            card.Click += (_, _) => Switch(preset);
            cards.Controls.Add(card);
        }
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
        int columns = cards.ClientSize.Width >= 620 ? 2 : 1;
        int width = Math.Max(230, (cards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth) / columns - 12);
        foreach (Control control in cards.Controls)
            if (control is PresetButton) control.Width = width;
    }

    private void Switch(Preset preset)
    {
        if (switching) return;
        switching = true;
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
        status.ForeColor = error ? Color.FromArgb(182, 57, 45) : Theme.Muted;
    }

    private void OpenSettings(bool add = false)
    {
        if (settingsOpen) return;
        ShowWindow();
        settingsOpen = true;
        try
        {
            using var dialog = new SettingsForm(config, StartupService.IsEnabled(), SaveSettings);
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
        SetStatus("设置已保存 · 预设、托盘菜单与全局快捷键已同步");
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
        hotkeys.Dispose();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !resourcesDisposed)
        {
            hotkeys?.Dispose();
            tray.Visible = false;
            tray.Dispose();
            trayMenu.Dispose();
            appIcon.Dispose();
            resourcesDisposed = true;
        }
        base.Dispose(disposing);
    }
}

internal sealed class PresetButton : Button
{
    private readonly Preset preset;
    private readonly Font titleFont = Theme.Font(11, FontStyle.Bold);
    private readonly Font modeFont = Theme.Font(13);
    private readonly Font subFont = Theme.Font(8.5f);
    private readonly Pen normalBorder = new(Theme.Border);
    private readonly Pen activeBorder = new(Theme.Accent);
    private readonly string modeLabel;
    private readonly string shortcutLabel;
    private bool hover;
    public PresetButton(Preset preset)
    {
        this.preset = preset;
        modeLabel = preset.ModeLabel;
        shortcutLabel = preset.VirtualKey == 0 ? "点击切换  ·  可在设置中添加快捷键" : preset.ShortcutLabel;
        Height = 132;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.Surface;
        Cursor = Cursors.Hand;
        AccessibleName = $"切换到 {preset.Name}，{preset.ModeLabel}，快捷键 {preset.ShortcutLabel}";
        Name = $"Preset_{preset.Id:N}";
        Text = preset.Name;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(hover ? Color.FromArgb(235, 241, 255) : BackColor);
        e.Graphics.DrawRectangle(hover || Focused ? activeBorder : normalBorder, 0, 0, Width - 1, Height - 1);
        var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(e.Graphics, preset.Name, titleFont, new Rectangle(18, 15, Width - 55, 26), Theme.Ink, flags);
        TextRenderer.DrawText(e.Graphics, "↗", titleFont, new Rectangle(Width - 34, 15, 22, 24), Theme.Accent, flags);
        TextRenderer.DrawText(e.Graphics, modeLabel, modeFont, new Rectangle(18, 46, Width - 28, 30), Theme.Accent, flags);
        TextRenderer.DrawText(e.Graphics, shortcutLabel, subFont, new Rectangle(18, 94, Width - 28, 22), Theme.Muted, flags);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 8, Height - 8));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            titleFont.Dispose();
            modeFont.Dispose();
            subFont.Dispose();
            normalBorder.Dispose();
            activeBorder.Dispose();
        }
        base.Dispose(disposing);
    }
}
