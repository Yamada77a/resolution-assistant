using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ResolutionAssistant;

internal sealed class ModeHero : Panel
{
    private Bitmap? _background;
    private int _backgroundDpi;

    public ModeHero()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
        Size = new Size(760, 210);
        TabStop = false;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        ClearBackground();
        base.OnSizeChanged(e);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        ClearBackground();
        base.OnDpiChangedAfterParent(e);
        Invalidate(true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        if (_background is null || _backgroundDpi != DeviceDpi)
        {
            ClearBackground();
            _background = CreateBackground();
            _backgroundDpi = DeviceDpi;
        }
        e.Graphics.DrawImageUnscaled(_background, Point.Empty);
    }

    private Bitmap CreateBackground()
    {
        var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
        bitmap.SetResolution(DeviceDpi, DeviceDpi);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(Point.Empty, ClientSize);
        using (var ground = new LinearGradientBrush(bounds,
            Color.FromArgb(20, 34, 35), Color.FromArgb(14, 24, 26), 12f))
            g.FillRectangle(ground, bounds);

        // The scene stays anchored to the right, leaving the mode readout quiet.
        var scale = DeviceDpi / 96f;
        var sceneScale = Math.Min(ClientSize.Height / 210f, scale * 1.3f);
        g.TranslateTransform(ClientSize.Width - 310f * sceneScale,
            (ClientSize.Height - 210f * sceneScale) / 2f);
        g.ScaleTransform(sceneScale, sceneScale);

        using (var glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(8, -38, 350, 290);
            using var glow = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(64, 62, 113, 105),
                SurroundColors = [Color.FromArgb(0, 32, 58, 56)]
            };
            g.FillPath(glow, glowPath);
        }

        using var grid = new Pen(Color.FromArgb(17, 117, 158, 146), .7f);
        for (var x = -30; x < 360; x += 30)
            g.DrawLine(grid, x, 0, x, 210);
        for (var y = 0; y < 240; y += 30)
            g.DrawLine(grid, -30, y, 360, y);
        using var ray = new Pen(Color.FromArgb(19, 115, 159, 146), .8f);
        for (var i = 0; i < 6; i++)
            g.DrawLine(ray, -100, 210, 150 + i * 47, -20);

        DrawScreen(g, 126, 29, 139, 89, Color.FromArgb(32, 112, 151, 140));
        DrawScreen(g, 105, 47, 153, 98, Color.FromArgb(47, 112, 151, 140));
        DrawScreen(g, 76, 68, 168, 104, Color.FromArgb(103, 124, 168, 153));
        using (var screen = new LinearGradientBrush(new Rectangle(83, 76, 151, 79),
            Color.FromArgb(30, 68, 109, 98), Color.FromArgb(3, 50, 81, 75), 135f))
        {
            PointF[] points = [new(84, 76), new(234, 63), new(234, 142), new(84, 155)];
            g.FillPolygon(screen, points);
        }
        using var stand = new Pen(Color.FromArgb(92, 111, 149, 137), 1.2f);
        g.DrawLines(stand, [new PointF(148, 165), new PointF(148, 181), new PointF(130, 191), new PointF(191, 186)]);
        g.DrawLine(stand, 166, 163, 166, 178);
        using var amber = new Pen(Color.FromArgb(155, Theme.Accent), 1.5f);
        g.DrawLine(amber, 220, 153, 233, 152);
        g.DrawLine(amber, 254, 32, 264, 31);
        using var detail = new Pen(Color.FromArgb(52, 143, 181, 161), 1f);
        g.DrawLines(detail, [new PointF(199, 195), new PointF(242, 191), new PointF(262, 176), new PointF(290, 174)]);
        g.DrawLine(detail, 266, 58, 279, 57);
        g.DrawLine(detail, 279, 57, 279, 83);
        g.ResetTransform();

        // A dark scrim also protects long labels on narrower layouts.
        using (var shade = new LinearGradientBrush(bounds, Color.FromArgb(14, 24, 26),
            Color.FromArgb(0, 14, 24, 26), 0f))
        {
            shade.InterpolationColors = new ColorBlend
            {
                Positions = [0f, .54f, .82f, 1f],
                Colors = [Color.FromArgb(245, 14, 24, 26), Color.FromArgb(225, 14, 24, 26),
                    Color.FromArgb(90, 14, 24, 26), Color.FromArgb(10, 14, 24, 26)]
            };
            g.FillRectangle(shade, bounds);
        }
        using var border = new Pen(Theme.Border, Math.Max(1f, scale));
        var inset = border.Width / 2f;
        g.DrawRectangle(border, inset, inset, Math.Max(0, ClientSize.Width - border.Width),
            Math.Max(0, ClientSize.Height - border.Width));
        return bitmap;
    }

    private static void DrawScreen(Graphics g, float x, float y, float width, float height, Color color)
    {
        using var pen = new Pen(color, 1.2f);
        PointF[] corners = [new(x, y), new(x + width, y - 13),
            new(x + width, y + height - 13), new(x, y + height), new(x, y)];
        g.DrawLines(pen, corners);
        g.DrawLine(pen, x, y + height - 13, x + width, y + height - 26);
    }

    private void ClearBackground()
    {
        _background?.Dispose();
        _background = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ClearBackground();
        base.Dispose(disposing);
    }
}

internal sealed class PresetTile : Button
{
    private const TextFormatFlags TextFlags = TextFormatFlags.NoPadding |
        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter |
        TextFormatFlags.NoPrefix;
    private readonly string _presetName;
    private readonly string _ordinal;
    private readonly string _mode;
    private readonly string _refresh;
    private readonly string _ratio;
    private readonly string _shortcut;
    private readonly SolidBrush _surface = new(Theme.Surface);
    private readonly SolidBrush _raised = new(Theme.Raised);
    private readonly SolidBrush _selectedSurface = new(Color.FromArgb(31, 37, 32));
    private readonly SolidBrush _pressedSurface = new(Color.FromArgb(19, 27, 27));
    private readonly SolidBrush _accent = new(Theme.Accent);
    private readonly SolidBrush _chip = new(Theme.AccentSoft);
    private readonly Pen _border = new(Theme.Border);
    private readonly Pen _activeBorder = new(Theme.Accent);
    private readonly Pen _hoverBorder = new(Color.FromArgb(93, 117, 111));
    private readonly Pen _geometry = new(Color.FromArgb(47, 67, 64));
    private readonly Pen _geometrySelected = new(Color.FromArgb(76, 78, 57));
    private readonly Pen _focus = new(Theme.Ink) { DashStyle = DashStyle.Dot };
    private Font? _nameFont;
    private Font? _modeFont;
    private Font? _smallFont;
    private Font? _ordinalFont;
    private int _fontDpi;
    private bool _selected;
    private bool _hovered;
    private bool _mousePressed;
    private bool _keyPressed;

    public Guid PresetId { get; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            AccessibleDescription = value
                ? "已选择的预设，不表示当前系统显示模式。点击切换到此预设。"
                : "点击切换到此预设。";
            Invalidate();
        }
    }

    public PresetTile(Preset preset, int index)
    {
        ArgumentNullException.ThrowIfNull(preset);
        PresetId = preset.Id;
        _presetName = preset.Name;
        _ordinal = ((long)index + 1).ToString("D2");
        _mode = $"{preset.Width} × {preset.Height}";
        _refresh = $"{preset.RefreshRate} Hz";
        var divisor = GreatestCommonDivisor(preset.Width, preset.Height);
        _ratio = preset.Width == 0 || preset.Height == 0 ? "自定义比例" : $"{preset.Width / divisor}:{preset.Height / divisor}";
        _shortcut = preset.VirtualKey == 0 ? "点击即切换" : preset.ShortcutLabel;
        Name = $"Preset_{preset.Id:N}";
        Text = _presetName;
        AccessibleName = $"{_presetName}，{preset.Width} × {preset.Height}，{preset.RefreshRate} Hz，比例 {_ratio}，{_shortcut}";
        AccessibleDescription = "点击切换到此预设。";
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UseVisualStyleBackColor = false;
        UseMnemonic = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.Surface;
        ForeColor = Theme.Ink;
        Cursor = Cursors.Hand;
        Size = new Size(290, 166);
        Margin = new Padding(0, 0, 12, 12);
        EnsureFonts();
    }

    private static uint GreatestCommonDivisor(uint a, uint b)
    {
        while (b != 0)
        {
            var remainder = a % b;
            a = b;
            b = remainder;
        }
        return a;
    }

    private void EnsureFonts()
    {
        if (_fontDpi == DeviceDpi && _nameFont is not null) return;
        DisposeFonts();
        var scale = DeviceDpi / 96f;
        _nameFont = new Font("Microsoft YaHei UI", 14f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        _modeFont = new Font("Microsoft YaHei UI", 24f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        _smallFont = new Font("Microsoft YaHei UI", 11f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        _ordinalFont = new Font("Microsoft YaHei UI", 11f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        _fontDpi = DeviceDpi;
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        EnsureFonts();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        EnsureFonts();
        var g = e.Graphics;
        var scale = DeviceDpi / 96f;
        var width = ClientSize.Width;
        var height = ClientSize.Height;
        if (width <= 0 || height <= 0) return;
        var pressed = Enabled && ((_mousePressed && _hovered) || _keyPressed);
        var hovered = Enabled && _hovered;
        var fill = pressed ? _pressedSurface : _selected ? _selectedSurface : hovered ? _raised : _surface;
        g.FillRectangle(fill, ClientRectangle);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var outline = _selected && Enabled ? _activeBorder : hovered ? _hoverBorder : _border;
        outline.Width = scale;
        var inset = scale / 2f;
        g.DrawRectangle(outline, inset, inset, Math.Max(0, width - scale), Math.Max(0, height - scale));
        if (_selected)
            g.FillRectangle(Enabled ? _accent : _chip, 0, 0, 3 * scale, height);

        var geometry = _selected ? _geometrySelected : _geometry;
        geometry.Width = scale;
        var monitorX = width - 75 * scale;
        var monitorY = height - 77 * scale;
        g.DrawRectangle(geometry, monitorX, monitorY, 52 * scale, 32 * scale);
        g.DrawRectangle(geometry, monitorX + 7 * scale, monitorY - 7 * scale, 52 * scale, 32 * scale);
        g.DrawLine(geometry, monitorX + 26 * scale, monitorY + 32 * scale,
            monitorX + 26 * scale, monitorY + 41 * scale);
        g.DrawLine(geometry, monitorX + 15 * scale, monitorY + 41 * scale,
            monitorX + 37 * scale, monitorY + 41 * scale);

        var ink = Enabled ? Theme.Ink : Theme.Muted;
        var quiet = Enabled ? Theme.Muted : Color.FromArgb(94, 111, 107);
        var accent = Enabled ? Theme.Accent : Theme.Muted;
        int S(float value) => (int)Math.Round(value * scale);
        void Draw(string text, Font font, Color color, float x, float y, float w, float h) =>
            TextRenderer.DrawText(g, text, font,
                new Rectangle(S(x), S(y), Math.Max(0, S(w)), Math.Max(0, S(h))), color, TextFlags);
        var logicalWidth = width / scale;
        var logicalHeight = height / scale;
        Draw(_ordinal, _ordinalFont!, quiet, 18, 13, Math.Max(0, logicalWidth - 112), 18);
        if (_selected)
        {
            var chipBounds = new Rectangle(S(logicalWidth - 74), S(10), S(58), S(22));
            g.FillRectangle(_chip, chipBounds);
            TextRenderer.DrawText(g, "已选择", _smallFont!, chipBounds, accent,
                TextFlags | TextFormatFlags.HorizontalCenter);
        }
        Draw(_presetName, _nameFont!, ink, 18, 38, logicalWidth - 36, 24);
        Draw(_mode, _modeFont!, ink, 17, 66, logicalWidth - 34, 34);

        // Fixed-width metadata slots keep extreme uint values from colliding.
        var badgeWidth = Math.Min(116f, Math.Max(40f, (logicalWidth - 48) * .57f));
        var badge = new Rectangle(S(18), S(107), S(badgeWidth), S(23));
        g.FillRectangle(_selected ? _chip : _raised, badge);
        TextRenderer.DrawText(g, _refresh, _smallFont!, badge, _selected ? accent : ink,
            TextFlags | TextFormatFlags.HorizontalCenter);
        Draw(_ratio, _smallFont!, quiet, 26 + badgeWidth, 107,
            logicalWidth - badgeWidth - 44, 23);
        Draw(_shortcut, _smallFont!, quiet, 18, logicalHeight - 28, logicalWidth - 96, 18);

        if (Focused && ShowFocusCues)
        {
            _focus.Width = scale;
            g.DrawRectangle(_focus, 5 * scale, 5 * scale,
                Math.Max(0, width - 10 * scale), Math.Max(0, height - 10 * scale));
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) _mousePressed = true;
        base.OnMouseDown(e);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) _mousePressed = false;
        base.OnMouseUp(e);
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture) _mousePressed = false;
        base.OnMouseCaptureChanged(e);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) _keyPressed = true;
        base.OnKeyDown(e);
        Invalidate();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) _keyPressed = false;
        base.OnKeyUp(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        _keyPressed = false;
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled) _mousePressed = _keyPressed = false;
        base.OnEnabledChanged(e);
        Invalidate();
    }

    private void DisposeFonts()
    {
        _nameFont?.Dispose();
        _modeFont?.Dispose();
        _smallFont?.Dispose();
        _ordinalFont?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeFonts();
            _surface.Dispose();
            _raised.Dispose();
            _selectedSurface.Dispose();
            _pressedSurface.Dispose();
            _accent.Dispose();
            _chip.Dispose();
            _border.Dispose();
            _activeBorder.Dispose();
            _hoverBorder.Dispose();
            _geometry.Dispose();
            _geometrySelected.Dispose();
            _focus.Dispose();
        }
        base.Dispose(disposing);
    }
}
