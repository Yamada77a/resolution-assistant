using System.Drawing.Drawing2D;

namespace ResolutionAssistant;

internal sealed class AccentButton : Button
{
    private bool hovered;
    private bool pressed;
    public bool Primary { get; }

    public AccentButton(bool primary = false)
    {
        Primary = primary;
        BackColor = Theme.Surface;
        ForeColor = Theme.Ink;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) pressed = false; Invalidate(); base.OnMouseCaptureChanged(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) pressed = true; Invalidate(); base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { if (e.KeyCode == Keys.Space) pressed = false; Invalidate(); base.OnKeyUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        var bounds = new RectangleF(.5f, .5f, Width - 1, Height - 1);
        using var path = RoundedRectangle(bounds, Math.Min(6 * scale, Math.Min(Width, Height) / 2f));
        Color fill = Enabled ? BackColor : Theme.Surface;
        if (Primary && Enabled)
        {
            Color top = pressed ? Color.FromArgb(216, 151, 49) : hovered ? Color.FromArgb(255, 213, 132) : Color.FromArgb(255, 204, 112);
            Color bottom = pressed ? Color.FromArgb(197, 133, 38) : hovered ? Color.FromArgb(255, 190, 78) : Color.FromArgb(237, 166, 57);
            using var brush = new LinearGradientBrush(bounds, top, bottom, LinearGradientMode.Vertical);
            e.Graphics.FillPath(brush, path);
        }
        else
        {
            if (Enabled && (pressed || hovered)) fill = ControlPaint.Light(fill, pressed ? .08f : .15f);
            using var brush = new SolidBrush(fill);
            e.Graphics.FillPath(brush, path);
        }
        using var outline = new Pen(Enabled && (Primary || hovered || Focused) ? Theme.Accent : Theme.Border, Math.Max(1, scale));
        e.Graphics.DrawPath(outline, path);
        var textBounds = Rectangle.Inflate(ClientRectangle, -(int)(10 * scale), -(int)(3 * scale));
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
        else if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
        if (RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Enabled ? Primary ? Theme.Background : ForeColor : Theme.Muted, flags);
        if (Focused && ShowFocusCues)
        {
            var focus = Rectangle.Inflate(ClientRectangle, -(int)(4 * scale), -(int)(4 * scale));
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, Primary ? Theme.Background : Theme.Accent, fill);
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
    {
        float diameter = Math.Max(1, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class DarkTabControl : TabControl
{
    public DarkTabControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Padding = new Point(20, 10);
        BackColor = Theme.Background;
        ForeColor = Theme.Ink;
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is TabPage page)
        {
            page.UseVisualStyleBackColor = false;
            page.BackColor = Theme.Background;
            page.ForeColor = Theme.Ink;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Background);
        var frame = DisplayRectangle;
        frame.Inflate(1, 1);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, frame);
        for (int index = 0; index < TabCount; index++)
            DrawTab(e.Graphics, index, GetTabRect(index));
        base.OnPaint(e);
    }

    private void DrawTab(Graphics graphics, int index, Rectangle bounds)
    {
        bool selected = SelectedIndex == index;
        using var background = new SolidBrush(selected ? Theme.Surface : Theme.Background);
        graphics.FillRectangle(background, bounds);
        TextRenderer.DrawText(graphics, TabPages[index].Text, Font, bounds,
            Enabled ? selected ? Theme.Accent : Theme.Muted : Theme.Border,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        if (selected)
        {
            using var accent = new SolidBrush(Theme.Accent);
            int thickness = Math.Max(2, (int)Math.Round(2d * DeviceDpi / 96));
            graphics.FillRectangle(accent, bounds.Left + 8, bounds.Bottom - thickness, Math.Max(0, bounds.Width - 16), thickness);
        }
        if (Focused && selected && ShowFocusCues) ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(bounds, -4, -4), Theme.Accent, Theme.Surface);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }
}

internal sealed class DarkComboBox : ComboBox
{
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is not (0xF or 0x317 or 0x318) || !IsHandleCreated || Width < 2 || Height < 2) return;
        using var graphics = m.Msg == 0xF ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(m.WParam);
        var info = new ComboBoxInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<ComboBoxInfo>() };
        if (GetComboBoxInfo(Handle, ref info))
        {
            var button = Rectangle.FromLTRB(info.Button.Left, info.Button.Top, info.Button.Right, info.Button.Bottom);
            button.Inflate(1, 0);
            using var fill = new SolidBrush(Theme.Raised);
            graphics.FillRectangle(fill, button);
            int x = button.Left + button.Width / 2;
            int y = button.Top + button.Height / 2;
            int radius = Math.Max(3, 3 * DeviceDpi / 96);
            using var arrow = new SolidBrush(Enabled && Focused ? Theme.Accent : Theme.Muted);
            graphics.FillPolygon(arrow, [new Point(x - radius, y - 1), new Point(x + radius, y - 1), new Point(x, y + radius - 1)]);
        }
        using var border = new Pen(Enabled && Focused ? Theme.Accent : Theme.Border);
        graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ComboBoxInfo
    {
        public int Size;
        public NativeRect Item, Button;
        public int ButtonState;
        public IntPtr Combo, Edit, List;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetComboBoxInfo(IntPtr window, ref ComboBoxInfo info);
}

internal sealed class DarkCheckBox : CheckBox
{
    public DarkCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        float scale = DeviceDpi / 96f;
        var text = TextRenderer.MeasureText(Text, Font, Size.Empty,
            TextFormatFlags.SingleLine | (!UseMnemonic ? TextFormatFlags.NoPrefix : 0));
        int side = (int)Math.Round(14 * scale);
        int gap = (int)Math.Round(7 * scale);
        return new Size(side + gap + text.Width + 2, Math.Max(side + 2, text.Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        float scale = DeviceDpi / 96f;
        int side = (int)Math.Round(14 * scale);
        int gap = (int)Math.Round(7 * scale);
        bool rtl = RightToLeft == RightToLeft.Yes;
        var box = new Rectangle(rtl ? Width - side - 1 : 1, (Height - side) / 2, side, side);
        using var fill = new SolidBrush(Checked ? Theme.AccentSoft : Theme.Raised);
        e.Graphics.FillRectangle(fill, box);
        using var outline = new Pen(Enabled && Focused ? Theme.Accent : Theme.Muted);
        e.Graphics.DrawRectangle(outline, box);
        using var mark = new Pen(Enabled ? Theme.Accent : Theme.Muted, Math.Max(1.5f, 1.5f * scale));
        if (CheckState == CheckState.Indeterminate)
            e.Graphics.DrawLine(mark, box.Left + 3 * scale, box.Top + side / 2f, box.Right - 3 * scale, box.Top + side / 2f);
        else if (Checked)
            e.Graphics.DrawLines(mark, [new PointF(box.Left + 3 * scale, box.Top + side * .5f), new PointF(box.Left + side * .43f, box.Bottom - 3 * scale), new PointF(box.Right - 3 * scale, box.Top + 3 * scale)]);
        var text = new Rectangle(rtl ? 0 : box.Right + gap, 0, Math.Max(0, Width - side - gap - 2), Height);
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        flags |= !UseMnemonic ? TextFormatFlags.NoPrefix : !ShowKeyboardCues ? TextFormatFlags.HidePrefix : 0;
        if (rtl) flags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
        TextRenderer.DrawText(e.Graphics, Text, Font, text, Enabled ? ForeColor : Theme.Muted, flags);
        if (Enabled && Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, text, Theme.Accent, BackColor);
    }
}

internal static class DarkControls
{
    public static void ApplyTree(Control root)
    {
        switch (root)
        {
            case TextBoxBase text:
                text.BackColor = Theme.Raised;
                text.ForeColor = Theme.Ink;
                text.BorderStyle = BorderStyle.FixedSingle;
                break;
            case CheckBox check:
                check.FlatStyle = FlatStyle.Flat;
                check.FlatAppearance.BorderColor = Theme.Border;
                check.FlatAppearance.CheckedBackColor = Theme.AccentSoft;
                check.ForeColor = Theme.Ink;
                check.BackColor = Theme.Background;
                break;
            case ComboBox combo:
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = Theme.Raised;
                combo.ForeColor = Theme.Ink;
                if (combo.DrawMode != DrawMode.OwnerDrawFixed)
                {
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem += DrawComboItem;
                }
                combo.ItemHeight = Math.Max(combo.ItemHeight, combo.Font.Height + (int)Math.Round(6d * combo.DeviceDpi / 96));
                break;
            case TabPage page:
                page.UseVisualStyleBackColor = false;
                page.BackColor = Theme.Background;
                page.ForeColor = Theme.Ink;
                break;
        }
        foreach (Control child in root.Controls) ApplyTree(child);
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color background = selected ? Theme.AccentSoft : Theme.Raised;
        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, e.Bounds);
        string text = e.Index >= 0 && e.Index < combo.Items.Count ? combo.Items[e.Index]?.ToString() ?? string.Empty : combo.Text;
        var bounds = Rectangle.Inflate(e.Bounds, -4, 0);
        TextRenderer.DrawText(e.Graphics, text, e.Font ?? combo.Font, bounds,
            combo.Enabled ? selected ? Theme.Accent : Theme.Ink : Theme.Muted,
            TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | (combo.RightToLeft == RightToLeft.Yes ? TextFormatFlags.RightToLeft : 0));
        if ((e.State & DrawItemState.Focus) != 0) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, Theme.Accent, background);
    }
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkMenuColors()) { RoundedEdges = false; }
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Theme.Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Theme.Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        using var brush = new SolidBrush(e.Item.Selected && e.Item.Enabled ? Theme.AccentSoft : Theme.Surface);
        e.Graphics.FillRectangle(brush, bounds);
        if (e.Item.Selected && e.Item.Enabled)
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));
        }
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = !e.Item.Enabled ? Theme.Muted : e.Item.Selected ? Theme.Accent : Theme.Ink;
        base.OnRenderItemText(e);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == false ? Theme.Muted : Theme.Ink;
        base.OnRenderArrow(e);
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Border);
        int y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 10, y, Math.Max(10, e.Item.Width - 10), y);
    }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, e.ToolStrip.Width - 1), Math.Max(0, e.ToolStrip.Height - 1));
    }
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var rect = e.ImageRectangle;
        using var pen = new Pen(e.Item.Enabled ? Theme.Accent : Theme.Muted, Math.Max(1.5f, e.Graphics.DpiX / 64f));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(pen, [new PointF(rect.Left + rect.Width * .18f, rect.Top + rect.Height * .5f), new PointF(rect.Left + rect.Width * .42f, rect.Top + rect.Height * .74f), new PointF(rect.Left + rect.Width * .83f, rect.Top + rect.Height * .25f)]);
    }
    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Theme.Border;
        public override Color MenuItemSelected => Theme.AccentSoft;
        public override Color MenuItemSelectedGradientBegin => Theme.AccentSoft;
        public override Color MenuItemSelectedGradientEnd => Theme.AccentSoft;
        public override Color MenuItemPressedGradientBegin => Theme.AccentSoft;
        public override Color MenuItemPressedGradientEnd => Theme.AccentSoft;
        public override Color CheckBackground => Theme.AccentSoft;
        public override Color CheckSelectedBackground => Theme.AccentSoft;
        public override Color CheckPressedBackground => Theme.AccentSoft;
        public override Color ImageMarginGradientBegin => Theme.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Surface;
        public override Color ImageMarginGradientEnd => Theme.Surface;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }
}
