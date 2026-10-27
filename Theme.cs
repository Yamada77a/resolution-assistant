using System.Drawing.Drawing2D;

namespace ResolutionAssistant;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(15, 21, 22);
    public static readonly Color Surface = Color.FromArgb(22, 31, 32);
    public static readonly Color Raised = Color.FromArgb(30, 41, 42);
    public static readonly Color Ink = Color.FromArgb(234, 238, 232);
    public static readonly Color Muted = Color.FromArgb(139, 158, 155);
    public static readonly Color Accent = Color.FromArgb(255, 190, 78);
    public static readonly Color AccentSoft = Color.FromArgb(66, 52, 30);
    public static readonly Color Border = Color.FromArgb(50, 67, 66);
    public static readonly Color Success = Color.FromArgb(105, 201, 162);
    public static readonly Color Error = Color.FromArgb(242, 132, 118);
    public static readonly Color WarningSurface = Color.FromArgb(47, 38, 25);
    public static Font Font(float size = 10, FontStyle style = FontStyle.Regular) => new("Microsoft YaHei UI", size, style);
    public static void Apply(Form form)
    {
        form.Font = Font();
        form.BackColor = Background;
        form.ForeColor = Ink;
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.StartPosition = FormStartPosition.CenterScreen;
    }
    public static Button Button(string text, bool primary = false) => new AccentButton(primary)
    {
        Text = text, Height = 38, Width = 112, Cursor = Cursors.Hand, Font = Font(9.5f)
    };
    public static Label Label(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, AutoSize = true, UseMnemonic = false, Font = Font(size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = Ink,
        BackColor = Color.Transparent
    };
    public static void StyleInputs(Control root) => DarkControls.ApplyTree(root);
    public static void StyleMenu(ContextMenuStrip menu)
    {
        menu.Renderer = new DarkMenuRenderer();
        menu.BackColor = Surface;
        menu.ForeColor = Ink;
        menu.Font = Font(9.5f);
    }
    public static void StyleNativeScrollbars(Control control)
    {
        control.HandleCreated += (_, _) => SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        if (control.IsHandleCreated) SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
    }
    public static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var background = new SolidBrush(Background);
        using var path = new GraphicsPath();
        path.AddArc(0, 0, 14, 14, 180, 90);
        path.AddArc(18, 0, 14, 14, 270, 90);
        path.AddArc(18, 18, 14, 14, 0, 90);
        path.AddArc(0, 18, 14, 14, 90, 90);
        path.CloseFigure();
        g.FillPath(background, path);
        using var pen = new Pen(Accent, 2);
        g.DrawRectangle(pen, 6, 7, 20, 14);
        g.DrawLine(pen, 16, 22, 16, 26);
        g.DrawLine(pen, 11, 26, 21, 26);
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string appName, string? idList);
}
