using System.Text.Json;
using System.Text.Json.Nodes;

namespace ResolutionAssistant.Regression;

internal static class ConfigMigrationRegression
{
    private const string DataDirectoryVariable = "RESOLUTION_ASSISTANT_DATA_DIR";
    private static readonly JsonSerializerOptions JsonOptions = new() { IgnoreReadOnlyProperties = true, WriteIndented = true };

    public static IEnumerable<(string Name, Action Run)> Cases
    {
        get
        {
            yield return (nameof(LegacyMigrationPreservesPresetsAndHotkeys), LegacyMigrationPreservesPresetsAndHotkeys);
            yield return (nameof(LegacyEnabledRuleMigratesWithoutSourceId), LegacyEnabledRuleMigratesWithoutSourceId);
            yield return (nameof(LegacyPresetOnlyRuleGetsFallbackName), LegacyPresetOnlyRuleGetsFallbackName);
            yield return (nameof(LegacyEmptyRuleDoesNotCreateCard), LegacyEmptyRuleDoesNotCreateCard);
            yield return (nameof(MissingAutomationRemainsEmptyAndDisabled), MissingAutomationRemainsEmptyAndDisabled);
            yield return (nameof(ExplicitEmptyRulesSurviveSaveAndReload), ExplicitEmptyRulesSurviveSaveAndReload);
            yield return (nameof(NewRulesRoundTripOrderAndIdentity), NewRulesRoundTripOrderAndIdentity);
            yield return (nameof(DanglingPresetReferencesRemainRepairable), DanglingPresetReferencesRemainRepairable);
            yield return (nameof(SaveRejectsInvalidModelsWithoutReplacingFile), SaveRejectsInvalidModelsWithoutReplacingFile);
            foreach (var (name, mutate) in InvalidSources())
            {
                yield return (name, () =>
                {
                    var root = NewSource();
                    mutate(root);
                    WithDirectory(() =>
                    {
                        var text = root.ToJsonString(JsonOptions);
                        File.WriteAllText(ConfigStore.FilePath, text);
                        RegressionAssert.Throws<InvalidDataException>(() => ConfigStore.Load(), $"Invalid source {name} was silently accepted.");
                        RegressionAssert.Equal(text, File.ReadAllText(ConfigStore.FilePath), "A rejected source must remain untouched for repair.");
                    });
                });
            }
        }
    }

    private static AppConfig Fixture() => new()
    {
        Presets =
        [
            new() { Name = "游戏预设", Width = 1920, Height = 1440, RefreshRate = 240, Modifiers = 3, VirtualKey = (uint)Keys.F6 },
            new() { Name = "桌面预设", Width = 2560, Height = 1440, RefreshRate = 165, Modifiers = 6, VirtualKey = (uint)Keys.D7 }
        ]
    };

    private static JsonObject Source(AppConfig config) => JsonSerializer.SerializeToNode(config, JsonOptions)!.AsObject();

    private static JsonObject NewSource()
    {
        var config = Fixture();
        config.AutoSwitch.Enabled = true;
        config.AutoSwitch.Rules.Add(new()
        {
            Name = "第一个游戏",
            ExecutablePath = @"C:\Games\First.exe",
            RunningPresetId = config.Presets[0].Id,
            StoppedPresetId = config.Presets[1].Id
        });
        return Source(config);
    }

    private static JsonObject RuleObject(JsonObject root) => root["AutoSwitch"]!["Rules"]![0]!.AsObject();

    private static void WithDirectory(Action action)
    {
        var previous = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        var directory = Path.Combine(Path.GetTempPath(), $"ResolutionAssistant.Regression.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Environment.SetEnvironmentVariable(DataDirectoryVariable, directory);
            RegressionAssert.Equal(Path.Combine(directory, "settings.json"), ConfigStore.FilePath, "Regression isolation must take effect before configuration access.");
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(DataDirectoryVariable, previous);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void WithConfiguration(JsonObject root, Action<AppConfig> inspect)
    {
        WithDirectory(() =>
        {
            var text = root.ToJsonString(JsonOptions);
            File.WriteAllText(ConfigStore.FilePath, text);
            var loaded = ConfigStore.Load();
            RegressionAssert.Equal(2, loaded.Version, "Loaded configuration must use the v2 model.");
            RegressionAssert.Equal(text, File.ReadAllText(ConfigStore.FilePath), "Loading must not rewrite a migrated source.");
            inspect(loaded);
        });
    }

    private static void SamePreset(Preset expected, Preset actual)
    {
        RegressionAssert.Equal(expected.Id, actual.Id, "Migration changed a preset ID.");
        RegressionAssert.Equal(expected.Name, actual.Name, "Migration changed a preset name.");
        RegressionAssert.Equal(expected.Width, actual.Width, "Migration changed a preset width.");
        RegressionAssert.Equal(expected.Height, actual.Height, "Migration changed a preset height.");
        RegressionAssert.Equal(expected.RefreshRate, actual.RefreshRate, "Migration changed a preset refresh rate.");
        RegressionAssert.Equal(expected.Modifiers, actual.Modifiers, "Migration changed hotkey modifiers.");
        RegressionAssert.Equal(expected.VirtualKey, actual.VirtualKey, "Migration changed a hotkey key.");
    }

    private static void LegacyMigrationPreservesPresetsAndHotkeys()
    {
        var original = Fixture();
        var root = Source(original);
        root["Version"] = 1;
        var legacyId = Guid.NewGuid();
        root["AutoSwitch"] = new JsonObject
        {
            ["Id"] = legacyId,
            ["Enabled"] = false,
            ["ExecutablePath"] = @"C:\PrivateGames\NamedGame.exe",
            ["RunningPresetId"] = original.Presets[0].Id,
            ["StoppedPresetId"] = original.Presets[1].Id
        };
        WithConfiguration(root, loaded =>
        {
            RegressionAssert.True(!loaded.AutoSwitch.Enabled, "Migration must preserve the legacy global enable flag.");
            RegressionAssert.Equal(1, loaded.AutoSwitch.Rules.Count, "A configured legacy target must become one card.");
            var rule = loaded.AutoSwitch.Rules[0];
            RegressionAssert.True(rule.Id != Guid.Empty && rule.Id != legacyId, "Legacy migration must allocate a new stable card identity.");
            RegressionAssert.Equal("NamedGame", rule.Name, "Legacy card name should come from the executable filename stem.");
            RegressionAssert.True(rule.Enabled, "Legacy card enablement is separate from global enablement.");
            RegressionAssert.Equal(@"C:\PrivateGames\NamedGame.exe", rule.ExecutablePath, "Migration changed the target path.");
            RegressionAssert.Equal<Guid?>(original.Presets[0].Id, rule.RunningPresetId, "Migration lost the running preset.");
            RegressionAssert.Equal<Guid?>(original.Presets[1].Id, rule.StoppedPresetId, "Migration lost the exit preset.");
            RegressionAssert.Equal(original.Presets.Count, loaded.Presets.Count, "Migration changed the preset count.");
            for (var index = 0; index < original.Presets.Count; index++) SamePreset(original.Presets[index], loaded.Presets[index]);
            ConfigStore.Save(loaded);
            var saved = JsonNode.Parse(File.ReadAllText(ConfigStore.FilePath))!.AsObject();
            RegressionAssert.Equal(2, saved["Version"]!.GetValue<int>(), "Explicit save must write v2.");
            RegressionAssert.True(saved["AutoSwitch"]!["Rules"] is JsonArray, "Save must replace the legacy shape with Rules.");
            RegressionAssert.True(saved["AutoSwitch"]!["ExecutablePath"] is null, "Save must remove the obsolete top-level path.");
            var reloaded = ConfigStore.Load();
            RegressionAssert.Equal(rule.Id, reloaded.AutoSwitch.Rules[0].Id, "Card identity must survive a v2 save/reload.");
        });
    }

    private static void LegacyEnabledRuleMigratesWithoutSourceId()
    {
        var root = Source(Fixture());
        root["AutoSwitch"] = new JsonObject { ["Enabled"] = true, ["ExecutablePath"] = @"C:\Games\Game.exe" };
        WithConfiguration(root, loaded =>
        {
            RegressionAssert.True(loaded.AutoSwitch.Enabled, "The legacy global flag was lost.");
            RegressionAssert.Equal(1, loaded.AutoSwitch.Rules.Count, "Legacy source IDs must not be required.");
            RegressionAssert.True(loaded.AutoSwitch.Rules[0].Id != Guid.Empty, "Migration must generate a nonempty ID.");
            RegressionAssert.Equal("Game", loaded.AutoSwitch.Rules[0].Name, "Migration did not derive the game name.");
        });
    }

    private static void LegacyPresetOnlyRuleGetsFallbackName()
    {
        foreach (var property in new[] { "RunningPresetId", "StoppedPresetId" })
        {
            var original = Fixture();
            var root = Source(original);
            root["AutoSwitch"] = new JsonObject
            {
                ["Enabled"] = false,
                ["ExecutablePath"] = "",
                [property] = original.Presets[0].Id
            };
            WithConfiguration(root, loaded =>
            {
                RegressionAssert.Equal(1, loaded.AutoSwitch.Rules.Count, "A preset-only legacy rule must not be discarded.");
                RegressionAssert.Equal("游戏 1", loaded.AutoSwitch.Rules[0].Name, "An empty legacy path needs a named card.");
                RegressionAssert.Equal("", loaded.AutoSwitch.Rules[0].ExecutablePath, "Migration must not invent a path.");
            });
        }
    }

    private static void LegacyEmptyRuleDoesNotCreateCard()
    {
        var root = Source(Fixture());
        root["AutoSwitch"] = new JsonObject { ["Enabled"] = true, ["ExecutablePath"] = "" };
        WithConfiguration(root, loaded =>
        {
            RegressionAssert.True(loaded.AutoSwitch.Enabled, "An empty rule must still preserve the legacy global flag.");
            RegressionAssert.Equal(0, loaded.AutoSwitch.Rules.Count, "An unconfigured legacy rule must not become an empty card.");
        });
    }

    private static void MissingAutomationRemainsEmptyAndDisabled()
    {
        var root = Source(Fixture());
        root.Remove("AutoSwitch");
        WithConfiguration(root, loaded =>
        {
            RegressionAssert.True(!loaded.AutoSwitch.Enabled, "Missing automation must default to disabled.");
            RegressionAssert.Equal(0, loaded.AutoSwitch.Rules.Count, "Missing automation must not inject a card.");
            ConfigStore.Save(loaded);
            RegressionAssert.Equal(0, ConfigStore.Load().AutoSwitch.Rules.Count, "Saving missing automation must retain an empty rule list.");
        });
    }

    private static void ExplicitEmptyRulesSurviveSaveAndReload()
    {
        var root = Source(Fixture());
        root["AutoSwitch"] = new JsonObject { ["Enabled"] = true, ["Rules"] = new JsonArray() };
        WithConfiguration(root, loaded =>
        {
            RegressionAssert.Equal(0, loaded.AutoSwitch.Rules.Count, "Explicit Rules:[] must not be mistaken for legacy configuration.");
            ConfigStore.Save(loaded);
            var reloaded = ConfigStore.Load();
            RegressionAssert.True(reloaded.AutoSwitch.Enabled, "Empty rules must not silently change the global flag.");
            RegressionAssert.Equal(0, reloaded.AutoSwitch.Rules.Count, "An empty card list must survive reload.");
        });
    }

    private static void NewRulesRoundTripOrderAndIdentity()
    {
        var original = Fixture();
        original.AutoSwitch.Enabled = true;
        original.AutoSwitch.Rules =
        [
            new() { Name = "自定义名称 A", ExecutablePath = @"C:\A\Same.exe", RunningPresetId = original.Presets[0].Id, StoppedPresetId = original.Presets[1].Id },
            new() { Name = "自定义名称 B", Enabled = false, ExecutablePath = @"C:\B\Same.exe", RunningPresetId = original.Presets[1].Id }
        ];
        WithConfiguration(Source(original), loaded =>
        {
            ConfigStore.Save(loaded);
            var reloaded = ConfigStore.Load();
            RegressionAssert.Equal(2, reloaded.AutoSwitch.Rules.Count, "New rules were lost.");
            for (var index = 0; index < original.AutoSwitch.Rules.Count; index++)
            {
                var expected = original.AutoSwitch.Rules[index];
                var actual = reloaded.AutoSwitch.Rules[index];
                RegressionAssert.Equal(expected.Id, actual.Id, "Priority order or stable rule identity changed.");
                RegressionAssert.Equal(expected.Name, actual.Name, "A custom game name changed.");
                RegressionAssert.Equal(expected.Enabled, actual.Enabled, "Per-rule enablement changed.");
                RegressionAssert.Equal(expected.ExecutablePath, actual.ExecutablePath, "A target path changed.");
                RegressionAssert.Equal(expected.RunningPresetId, actual.RunningPresetId, "A running preset reference changed.");
                RegressionAssert.Equal(expected.StoppedPresetId, actual.StoppedPresetId, "An exit preset reference changed.");
            }
            for (var index = 0; index < original.Presets.Count; index++) SamePreset(original.Presets[index], reloaded.Presets[index]);
        });
    }

    private static void DanglingPresetReferencesRemainRepairable()
    {
        var root = NewSource();
        var missing = Guid.NewGuid();
        RuleObject(root)["RunningPresetId"] = missing;
        RuleObject(root)["StoppedPresetId"] = missing;
        WithConfiguration(root, loaded =>
        {
            RegressionAssert.Equal<Guid?>(missing, loaded.AutoSwitch.Rules[0].RunningPresetId, "Storage must retain dangling references for editor repair.");
            ConfigStore.Save(loaded);
            RegressionAssert.Equal<Guid?>(missing, ConfigStore.Load().AutoSwitch.Rules[0].StoppedPresetId, "Storage must not silently remove dangling references.");
        });
    }


    private static void SaveRejectsInvalidModelsWithoutReplacingFile()
    {
        WithConfiguration(NewSource(), loaded =>
        {
            var originalText = File.ReadAllText(ConfigStore.FilePath);
            var invalid = loaded.Copy();
            invalid.AutoSwitch.Rules.Add(invalid.AutoSwitch.Rules[0].Copy());
            RegressionAssert.Throws<InvalidDataException>(() => ConfigStore.Save(invalid), "Save accepted duplicate rule identities.");
            invalid = loaded.Copy();
            invalid.AutoSwitch.Rules[0].Id = Guid.Empty;
            RegressionAssert.Throws<InvalidDataException>(() => ConfigStore.Save(invalid), "Save accepted an empty rule identity.");
            invalid = loaded.Copy();
            invalid.AutoSwitch.Rules[0].ExecutablePath = null!;
            RegressionAssert.Throws<InvalidDataException>(() => ConfigStore.Save(invalid), "Save accepted a null path.");
            invalid = loaded.Copy();
            invalid.AutoSwitch.Rules = null!;
            RegressionAssert.Throws<InvalidDataException>(() => ConfigStore.Save(invalid), "Save accepted a null rule list.");
            RegressionAssert.Equal(originalText, File.ReadAllText(ConfigStore.FilePath), "Failed saves must not replace the original configuration.");
        });
    }


    private static IEnumerable<(string Name, Action<JsonObject> Mutate)> InvalidSources()
    {
        yield return ("RejectsNullAutomation", root => root["AutoSwitch"] = null);
        yield return ("RejectsNullRules", root => root["AutoSwitch"]!["Rules"] = null);
        yield return ("RejectsNonArrayRules", root => root["AutoSwitch"]!["Rules"] = new JsonObject());
        yield return ("RejectsNullRule", root => root["AutoSwitch"]!["Rules"]![0] = null);
        yield return ("RejectsMissingRuleId", root => RuleObject(root).Remove("Id"));
        yield return ("RejectsEmptyRuleId", root => RuleObject(root)["Id"] = Guid.Empty);
        yield return ("RejectsNullRuleId", root => RuleObject(root)["Id"] = null);
        yield return ("RejectsMalformedRuleId", root => RuleObject(root)["Id"] = "not-a-guid");
        yield return ("RejectsDuplicateRuleIds", root => root["AutoSwitch"]!["Rules"]!.AsArray().Add(RuleObject(root).DeepClone()));
        yield return ("RejectsMissingRuleName", root => RuleObject(root).Remove("Name"));
        yield return ("RejectsNullRuleName", root => RuleObject(root)["Name"] = null);
        yield return ("RejectsNonStringRuleName", root => RuleObject(root)["Name"] = 123);
        yield return ("RejectsNullRulePath", root => RuleObject(root)["ExecutablePath"] = null);
        yield return ("RejectsNullPresets", root => root["Presets"] = null);
        yield return ("RejectsMissingPresets", root => root.Remove("Presets"));
        yield return ("RejectsMissingPresetId", root => root["Presets"]![0]!.AsObject().Remove("Id"));
        yield return ("RejectsDuplicatePresetIds", root => root["Presets"]!.AsArray().Add(root["Presets"]![0]!.DeepClone()));
    }
}
