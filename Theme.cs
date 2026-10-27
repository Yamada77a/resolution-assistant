using System.Drawing.Drawing2D;

namespace ResolutionAssistant;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(243, 246, 250);
    public static readonly Color Surface = Color.White;
    public static readonly Color Ink = Color.FromArgb(27, 39, 59);
    public static readonly Color Muted = Color.FromArgb(103, 117, 137);
    public static readonly Color Accent = Color.FromArgb(46, 91, 232);
    public static readonly Color Border = Color.FromArgb(222, 228, 238);
    public static Font Font(float size = 10, FontStyle style = FontStyle.Regular) => new("Microsoft YaHei UI", size, style);
    public static void Apply(Form form)
    {
        form.Font = Font();
        form.BackColor = Background;
        form.ForeColor = Ink;
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.StartPosition = FormStartPosition.CenterScreen;
    }
    public static Button Button(string text, bool primary = false)
    {
        var b = new Button { Text = text, AutoSize = false, Height = 38, Width = 112,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = Font(9.5f),
            BackColor = primary ? Accent : Surface, ForeColor = primary ? Color.White : Ink };
        b.FlatAppearance.BorderSize = primary ? 0 : 1;
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(34, 74, 207) : Color.FromArgb(234, 239, 250);
        return b;
    }
    public static Label Label(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, AutoSize = true, Font = Font(size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = Ink
    };
    public static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var background = new SolidBrush(Accent);
        g.FillRoundedRectangle(background, new Rectangle(0, 0, 32, 32), 7);
        using var pen = new Pen(Color.White, 2);
        g.DrawRectangle(pen, 6, 7, 20, 14);
        g.DrawLine(pen, 16, 22, 16, 26);
        g.DrawLine(pen, 11, 26, 21, 26);
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }
    private static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle r, int radius)
    {
        using var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
