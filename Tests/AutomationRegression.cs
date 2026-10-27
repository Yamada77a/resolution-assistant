namespace ResolutionAssistant.Regression;

internal static class AutomationRegression
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        (nameof(InitialAbsenceDoesNotRestore), InitialAbsenceDoesNotRestore),
        (nameof(PriorityHandoffAndLastExit), PriorityHandoffAndLastExit),
        (nameof(LowerPriorityActivityDoesNotReapply), LowerPriorityActivityDoesNotReapply),
        (nameof(HigherPriorityGamePreempts), HigherPriorityGamePreempts),
        (nameof(DisabledRulesAreNotDetected), DisabledRulesAreNotDetected),
        (nameof(UnknownFreezesActiveWinnerAndRestore), UnknownFreezesActiveWinnerAndRestore),
        (nameof(InitialUnknownDoesNotCreateExitEntitlement), InitialUnknownDoesNotCreateExitEntitlement),
        (nameof(UnchangedAndRenamePreserveManualOverride), UnchangedAndRenamePreserveManualOverride),
        (nameof(ApplicablePresetChangesReapply), ApplicablePresetChangesReapply),
        (nameof(ReorderingChangesWinnerWithoutDesktop), ReorderingChangesWinnerWithoutDesktop),
        (nameof(DeletingActiveRuleDoesNotRestore), DeletingActiveRuleDoesNotRestore),
        (nameof(DisablingActiveRuleDoesNotRestore), DisablingActiveRuleDoesNotRestore),
        (nameof(GlobalDisableCancelsExitEntitlement), GlobalDisableCancelsExitEntitlement),
        (nameof(RetargetingActiveRuleDoesNotRestore), RetargetingActiveRuleDoesNotRestore),
        (nameof(EquivalentNormalizedPathPreservesState), EquivalentNormalizedPathPreservesState),
        (nameof(StaleScanCannotMutateState), StaleScanCannotMutateState),
        (nameof(PlanOwnsConfigurationSnapshot), PlanOwnsConfigurationSnapshot),
        (nameof(ExactPathsAndDuplicateRulesShareCandidate), ExactPathsAndDuplicateRulesShareCandidate),
        (nameof(MultipleInstancesRemainPresentUntilLastExit), MultipleInstancesRemainPresentUntilLastExit),
        (nameof(InaccessibleCandidatesAreConservative), InaccessibleCandidatesAreConservative),
        (nameof(IncompleteSnapshotNeverProvesAbsence), IncompleteSnapshotNeverProvesAbsence),
        (nameof(InvalidPathsAndErrorsUseOnlyGameNames), InvalidPathsAndErrorsUseOnlyGameNames),
    ];

    private static AutoSwitchRule Rule(string name, string path, bool enabled = true) => new()
    {
        Name = name,
        ExecutablePath = path,
        Enabled = enabled,
        RunningPresetId = Guid.NewGuid(),
        StoppedPresetId = Guid.NewGuid()
    };

    private static AutoSwitchSettings Settings(params AutoSwitchRule[] rules) => new()
    {
        Enabled = true,
        Rules = rules.ToList()
    };

    private static AutomationStateEvaluator Start(AutoSwitchSettings settings)
    {
        var evaluator = new AutomationStateEvaluator();
        RegressionAssert.True(evaluator.Configure(settings), "First configuration must initialize the evaluator.");
        return evaluator;
    }

    private static AutomationEvaluation Observe(AutomationStateEvaluator evaluator, params bool?[] running) =>
        evaluator.Evaluate(evaluator.Generation, new AutomationScanResult(running))
        ?? throw new InvalidOperationException("Current scan was incorrectly discarded.");

    private static void Transition(AutomationEvaluation result, AutoSwitchRule rule, bool running) =>
        RegressionAssert.Equal<AutomationTransition?>(new(rule.Id, running), result.Transition, "Incorrect automatic transition.");

    private static void NoTransition(AutomationEvaluation result) =>
        RegressionAssert.True(result.Transition is null, "An unchanged or uncertain state must not overwrite a manual selection.");

    private static void InitialAbsenceDoesNotRestore()
    {
        var rule = Rule("游戏 A", @"C:\Games\A.exe");
        var evaluator = Start(Settings(rule));
        var result = Observe(evaluator, false);
        NoTransition(result);
        RegressionAssert.Equal<bool?>(false, result.Observation.IsRunning, "Initial absence should be known.");
        RegressionAssert.Equal<Guid?>(null, result.Observation.ActiveRuleId, "Initial absence has no active game.");
        NoTransition(Observe(evaluator, false));
    }

    private static void PriorityHandoffAndLastExit()
    {
        var a = Rule("游戏 A", @"C:\Games\A.exe");
        var b = Rule("游戏 B", @"C:\Games\B.exe");
        var evaluator = Start(Settings(a, b));
        Transition(Observe(evaluator, true, true), a, true);
        NoTransition(Observe(evaluator, true, true));
        var handoff = Observe(evaluator, false, true);
        Transition(handoff, b, true);
        RegressionAssert.Equal<Guid?>(b.Id, handoff.Observation.ActiveRuleId, "B must directly take over when A exits.");
        Transition(Observe(evaluator, false, false), b, false);
        NoTransition(Observe(evaluator, false, false));
        Transition(Observe(evaluator, true, false), a, true);
    }

    private static void LowerPriorityActivityDoesNotReapply()
    {
        var a = Rule("游戏 A", @"C:\Games\A.exe");
        var b = Rule("游戏 B", @"C:\Games\B.exe");
        var evaluator = Start(Settings(a, b));
        Transition(Observe(evaluator, true, false), a, true);
        NoTransition(Observe(evaluator, true, true));
        NoTransition(Observe(evaluator, true, false));
    }

    private static void HigherPriorityGamePreempts()
    {
        var a = Rule("游戏 A", @"C:\Games\A.exe");
        var b = Rule("游戏 B", @"C:\Games\B.exe");
        var evaluator = Start(Settings(a, b));
        Transition(Observe(evaluator, false, true), b, true);
        Transition(Observe(evaluator, true, true), a, true);
        Transition(Observe(evaluator, false, false), a, false);
    }

    private static void DisabledRulesAreNotDetected()
    {
        var disabled = Rule("停用规则", "\0invalid", false);
        var enabled = Rule("有效游戏", @"C:\Games\Enabled.exe");
        var evaluator = Start(Settings(disabled, enabled));
        RegressionAssert.Equal(1, evaluator.Plan.Rules.Length, "Disabled rules must not participate in a scan.");
        NoTransition(Observe(evaluator, false));
        Transition(Observe(evaluator, true), enabled, true);
        var settings = Settings(disabled);
        evaluator.Configure(settings);
        RegressionAssert.True(!evaluator.Plan.CanScan, "An all-disabled rule list must stop monitoring.");
        RegressionAssert.True(evaluator.PendingObservation().ActiveRuleId is null, "Disabling all rules clears the active game.");
    }

    private static void UnknownFreezesActiveWinnerAndRestore()
    {
        var a = Rule("高优先级", @"C:\Games\A.exe");
        var b = Rule("低优先级", @"C:\Games\B.exe");
        var evaluator = Start(Settings(a, b));
        Transition(Observe(evaluator, false, true), b, true);
        var uncertain = Observe(evaluator, null, false);
        NoTransition(uncertain);
        RegressionAssert.Equal<bool?>(null, uncertain.Observation.IsRunning, "Unknown must remain unknown.");
        RegressionAssert.Equal<Guid?>(b.Id, uncertain.Observation.ActiveRuleId, "Unknown must retain the last effective winner.");
        uncertain = Observe(evaluator, true, null);
        NoTransition(uncertain);
        RegressionAssert.Equal<Guid?>(b.Id, uncertain.Observation.ActiveRuleId, "Even a lower-priority unknown must freeze a winner change.");
        Transition(Observe(evaluator, false, false), b, false);
        NoTransition(Observe(evaluator, false, false));
    }

    private static void InitialUnknownDoesNotCreateExitEntitlement()
    {
        var evaluator = Start(Settings(Rule("游戏", @"C:\Games\Game.exe")));
        NoTransition(Observe(evaluator, (bool?)null));
        NoTransition(Observe(evaluator, false));
        RegressionAssert.True(evaluator.PendingObservation().ActiveRuleId is null, "Unknown is not proof of a running game.");
    }

    private static void UnchangedAndRenamePreserveManualOverride()
    {
        var rule = Rule("旧名称", @"C:\Games\Game.exe");
        var settings = Settings(rule);
        var evaluator = Start(settings);
        Transition(Observe(evaluator, true), rule, true);
        evaluator.Configure(settings.Copy());
        NoTransition(Observe(evaluator, true));
        rule.Name = "我的游戏";
        evaluator.Configure(settings);
        var renamed = Observe(evaluator, true);
        NoTransition(renamed);
        RegressionAssert.True(renamed.Observation.Message.Contains(rule.Name), "Status must use the new custom name.");
        Transition(Observe(evaluator, false), rule, false);
        rule.Name = "再次改名";
        evaluator.Configure(settings);
        NoTransition(Observe(evaluator, false));
    }

    private static void ApplicablePresetChangesReapply()
    {
        var a = Rule("游戏 A", @"C:\Games\A.exe");
        var b = Rule("游戏 B", @"C:\Games\B.exe");
        var settings = Settings(a, b);
        var evaluator = Start(settings);
        Transition(Observe(evaluator, true, false), a, true);
        a.RunningPresetId = Guid.NewGuid();
        evaluator.Configure(settings);
        NoTransition(Observe(evaluator, true, null));
        Transition(Observe(evaluator, true, false), a, true);
        NoTransition(Observe(evaluator, true, false));
        a.StoppedPresetId = Guid.NewGuid();
        b.RunningPresetId = Guid.NewGuid();
        evaluator.Configure(settings);
        NoTransition(Observe(evaluator, true, false));
        Transition(Observe(evaluator, false, false), a, false);
        a.StoppedPresetId = Guid.NewGuid();
        evaluator.Configure(settings);
        NoTransition(Observe(evaluator, false, false));
        Transition(Observe(evaluator, true, false), a, true);
        Transition(Observe(evaluator, false, false), a, false);
    }

    private static void ReorderingChangesWinnerWithoutDesktop()
    {
        var a = Rule("游戏 A", @"C:\Games\A.exe");
        var b = Rule("游戏 B", @"C:\Games\B.exe");
        var evaluator = Start(Settings(a, b));
        Transition(Observe(evaluator, true, true), a, true);
        evaluator.Configure(Settings(b, a));
        Transition(Observe(evaluator, true, true), b, true);
        Transition(Observe(evaluator, false, false), b, false);
    }

    private static void DeletingActiveRuleDoesNotRestore()
    {
        var a = Rule("游戏 A", @"C:\Games\A.exe");
        var b = Rule("游戏 B", @"C:\Games\B.exe");
        var evaluator = Start(Settings(a, b));
        Transition(Observe(evaluator, true, false), a, true);
        evaluator.Configure(Settings(b));
        NoTransition(Observe(evaluator, false));
        Transition(Observe(evaluator, true), b, true);
        evaluator.Configure(Settings());
        NoTransition(Observe(evaluator));
        RegressionAssert.True(evaluator.PendingObservation().ActiveRuleId is null, "Deleting all games must clear the winner.");
    }

    private static void DisablingActiveRuleDoesNotRestore()
    {
        var a = Rule("游戏 A", @"C:\Games\A.exe");
        var b = Rule("游戏 B", @"C:\Games\B.exe");
        var settings = Settings(a, b);
        var evaluator = Start(settings);
        Transition(Observe(evaluator, true, true), a, true);
        a.Enabled = false;
        evaluator.Configure(settings);
        Transition(Observe(evaluator, true), b, true);
        Transition(Observe(evaluator, false), b, false);
        a.Enabled = true;
        evaluator.Configure(settings);
        Transition(Observe(evaluator, true, false), a, true);
        a.Enabled = false;
        evaluator.Configure(settings);
        NoTransition(Observe(evaluator, false));
    }

    private static void GlobalDisableCancelsExitEntitlement()
    {
        var rule = Rule("游戏", @"C:\Games\Game.exe");
        var settings = Settings(rule);
        var evaluator = Start(settings);
        Transition(Observe(evaluator, true), rule, true);
        var oldGeneration = evaluator.Generation;
        settings.Enabled = false;
        evaluator.Configure(settings);
        RegressionAssert.True(evaluator.Evaluate(oldGeneration, new AutomationScanResult([false])) is null,
            "A pre-disable scan must be discarded.");
        NoTransition(Observe(evaluator, false));
        RegressionAssert.True(evaluator.PendingObservation().ActiveRuleId is null, "Globally disabled automation has no active game.");
        settings.Enabled = true;
        evaluator.Configure(settings);
        NoTransition(Observe(evaluator, false));
        Transition(Observe(evaluator, true), rule, true);
    }

    private static void RetargetingActiveRuleDoesNotRestore()
    {
        var rule = Rule("游戏", @"C:\Games\Old.exe");
        var settings = Settings(rule);
        var evaluator = Start(settings);
        Transition(Observe(evaluator, true), rule, true);
        rule.ExecutablePath = @"C:\Games\New.exe";
        evaluator.Configure(settings);
        NoTransition(Observe(evaluator, false));
        Transition(Observe(evaluator, true), rule, true);
    }

    private static void EquivalentNormalizedPathPreservesState()
    {
        var rule = Rule("游戏", @"C:\Games\Game.exe");
        var settings = Settings(rule);
        var evaluator = Start(settings);
        Transition(Observe(evaluator, true), rule, true);
        rule.ExecutablePath = @"c:\games\Folder\..\GAME.EXE";
        evaluator.Configure(settings);
        NoTransition(Observe(evaluator, true));
        Transition(Observe(evaluator, false), rule, false);
    }

    private static void StaleScanCannotMutateState()
    {
        var a = Rule("游戏 A", @"C:\Games\A.exe");
        var b = Rule("游戏 B", @"C:\Games\B.exe");
        var evaluator = Start(Settings(a, b));
        Transition(Observe(evaluator, true, false), a, true);
        var oldGeneration = evaluator.Generation;
        evaluator.Configure(Settings(b, a));
        RegressionAssert.True(evaluator.Evaluate(oldGeneration, new AutomationScanResult([false, false])) is null,
            "A stale all-absent scan must not restore desktop.");
        RegressionAssert.Equal<Guid?>(a.Id, evaluator.PendingObservation().ActiveRuleId, "Stale scan changed the retained winner.");
        Transition(Observe(evaluator, true, true), b, true);
        RegressionAssert.True(evaluator.Evaluate(oldGeneration, new AutomationScanResult([true, false])) is null,
            "A late stale running scan must not take back priority.");
        NoTransition(Observe(evaluator, true, true));
        Transition(Observe(evaluator, false, false), b, false);
    }

    private static void PlanOwnsConfigurationSnapshot()
    {
        var rule = Rule("原名称", @"C:\Games\Original.exe");
        var settings = Settings(rule);
        var evaluator = Start(settings);
        var plan = evaluator.Plan;
        rule.Name = "新名称";
        rule.ExecutablePath = @"C:\Games\Replacement.exe";
        settings.Rules.Clear();
        RegressionAssert.Equal("原名称", plan.Rules[0].Name, "Mutable settings leaked into an in-flight scan.");
        RegressionAssert.True(plan.TryGetCandidates("Original.exe", out _), "An in-flight lookup must keep its original paths.");
        RegressionAssert.True(!plan.TryGetCandidates("Replacement.exe", out _), "A mutable rule changed the original lookup.");
    }

    private static void ExactPathsAndDuplicateRulesShareCandidate()
    {
        var a = Rule("目录 A", @"C:\Games\A\Game.exe");
        var duplicate = Rule("同路径规则", @"c:\games\a\GAME.EXE");
        var b = Rule("目录 B", @"C:\Games\B\Game.exe");
        var plan = new AutomationScanPlan(Settings(a, duplicate, b));
        RegressionAssert.True(plan.TryGetCandidates("GAME.EXE", out var group), "Executable-name lookup must ignore case.");
        var accumulator = new ProcessScanAccumulator(plan);
        accumulator.ObservePath(group, @"C:\Other\Game.exe");
        var absent = accumulator.Complete();
        for (var index = 0; index < 3; index++) RegressionAssert.Equal<bool?>(false, absent[index], "Same filename in a wrong directory must not match.");
        accumulator = new ProcessScanAccumulator(plan);
        accumulator.ObservePath(group, @"c:\GAMES\A\Folder\..\GAME.EXE");
        var present = accumulator.Complete();
        RegressionAssert.Equal<bool?>(true, present[0], "Normalized full path should match.");
        RegressionAssert.Equal<bool?>(true, present[1], "One path query must satisfy duplicate target cards.");
        RegressionAssert.Equal<bool?>(false, present[2], "A different complete path must remain absent.");
    }

    private static void MultipleInstancesRemainPresentUntilLastExit()
    {
        var rule = Rule("多实例游戏", @"C:\Games\Game.exe");
        var evaluator = Start(Settings(rule));
        var plan = evaluator.Plan;
        RegressionAssert.True(plan.TryGetCandidates("Game.exe", out var group), "Missing candidate group.");
        var two = new ProcessScanAccumulator(plan);
        two.ObservePath(group, rule.ExecutablePath);
        two.ObservePath(group, rule.ExecutablePath);
        Transition(evaluator.Evaluate(evaluator.Generation, two.Complete())!, rule, true);
        var one = new ProcessScanAccumulator(plan);
        one.ObservePath(group, rule.ExecutablePath);
        one.ObservePath(group, @"C:\Other\Game.exe");
        NoTransition(evaluator.Evaluate(evaluator.Generation, one.Complete())!);
        var none = new ProcessScanAccumulator(plan);
        none.ObservePath(group, @"C:\Other\Game.exe");
        Transition(evaluator.Evaluate(evaluator.Generation, none.Complete())!, rule, false);
    }

    private static void InaccessibleCandidatesAreConservative()
    {
        var a = Rule("目录 A", @"C:\Games\A\Game.exe");
        var b = Rule("目录 B", @"C:\Games\B\Game.exe");
        var plan = new AutomationScanPlan(Settings(a, b));
        RegressionAssert.True(plan.TryGetCandidates("Game.exe", out var group), "Missing candidate group.");
        var accumulator = new ProcessScanAccumulator(plan);
        accumulator.ObserveUnavailable(group);
        accumulator.ObservePath(group, a.ExecutablePath);
        accumulator.ObserveUnavailable(group);
        var result = accumulator.Complete();
        RegressionAssert.Equal<bool?>(true, result[0], "One confirmed instance proves presence despite inaccessible siblings.");
        RegressionAssert.Equal<bool?>(null, result[1], "An inaccessible same-named process could belong to the other directory.");
        RegressionAssert.True(!accumulator.AllRunning(group), "Unknown targets still require available candidate queries.");
        accumulator = new ProcessScanAccumulator(plan);
        accumulator.ObserveUnavailable(group);
        accumulator.ObservePath(group, a.ExecutablePath);
        accumulator.ObservePath(group, b.ExecutablePath);
        RegressionAssert.True(accumulator.AllRunning(group), "Confirmed matches should allow later redundant queries to be skipped.");
        result = accumulator.Complete();
        RegressionAssert.Equal<bool?>(true, result[1], "A later exact match must resolve earlier uncertainty.");
    }

    private static void IncompleteSnapshotNeverProvesAbsence()
    {
        var a = Rule("游戏 A", @"C:\Games\A.exe");
        var b = Rule("游戏 B", @"C:\Games\B.exe");
        var evaluator = Start(Settings(a, b));
        Transition(Observe(evaluator, false, true), b, true);
        var accumulator = new ProcessScanAccumulator(evaluator.Plan);
        RegressionAssert.True(evaluator.Plan.TryGetCandidates("A.exe", out var group), "Missing candidate group.");
        accumulator.ObservePath(group, a.ExecutablePath);
        var incomplete = accumulator.Complete(false);
        RegressionAssert.Equal<bool?>(true, incomplete[0], "Incomplete enumeration does not erase confirmed presence.");
        RegressionAssert.Equal<bool?>(null, incomplete[1], "Incomplete enumeration cannot prove absence.");
        var result = evaluator.Evaluate(evaluator.Generation, incomplete)!;
        NoTransition(result);
        RegressionAssert.Equal<Guid?>(b.Id, result.Observation.ActiveRuleId, "An incomplete snapshot must freeze the effective game.");
    }

    private static void InvalidPathsAndErrorsUseOnlyGameNames()
    {
        var rule = Rule("自定义游戏名称", "C:\\private-folder\\\0Game.exe");
        var evaluator = Start(Settings(rule));
        var accumulator = new ProcessScanAccumulator(evaluator.Plan);
        var result = evaluator.Evaluate(evaluator.Generation, accumulator.Complete())!;
        NoTransition(result);
        RegressionAssert.Equal<bool?>(null, result.Observation.IsRunning, "Invalid paths must be unknown, not absent.");
        RegressionAssert.True(result.Observation.Message.Contains(rule.Name), "Errors must identify the named game.");
        RegressionAssert.True(!result.Observation.Message.Contains("private-folder"), "A path or path exception leaked into the status.");
        result = evaluator.Evaluate(evaluator.Generation, AutomationScanResult.Unknown(1))!;
        RegressionAssert.True(!result.Observation.Message.Contains(rule.ExecutablePath), "Unexpected detector failures must not expose paths.");
    }

}
