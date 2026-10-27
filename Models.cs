namespace ResolutionAssistant;

public sealed class Preset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新预设";
    public uint Width { get; set; } = 2560;
    public uint Height { get; set; } = 1440;
    public uint RefreshRate { get; set; } = 240;
    public uint Modifiers { get; set; }
    public uint VirtualKey { get; set; }
    public Preset Copy() => (Preset)MemberwiseClone();
    public string ModeLabel => $"{Width} × {Height}  ·  {RefreshRate} Hz";
    public string ShortcutLabel
    {
        get
        {
            if (VirtualKey == 0) return "未设置";
            var parts = new List<string>();
            if ((Modifiers & 2) != 0) parts.Add("Ctrl");
            if ((Modifiers & 1) != 0) parts.Add("Alt");
            if ((Modifiers & 4) != 0) parts.Add("Shift");
            if ((Modifiers & 8) != 0) parts.Add("Win");
            var key = (Keys)VirtualKey;
            parts.Add(key >= Keys.D0 && key <= Keys.D9 ? ((char)VirtualKey).ToString() : key.ToString());
            return string.Join(" + ", parts);
        }
    }
}

public sealed class AutoSwitchRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新游戏";
    public bool Enabled { get; set; } = true;
    public string ExecutablePath { get; set; } = "";
    public Guid? RunningPresetId { get; set; }
    public Guid? StoppedPresetId { get; set; }
    public AutoSwitchRule Copy() => (AutoSwitchRule)MemberwiseClone();
}

public sealed class AutoSwitchSettings
{
    public bool Enabled { get; set; }
    public List<AutoSwitchRule> Rules { get; set; } = [];
    public AutoSwitchSettings Copy() => new()
    {
        Enabled = Enabled,
        Rules = Rules.Select(rule => rule.Copy()).ToList()
    };
}

public sealed class AppConfig
{
    public int Version { get; set; } = 2;
    public List<Preset> Presets { get; set; } = [];
    public AutoSwitchSettings AutoSwitch { get; set; } = new();
    public AppConfig Copy() => new()
    {
        Version = Version,
        Presets = Presets.Select(p => p.Copy()).ToList(),
        AutoSwitch = AutoSwitch.Copy()
    };
    public static AppConfig Defaults() => new()
    {
        Presets = [
            new() { Name = "宽屏 16:9", Width = 2560, Height = 1440, RefreshRate = 240 },
            new() { Name = "标准 4:3", Width = 2048, Height = 1536, RefreshRate = 240 },
            new() { Name = "低分辨率 4:3", Width = 1920, Height = 1440, RefreshRate = 240 }
        ]
    };
}
