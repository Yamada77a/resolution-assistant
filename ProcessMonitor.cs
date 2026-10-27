using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ResolutionAssistant;

internal sealed record ProcessObservation(bool? IsRunning, string Message);

internal static class ProcessDetector
{
    private const uint SnapshotProcesses = 0x00000002;
    private const uint QueryLimitedInformation = 0x00001000;
    private const uint StillActive = 259;
    private const int ErrorNoMoreFiles = 18;
    private const int ErrorInvalidParameter = 87;
    private const int ImagePathCapacity = 32768;

    public static ProcessObservation Check(string executablePath)
    {
        string fullPath;
        string fileName;
        try
        {
            fullPath = Path.GetFullPath(executablePath);
            fileName = Path.GetFileName(fullPath);
            if (fileName.Length == 0) return Unknown("可执行文件路径无效");
        }
        catch (Exception exception) when (IsPathError(exception))
        {
            return Unknown($"可执行文件路径无效：{exception.Message}");
        }

        using var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
        if (snapshot.IsInvalid)
            return NativeError("无法获取进程列表", Marshal.GetLastWin32Error());

        var entry = new ProcessEntry32
        {
            Size = (uint)Marshal.SizeOf<ProcessEntry32>(),
            ExeFile = string.Empty
        };
        if (!Process32First(snapshot, ref entry))
        {
            var error = Marshal.GetLastWin32Error();
            return error == ErrorNoMoreFiles
                ? Waiting(fileName)
                : NativeError("无法读取进程列表", error);
        }

        StringBuilder? imagePath = null;
        string? uncertainty = null;
        do
        {
            // Only same-named processes need a handle or an image-path query.
            if (!string.Equals(entry.ExeFile, fileName, StringComparison.OrdinalIgnoreCase)) continue;
            var matches = MatchesPath(entry.ProcessId, fullPath, ref imagePath, out var error);
            if (matches == true)
                return new(true, $"自动切换：程序正在运行（{fileName}）");
            if (matches is null) uncertainty ??= error;
        }
        while (Process32Next(snapshot, ref entry));

        var enumerationError = Marshal.GetLastWin32Error();
        if (enumerationError != ErrorNoMoreFiles)
            return NativeError("进程列表读取不完整", enumerationError);
        return uncertainty is null ? Waiting(fileName) : Unknown(uncertainty);
    }

    private static bool? MatchesPath(uint processId, string fullPath, ref StringBuilder? imagePath, out string? error)
    {
        error = null;
        using var process = OpenProcess(QueryLimitedInformation, false, processId);
        if (process.IsInvalid)
        {
            var nativeError = Marshal.GetLastWin32Error();
            // A snapshot can outlive a process. An inaccessible live candidate cannot be ignored.
            if (nativeError == ErrorInvalidParameter) return false;
            error = $"无法读取同名进程的路径：{new Win32Exception(nativeError).Message}";
            return null;
        }

        imagePath ??= new StringBuilder(ImagePathCapacity);
        imagePath.Clear();
        var length = (uint)imagePath.Capacity;
        if (!QueryFullProcessImageName(process, 0, imagePath, ref length))
        {
            var nativeError = Marshal.GetLastWin32Error();
            if (GetExitCodeProcess(process, out var exitCode) && exitCode != StillActive) return false;
            error = $"无法读取同名进程的路径：{new Win32Exception(nativeError).Message}";
            return null;
        }

        try
        {
            var candidatePath = Path.GetFullPath(imagePath.ToString());
            return string.Equals(candidatePath, fullPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (IsPathError(exception))
        {
            error = $"同名进程的路径无法识别：{exception.Message}";
            return null;
        }
    }

    private static bool IsPathError(Exception exception) =>
        exception is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException or SecurityException;

    private static ProcessObservation Waiting(string fileName) =>
        new(false, $"自动切换：等待程序启动（{fileName}）");

    private static ProcessObservation Unknown(string reason) =>
        new(null, $"自动切换：检测异常，暂不切换（{reason}）");

    private static ProcessObservation NativeError(string reason, int error) =>
        Unknown($"{reason}：{new Win32Exception(error).Message}");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public UIntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    private sealed class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeKernelHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern SafeKernelHandle CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(SafeKernelHandle snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(SafeKernelHandle snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern SafeKernelHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeKernelHandle process, uint flags, StringBuilder imagePath, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(SafeKernelHandle process, out uint exitCode);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

internal sealed class ProcessMonitor : IDisposable
{
    private readonly Action<ProcessObservation, bool?> report;
    private readonly SynchronizationContext uiContext;
    private readonly int uiThreadId;
    private readonly System.Windows.Forms.Timer timer;
    private AutoSwitchRule? rule;
    private ProcessObservation? lastObservation;
    private bool? lastKnownRunning;
    private bool scanInProgress;
    private int generation;
    private int disposed;

    // Construct on the WinForms UI thread; calls from other threads are marshalled back here.
    public ProcessMonitor(Action<ProcessObservation, bool?> report)
    {
        ArgumentNullException.ThrowIfNull(report);
        this.report = report;
        uiThreadId = Environment.CurrentManagedThreadId;
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) => StartScan();
    }

    public void Configure(AutoSwitchRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (Volatile.Read(ref disposed) != 0) return;
        var copy = rule.Copy();
        if (Environment.CurrentManagedThreadId != uiThreadId)
        {
            uiContext.Post(_ => ConfigureOnUi(copy), null);
            return;
        }
        ConfigureOnUi(copy);
    }

    private void ConfigureOnUi(AutoSwitchRule next)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        if (rule is not null && rule.Enabled == next.Enabled &&
            string.Equals(rule.ExecutablePath, next.ExecutablePath, StringComparison.OrdinalIgnoreCase) &&
            rule.RunningPresetId == next.RunningPresetId && rule.StoppedPresetId == next.StoppedPresetId)
            return;

        rule = next;
        generation++;
        lastKnownRunning = null;
        lastObservation = null;
        timer.Stop();
        if (!next.Enabled)
        {
            Publish(new(null, "自动切换：已关闭"), null);
            return;
        }

        Publish(new(null, "自动切换：等待检测程序状态…"), null);
        timer.Start();
        StartScan();
    }

    private async void StartScan()
    {
        if (Volatile.Read(ref disposed) != 0 || scanInProgress || rule is not { Enabled: true }) return;
        scanInProgress = true;
        var scanGeneration = generation;
        var path = rule.ExecutablePath;
        ProcessObservation observation;
        try
        {
            observation = await Task.Run(() => ProcessDetector.Check(path)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            observation = new(null, $"自动切换：检测异常，暂不切换（{exception.Message}）");
        }
        if (Volatile.Read(ref disposed) != 0) return;
        try
        {
            uiContext.Post(_ => CompleteScan(scanGeneration, observation), null);
        }
        catch (InvalidAsynchronousStateException) when (Volatile.Read(ref disposed) != 0)
        {
            // The UI was destroyed between the disposal check and posting the result.
        }
        catch (InvalidOperationException) when (Volatile.Read(ref disposed) != 0)
        {
            // No result needs to be delivered after the monitor has been disposed.
        }
    }

    private void CompleteScan(int scanGeneration, ProcessObservation observation)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        scanInProgress = false;
        if (scanGeneration != generation)
        {
            // A changed rule waits for the old scan instead of overlapping it.
            StartScan();
            return;
        }
        if (rule is not { Enabled: true }) return;

        bool? switchToRunning = null;
        if (observation.IsRunning is bool running)
        {
            if (running && lastKnownRunning != true) switchToRunning = true;
            else if (!running && lastKnownRunning == true) switchToRunning = false;
            lastKnownRunning = running;
        }
        Publish(observation, switchToRunning);
    }

    private void Publish(ProcessObservation observation, bool? switchToRunning)
    {
        if (observation == lastObservation && switchToRunning is null) return;
        lastObservation = observation;
        report(observation, switchToRunning);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        if (Environment.CurrentManagedThreadId != uiThreadId)
        {
            uiContext.Post(_ => timer.Dispose(), null);
            return;
        }
        timer.Dispose();
    }
}
