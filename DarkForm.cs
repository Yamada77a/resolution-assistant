using System.Runtime.InteropServices;

namespace ResolutionAssistant;

internal class DarkForm : Form
{
    public Panel Content { get; }

    public DarkForm()
    {
        DoubleBuffered = true;
        BackColor = Theme.Background;
        Content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Margin = Padding.Empty };
        Controls.Add(Content);
    }

    protected override void OnLoad(EventArgs e)
    {
        // All layouts use 96-DPI coordinates. Scale after derived forms have added their controls.
        AutoScaleDimensions = new SizeF(96, 96);
        PerformAutoScale();
        base.OnLoad(e);
        var area = Screen.FromControl(this).WorkingArea;
        var available = new Size(Math.Max(1, area.Width - 24), Math.Max(1, area.Height - 24));
        MinimumSize = new Size(Math.Min(MinimumSize.Width, available.Width), Math.Min(MinimumSize.Height, available.Height));
        Size = new Size(Math.Min(Width, available.Width), Math.Min(Height, available.Height));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
        var dark = 1;
        SetWindowAttribute(20, dark);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            SetWindowAttribute(33, 2);
            SetWindowAttribute(34, ColorRef(Theme.Border));
            SetWindowAttribute(35, ColorRef(Theme.Surface));
            SetWindowAttribute(36, ColorRef(Theme.Ink));
        }
    }

    private static int ColorRef(Color color) => color.R | color.G << 8 | color.B << 16;
    private void SetWindowAttribute(int attribute, int value) =>
        DwmSetWindowAttribute(Handle, attribute, ref value, sizeof(int));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
