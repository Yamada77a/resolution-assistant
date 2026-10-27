using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace ResolutionAssistant;

internal static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = true
    };

    public static string FilePath => Path.Combine(
        Environment.GetEnvironmentVariable("RESOLUTION_ASSISTANT_DATA_DIR") is { Length: > 0 } directory
            ? directory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ResolutionAssistant"),
        "settings.json");

    public static AppConfig Load()
    {
        var path = FilePath;
        if (!File.Exists(path))
        {
            var defaults = AppConfig.Defaults();
            Save(defaults);
            return defaults;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty(nameof(AppConfig.Presets), out var presets) ||
                presets.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("配置必须包含 Presets 预设数组。");

            RequireNamedIds(presets, "预设");
            bool legacyRule = false;
            if (root.TryGetProperty(nameof(AppConfig.AutoSwitch), out var automation))
            {
                if (automation.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("程序自动切换设置必须为对象。");
                if (automation.TryGetProperty(nameof(AutoSwitchSettings.Rules), out var rules))
                {
                    if (rules.ValueKind != JsonValueKind.Array)
                        throw new InvalidDataException("游戏检测规则必须为数组。");
                    RequireNamedIds(rules, "游戏检测规则");
                }
                else legacyRule = true;
            }

            var config = root.Deserialize<AppConfig>(JsonOptions)
                ?? throw new InvalidDataException("配置内容不能为 null。");
            if (legacyRule)
            {
                var previous = automation.Deserialize<AutoSwitchRule>(JsonOptions)
                    ?? throw new InvalidDataException("旧程序检测规则不能为 null。");
                if (previous.ExecutablePath is null)
                    throw new InvalidDataException("旧程序检测路径不能为 null。");
                if (previous.ExecutablePath.Length != 0 || previous.RunningPresetId.HasValue || previous.StoppedPresetId.HasValue)
                {
                    previous.Id = Guid.NewGuid();
                    var name = Path.GetFileNameWithoutExtension(previous.ExecutablePath);
                    previous.Name = string.IsNullOrWhiteSpace(name) ? "游戏 1" : name;
                    previous.Enabled = true;
                    config.AutoSwitch.Rules.Add(previous);
                }
            }
            config.Version = 2;
            Validate(config);
            return config;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or NotSupportedException)
        {
            throw new InvalidDataException($"无法读取配置文件“{path}”：{exception.Message} 原文件未被覆盖。", exception);
        }
    }

    public static void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Validate(config);
        var path = Path.GetFullPath(FilePath);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".settings.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, config, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void RequireNamedIds(JsonElement items, string label)
    {
        // Model initializers must not silently repair missing IDs in persisted lists.
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("Id", out var id) ||
                id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var value) || value == Guid.Empty ||
                !item.TryGetProperty("Name", out var name) || name.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"每个{label}必须具有非空有效 ID 和非 null 名称。");
        }
    }

    private static void Validate(AppConfig config)
    {
        if (config.Presets is null) throw new InvalidDataException("预设列表不能为 null。");
        if (config.AutoSwitch?.Rules is null)
            throw new InvalidDataException("程序自动切换设置及其规则列表不能为 null。");
        var ids = new HashSet<Guid>();
        foreach (var preset in config.Presets)
        {
            if (preset is null || preset.Name is null)
                throw new InvalidDataException("预设及其名称不能为 null。");
            if (preset.Id == Guid.Empty || !ids.Add(preset.Id))
                throw new InvalidDataException("预设 ID 必须非空且不能重复。");
        }
        ids.Clear();
        foreach (var rule in config.AutoSwitch.Rules)
        {
            if (rule is null || rule.Name is null || rule.ExecutablePath is null)
                throw new InvalidDataException("游戏规则、名称及其路径不能为 null。");
            if (rule.Id == Guid.Empty || !ids.Add(rule.Id))
                throw new InvalidDataException("游戏检测规则 ID 必须非空且不能重复。");
        }
    }
}

internal record DisplayResult(bool Success, string Message);

internal static class DisplayService
{
    private const uint PrimaryDevice = 0x00000004;
    private const uint ModeFields = 0x00080000 | 0x00100000 | 0x00400000;

    public static string CurrentMode()
    {
        if (!TryGetCurrent(out var device, out var mode, out var error)) return error;
        return $"{mode.PelsWidth} × {mode.PelsHeight}  ·  {mode.DisplayFrequency} Hz";
    }

    public static DisplayResult Apply(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (!TryGetCurrent(out var device, out var mode, out var error)) return new(false, error);
        mode.PelsWidth = preset.Width;
        mode.PelsHeight = preset.Height;
        mode.DisplayFrequency = preset.RefreshRate;
        mode.Fields = ModeFields;
        // Flags 0 changes this session only. Never test, persist, or restore a mode here.
        var result = ChangeDisplaySettingsEx(device.DeviceName, ref mode, IntPtr.Zero, 0, IntPtr.Zero);
        return result switch
        {
            0 => new(true, $"切换成功。{CurrentMode()}"),
            1 => new(false, "Windows 返回“需要重启”；当前会话未确认切换成功，本程序不会自动重启。"),
            -1 => new(false, "Windows 拒绝切换显示模式（驱动程序或显示设备错误）。"),
            -2 => new(false, "Windows 不接受此显示模式（分辨率或刷新率不受支持）。"),
            -3 => new(false, "Windows 无法更新显示设置（DISP_CHANGE_NOTUPDATED）。"),
            -4 => new(false, "Windows 拒绝了显示设置标志（DISP_CHANGE_BADFLAGS）。"),
            -5 => new(false, "Windows 拒绝了显示参数（DISP_CHANGE_BADPARAM）。"),
            -6 => new(false, "Windows 当前的双屏显示配置不允许此更改（DISP_CHANGE_BADDUALVIEW）。"),
            _ => new(false, $"Windows 切换显示模式失败，返回代码：{result}。")
        };
    }

    private static bool TryGetCurrent(out DisplayDevice device, out DevMode mode, out string error)
    {
        device = default;
        mode = default;
        error = "无法找到 Windows 主显示设备。";
        for (uint index = 0; ; index++)
        {
            device = new DisplayDevice { Size = (uint)Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, index, ref device, 0)) return false;
            if ((device.StateFlags & PrimaryDevice) != 0) break;
        }
        mode = new DevMode { Size = (ushort)Marshal.SizeOf<DevMode>() };
        if (!EnumDisplaySettings(device.DeviceName, -1, ref mode))
        {
            error = $"无法读取主显示设备“{device.DeviceString}”的当前显示模式。";
            return false;
        }
        error = string.Empty;
        return true;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    // DEVMODEW's display union occupies 16 bytes (POINTL + orientation + fixed output).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint ICMMethod;
        public uint ICMIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;
    }

    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice displayDevice, uint flags);

    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNumber, ref DevMode mode);

    [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int ChangeDisplaySettingsEx(string deviceName, ref DevMode mode, IntPtr window, uint flags, IntPtr parameter);
}

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ResolutionAssistant";
    private static string Command => $"\"{Environment.ProcessPath ?? throw new InvalidOperationException("无法确定当前程序的路径。 ")}\" --background";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string value &&
            string.Equals(value, Command, StringComparison.Ordinal);
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

internal sealed class HotkeyService : IDisposable
{
    private const uint NoRepeat = 0x4000;
    private const int MaximumId = 0xBFFF;
    private readonly IntPtr handle;
    private readonly Dictionary<int, Binding> active = [];
    private int nextId = 1;
    private bool disposed;

    private sealed record Binding(int Id, Guid PresetId, uint Modifiers, uint Key, string Name, string Shortcut);

    public HotkeyService(IntPtr handle)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("全局快捷键需要有效窗口句柄。", nameof(handle));
        this.handle = handle;
    }

    public string? Apply(IReadOnlyList<Preset> presets)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(presets);
        var combinations = new Dictionary<(uint Modifiers, uint Key), string>();
        var candidates = new List<Binding>();
        var reservedIds = new HashSet<int>(active.Keys);
        foreach (var preset in presets)
        {
            if ((preset.Modifiers & ~15u) != 0)
                return $"预设“{preset.Name}”的快捷键修饰键无效。";
            if (preset.VirtualKey == 0)
            {
                if (preset.Modifiers != 0) return $"预设“{preset.Name}”的快捷键缺少按键。";
                continue;
            }
            var key = preset.VirtualKey;
            if (key > 254 || !Enum.IsDefined((Keys)key) || key <= 6 ||
                key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0x7B or >= 0xA0 and <= 0xA5)
                return $"预设“{preset.Name}”的快捷键“{preset.ShortcutLabel}”含无效或保留按键（F12 为 Windows 调试器保留）。";
            if (!combinations.TryAdd((preset.Modifiers, key), preset.Name))
                return $"预设“{preset.Name}”与“{combinations[(preset.Modifiers, key)]}”使用相同快捷键：{preset.ShortcutLabel}。";
            if (reservedIds.Count >= MaximumId) return "全局快捷键数量超出 Windows 可用 ID 范围。";
            while (reservedIds.Contains(nextId)) nextId = nextId == MaximumId ? 1 : nextId + 1;
            var id = nextId;
            reservedIds.Add(id);
            nextId = nextId == MaximumId ? 1 : nextId + 1;
            candidates.Add(new(id, preset.Id, preset.Modifiers, key, preset.Name, preset.ShortcutLabel));
        }

        // Snapshot values rather than holding mutable Preset objects. A later Apply(oldPresets)
        // can roll back a successful registration if saving the new configuration fails.
        var previous = active.Values.ToArray();
        var removalErrors = Remove(previous);
        if (removalErrors.Count != 0)
            return FailureWithRestore("无法注销原快捷键：" + string.Join("；", removalErrors), previous);

        foreach (var binding in candidates)
        {
            if (TryRegister(binding, out var error)) continue;
            var cleanupErrors = Remove(candidates);
            var message = $"无法注册预设“{binding.Name}”的快捷键“{binding.Shortcut}”：{error}。可能已被其他程序占用。";
            if (cleanupErrors.Count != 0) message += " 新快捷键清理失败：" + string.Join("；", cleanupErrors) + "。";
            return FailureWithRestore(message, previous);
        }
        return null;
    }

    public bool TryGetPreset(int id, out Guid presetId)
    {
        if (active.TryGetValue(id, out var binding))
        {
            presetId = binding.PresetId;
            return true;
        }
        presetId = Guid.Empty;
        return false;
    }

    private bool TryRegister(Binding binding, out string error)
    {
        if (RegisterHotKey(handle, binding.Id, binding.Modifiers | NoRepeat, binding.Key))
        {
            active.Add(binding.Id, binding);
            error = string.Empty;
            return true;
        }
        var code = Marshal.GetLastWin32Error();
        error = $"{new Win32Exception(code).Message}（错误 {code}）";
        return false;
    }

    private List<string> Remove(IEnumerable<Binding> bindings)
    {
        var errors = new List<string>();
        foreach (var binding in bindings)
        {
            if (!active.ContainsKey(binding.Id)) continue;
            if (UnregisterHotKey(handle, binding.Id)) active.Remove(binding.Id);
            else
            {
                var code = Marshal.GetLastWin32Error();
                errors.Add($"“{binding.Name}” / {binding.Shortcut}（错误 {code}）");
            }
        }
        return errors;
    }

    private string FailureWithRestore(string message, Binding[] previous)
    {
        var errors = new List<string>();
        foreach (var binding in previous)
        {
            if (active.ContainsKey(binding.Id)) continue;
            if (!TryRegister(binding, out var error)) errors.Add($"“{binding.Name}” / {binding.Shortcut}：{error}");
        }
        return errors.Count == 0
            ? message + " 原快捷键已保留或恢复。"
            : message + " 部分原快捷键恢复失败：" + string.Join("；", errors) + "。请重新保存快捷键设置或重启程序。";
    }

    public void Dispose()
    {
        if (disposed) return;
        var errors = Remove(active.Values.ToArray());
        // Keep failed bindings tracked so disposal can be retried before the window is destroyed.
        if (errors.Count != 0) throw new InvalidOperationException("无法释放全局快捷键：" + string.Join("；", errors));
        disposed = true;
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
