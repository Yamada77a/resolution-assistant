using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ResolutionAssistant;

internal sealed record AutomationObservation(bool? IsRunning, string Message, Guid? ActiveRuleId);
internal sealed record AutomationTransition(Guid RuleId, bool Running);
internal sealed record AutomationEvaluation(AutomationObservation Observation, AutomationTransition? Transition);

// Immutable, precomputed configuration shared by the UI evaluator and one background scan.
internal sealed class AutomationScanPlan
{
    private readonly AutomationRule[] allRules;
    private readonly Dictionary<Guid, AutomationRule> rulesById = [];
    private readonly Dictionary<string, AutomationCandidateGroup> candidates = new(StringComparer.OrdinalIgnoreCase);

    public static AutomationScanPlan Empty { get; } = new(new AutoSwitchSettings());
    public bool Enabled { get; }
    public AutomationRule[] Rules { get; }
    public bool CanScan => Enabled && Rules.Length != 0;

    public AutomationScanPlan(AutoSwitchSettings settings)
    {
        Enabled = settings.Enabled;
        allRules = settings.Rules.Select(rule => new AutomationRule(rule)).ToArray();
        Rules = allRules.Where(rule => rule.Enabled).ToArray();
        var groups = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < Rules.Length; index++)
        {
            var rule = Rules[index];
            rulesById.Add(rule.Id, rule);
            if (rule.FullPath is null) continue;
            var fileName = Path.GetFileName(rule.FullPath);
            if (!groups.TryGetValue(fileName, out var indexes))
                groups.Add(fileName, indexes = []);
            indexes.Add(index);
        }
        foreach (var (fileName, indexes) in groups)
            candidates.Add(fileName, new AutomationCandidateGroup(indexes.ToArray(), Rules));
    }

    public bool Matches(AutoSwitchSettings settings)
    {
        if (Enabled != settings.Enabled || allRules.Length != settings.Rules.Count) return false;
        for (var index = 0; index < allRules.Length; index++)
            if (!allRules[index].Matches(settings.Rules[index])) return false;
        return true;
    }

    public AutomationRule? FindRule(Guid? id) =>
        id is Guid value ? rulesById.GetValueOrDefault(value) : null;

    public bool TryGetCandidates(string fileName, out AutomationCandidateGroup group) =>
        candidates.TryGetValue(fileName, out group!);

    internal static string? NormalizePath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            return Path.GetFileName(fullPath).Length == 0 ? null : fullPath;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException or
            UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }
}

internal sealed class AutomationRule
{
    private readonly string executablePath;
    public Guid Id { get; }
    public string Name { get; }
    public bool Enabled { get; }
    public string? FullPath { get; }
    public Guid? RunningPresetId { get; }
    public Guid? StoppedPresetId { get; }

    public AutomationRule(AutoSwitchRule rule)
    {
        Id = rule.Id;
        Name = rule.Name;
        Enabled = rule.Enabled;
        executablePath = rule.ExecutablePath;
        FullPath = AutomationScanPlan.NormalizePath(executablePath);
        RunningPresetId = rule.RunningPresetId;
        StoppedPresetId = rule.StoppedPresetId;
    }

    public bool Matches(AutoSwitchRule rule) => Id == rule.Id && Name == rule.Name && Enabled == rule.Enabled &&
        string.Equals(executablePath, rule.ExecutablePath, StringComparison.OrdinalIgnoreCase) &&
        RunningPresetId == rule.RunningPresetId && StoppedPresetId == rule.StoppedPresetId;
}

internal sealed class AutomationCandidateGroup
{
    private readonly Dictionary<string, int[]> paths = new(StringComparer.OrdinalIgnoreCase);
    public int[] Indexes { get; }

    public AutomationCandidateGroup(int[] indexes, AutomationRule[] rules)
    {
        Indexes = indexes;
        var pathIndexes = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var index in indexes)
        {
            var path = rules[index].FullPath!;
            if (!pathIndexes.TryGetValue(path, out var matches))
                pathIndexes.Add(path, matches = []);
            matches.Add(index);
        }
        foreach (var (path, matches) in pathIndexes) paths.Add(path, matches.ToArray());
    }

    public bool TryGetMatches(string path, out int[] indexes) => paths.TryGetValue(path, out indexes!);
}

internal sealed class AutomationScanResult(bool?[] running)
{
    public int Count => running.Length;
    public bool? this[int index] => running[index];
    public static AutomationScanResult Unknown(int count) => new(new bool?[count]);
}

// The native detector and deterministic regressions use exactly this accumulation logic.
// One confirmed instance proves presence; other instances can never undo that evidence.
internal sealed class ProcessScanAccumulator
{
    private readonly bool?[] running;

    public ProcessScanAccumulator(AutomationScanPlan plan)
    {
        running = new bool?[plan.Rules.Length];
        for (var index = 0; index < running.Length; index++)
            running[index] = plan.Rules[index].FullPath is null ? null : false;
    }

    public bool AllRunning(AutomationCandidateGroup group)
    {
        foreach (var index in group.Indexes)
            if (running[index] != true) return false;
        return true;
    }

    public void ObservePath(AutomationCandidateGroup group, string path)
    {
        var fullPath = AutomationScanPlan.NormalizePath(path);
        if (fullPath is null)
        {
            ObserveUnavailable(group);
            return;
        }
        if (group.TryGetMatches(fullPath, out var indexes))
            foreach (var index in indexes) running[index] = true;
    }

    public void ObserveUnavailable(AutomationCandidateGroup group)
    {
        foreach (var index in group.Indexes)
            if (running[index] != true) running[index] = null;
    }

    public AutomationScanResult Complete(bool enumerationComplete = true)
    {
        if (!enumerationComplete)
            for (var index = 0; index < running.Length; index++)
                if (running[index] != true) running[index] = null;
        return new(running);
    }
}

internal static class ProcessDetector
{
    private const uint SnapshotProcesses = 0x00000002;
    private const uint QueryLimitedInformation = 0x00001000;
    private const uint StillActive = 259;
    private const int ErrorNoMoreFiles = 18;
    private const int ErrorInvalidParameter = 87;
    private const int ImagePathCapacity = 32768;

    public static AutomationScanResult Check(AutomationScanPlan plan)
    {
        using var snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
        if (snapshot.IsInvalid) return AutomationScanResult.Unknown(plan.Rules.Length);
        var accumulator = new ProcessScanAccumulator(plan);

        var entry = new ProcessEntry32
        {
            Size = (uint)Marshal.SizeOf<ProcessEntry32>(),
            ExeFile = string.Empty
        };
        if (!Process32First(snapshot, ref entry))
            return accumulator.Complete(Marshal.GetLastWin32Error() == ErrorNoMoreFiles);

        StringBuilder? imagePath = null;
        do
        {
            // Query each interested process at most once, regardless of rule count.
            if (!plan.TryGetCandidates(entry.ExeFile, out var group) || accumulator.AllRunning(group)) continue;
            var result = ReadPath(entry.ProcessId, ref imagePath, out var path);
            if (result == CandidatePathResult.Available) accumulator.ObservePath(group, path!);
            else if (result == CandidatePathResult.Unknown) accumulator.ObserveUnavailable(group);
        }
        while (Process32Next(snapshot, ref entry));

        return accumulator.Complete(Marshal.GetLastWin32Error() == ErrorNoMoreFiles);
    }

    private enum CandidatePathResult { Available, Exited, Unknown }

    private static CandidatePathResult ReadPath(uint processId, ref StringBuilder? imagePath, out string? path)
    {
        path = null;
        using var process = OpenProcess(QueryLimitedInformation, false, processId);
        if (process.IsInvalid)
        {
            // A snapshot can outlive a process. An inaccessible live candidate cannot be ignored.
            return Marshal.GetLastWin32Error() == ErrorInvalidParameter
                ? CandidatePathResult.Exited : CandidatePathResult.Unknown;
        }

        imagePath ??= new StringBuilder(ImagePathCapacity);
        imagePath.Clear();
        var length = (uint)imagePath.Capacity;
        if (!QueryFullProcessImageName(process, 0, imagePath, ref length))
            return GetExitCodeProcess(process, out var exitCode) && exitCode != StillActive
                ? CandidatePathResult.Exited : CandidatePathResult.Unknown;

        path = imagePath.ToString();
        return CandidatePathResult.Available;
    }

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

// UI-thread-owned state machine. Unknown or obsolete scans never mutate effective state.
internal sealed class AutomationStateEvaluator
{
    private Guid? effectiveRuleId;
    private string? effectivePath;
    private bool effectiveRunning;
    private Guid? appliedPresetId;

    public AutomationScanPlan Plan { get; private set; } = AutomationScanPlan.Empty;
    public int Generation { get; private set; }
    private Guid? ActiveRuleId => effectiveRunning ? effectiveRuleId : null;

    public bool Configure(AutoSwitchSettings settings)
    {
        if (Generation != 0 && Plan.Matches(settings)) return false;
        var next = new AutomationScanPlan(settings);
        var retained = next.FindRule(effectiveRuleId);
        if (!next.Enabled || retained is null ||
            !string.Equals(effectivePath, retained.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            // Removing, disabling or retargeting a rule is not a process-exit event.
            effectiveRuleId = null;
            effectivePath = null;
            effectiveRunning = false;
            appliedPresetId = null;
        }
        Plan = next;
        Generation++;
        return true;
    }

    public AutomationObservation PendingObservation()
    {
        if (!Plan.Enabled) return new(null, "自动切换：已关闭", null);
        if (Plan.Rules.Length == 0) return new(null, "自动切换：没有已启用的游戏规则", null);
        var active = Plan.FindRule(ActiveRuleId);
        return new(null, active is null ? "自动切换：等待检测游戏状态…" :
            $"自动切换：正在检测《{active.Name}》等游戏状态…", ActiveRuleId);
    }

    public AutomationEvaluation? Evaluate(int generation, AutomationScanResult result)
    {
        if (generation != Generation) return null;
        if (!Plan.CanScan) return new(PendingObservation(), null);
        if (result.Count != Plan.Rules.Length)
            throw new ArgumentException("检测结果与游戏规则数量不一致。", nameof(result));

        // Even a lower-priority unknown target can prevent a safe all-games-exited decision.
        // Freeze every automatic transition until all enabled targets have known states.
        for (var index = 0; index < Plan.Rules.Length; index++)
            if (result[index] is null)
                return new(new(null, $"自动切换：检测《{Plan.Rules[index].Name}》状态异常，暂不切换", ActiveRuleId), null);

        AutomationRule? winner = null;
        for (var index = 0; index < Plan.Rules.Length; index++)
            if (result[index] == true)
            {
                winner = Plan.Rules[index];
                break;
            }

        AutomationTransition? transition = null;
        if (winner is not null)
        {
            if (effectiveRuleId != winner.Id || !effectiveRunning || appliedPresetId != winner.RunningPresetId)
                transition = new(winner.Id, true);
            effectiveRuleId = winner.Id;
            effectivePath = winner.FullPath;
            effectiveRunning = true;
            appliedPresetId = winner.RunningPresetId;
            return new(new(true, $"自动切换：游戏正在运行（{winner.Name}）", winner.Id), transition);
        }

        var previous = Plan.FindRule(effectiveRuleId);
        if (previous is not null && effectiveRunning)
        {
            transition = new(previous.Id, false);
            appliedPresetId = previous.StoppedPresetId;
        }
        effectiveRunning = false;
        return new(new(false, previous is null ? "自动切换：等待游戏启动" :
            $"自动切换：等待游戏启动（上次游戏：{previous.Name}）", null), transition);
    }
}

internal sealed class ProcessMonitor : IDisposable
{
    private readonly Action<AutomationObservation, AutomationTransition?> report;
    private readonly SynchronizationContext uiContext;
    private readonly int uiThreadId;
    private readonly System.Windows.Forms.Timer timer;
    private readonly AutomationStateEvaluator evaluator = new();
    private AutomationObservation? lastObservation;
    private bool scanInProgress;
    private int disposed;

    // Construct on the WinForms UI thread; calls from other threads are marshalled back here.
    public ProcessMonitor(Action<AutomationObservation, AutomationTransition?> report)
    {
        ArgumentNullException.ThrowIfNull(report);
        this.report = report;
        uiThreadId = Environment.CurrentManagedThreadId;
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) => StartScan();
    }

    public void Configure(AutoSwitchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (Volatile.Read(ref disposed) != 0) return;
        if (Environment.CurrentManagedThreadId != uiThreadId)
        {
            var copy = settings.Copy();
            PostToUi(() => ConfigureOnUi(copy));
            return;
        }
        ConfigureOnUi(settings);
    }

    private void ConfigureOnUi(AutoSwitchSettings next)
    {
        if (Volatile.Read(ref disposed) != 0 || !evaluator.Configure(next)) return;
        timer.Stop();
        Publish(evaluator.PendingObservation(), null);
        if (Volatile.Read(ref disposed) != 0 || !evaluator.Plan.CanScan) return;
        timer.Start();
        StartScan();
    }

    private async void StartScan()
    {
        if (Volatile.Read(ref disposed) != 0 || scanInProgress || !evaluator.Plan.CanScan) return;
        scanInProgress = true;
        var scanGeneration = evaluator.Generation;
        var plan = evaluator.Plan;
        AutomationScanResult result;
        try
        {
            result = await Task.Run(() => ProcessDetector.Check(plan)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Exception messages may contain executable paths. Only named-game errors reach the UI.
            result = AutomationScanResult.Unknown(plan.Rules.Length);
        }
        if (Volatile.Read(ref disposed) != 0) return;
        PostToUi(() => CompleteScan(scanGeneration, result));
    }

    private void CompleteScan(int scanGeneration, AutomationScanResult result)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        scanInProgress = false;
        var evaluation = evaluator.Evaluate(scanGeneration, result);
        if (evaluation is null)
        {
            // Changed configurations wait for the old worker rather than overlapping snapshots.
            StartScan();
            return;
        }
        Publish(evaluation.Observation, evaluation.Transition);
    }

    private void Publish(AutomationObservation observation, AutomationTransition? transition)
    {
        if (observation == lastObservation && transition is null) return;
        lastObservation = observation;
        report(observation, transition);
    }

    private void PostToUi(Action action)
    {
        try
        {
            uiContext.Post(_ => action(), null);
        }
        catch (InvalidAsynchronousStateException) when (Volatile.Read(ref disposed) != 0)
        {
            // The UI was destroyed between disposal and posting the final callback.
        }
        catch (InvalidOperationException) when (Volatile.Read(ref disposed) != 0)
        {
            // No callbacks need delivery after the UI context has gone away.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        if (Environment.CurrentManagedThreadId != uiThreadId)
        {
            PostToUi(timer.Dispose);
            return;
        }
        timer.Dispose();
    }
}
