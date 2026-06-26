#nullable enable

using System.CommandLine;

namespace AiNetLinter.Cli;

/// <summary>
/// Erzeugt die System.CommandLine-Definition für die AiNetLinter-CLI.
/// </summary>
internal static class CliCommandBuilder
{
    internal static (RootCommand Root, CliOptions Options) Build()
    {
        var options = CreateOptions();
        var root = new RootCommand("AiNetLinter - CLI-Linter für AI-optimierten .NET Code")
        {
            options.Config, options.Path, options.Playbook, options.Verbose,
            options.CreateBaseline, options.Baseline, options.AddDisableAll, options.RemoveDisableAll,
            options.DebtReport, options.WaveReady, options.OnlyChanged, options.GitSince,
            options.Fix, options.Impact, options.SyncCursorRules, options.Check, options.NoCache, options.CacheTtl,
            options.Footprint, options.Docs,
            options.ListRules, options.DescribeRule, options.SearchRules, options.Map,
            options.Eval, options.ListEvals, options.Spec,
            options.IncludeProjects, options.ExcludeProjects, options.IncludeNamespaces, options.ExcludeNamespaces,
            options.ExcludeTests, options.TestsOnly, options.PublicOnly,
        };

        return (root, options);
    }

    private static CliOptions CreateOptions()
    {
        return new CliOptions(
            CliOptionFactory.CreateConfigOption(),
            CliOptionFactory.CreatePathOption(),
            CliOptionFactory.CreatePlaybookOption(),
            CliOptionFactory.CreateVerboseOption(),
            CliOptionFactory.CreateBaselineCreateOption(),
            CliOptionFactory.CreateBaselineOption(),
            CliOptionFactory.CreateAddDisableAllOption(),
            CliOptionFactory.CreateRemoveDisableAllOption(),
            CliOptionFactory.CreateDebtReportOption(),
            CliOptionFactory.CreateWaveReadyOption(),
            CliOptionFactory.CreateOnlyChangedOption(),
            CliOptionFactory.CreateGitSinceOption(),
            CliOptionFactory.CreateFixOption(),
            CliOptionFactory.CreateImpactOption(),
            CliOptionFactory.CreateSyncCursorRulesOption(),
            CliOptionFactory.CreateCheckOption(),
            CliOptionFactory.CreateNoCacheOption(),
            CliOptionFactory.CreateCacheTtlOption(),
            CliOptionFactory.CreateFootprintOption(),
            CliOptionFactory.CreateDocsOption(),
            CliOptionFactory.CreateListRulesOption(),
            CliOptionFactory.CreateDescribeRuleOption(),
            CliOptionFactory.CreateSearchRulesOption(),
            CliOptionFactory.CreateMapOption(),
            CliOptionFactory.CreateEvalOption(),
            CliOptionFactory.CreateListEvalsOption(),
            CliOptionFactory.CreateSpecOption(),
            CliOptionFactory.CreateIncludeProjectOption(),
            CliOptionFactory.CreateExcludeProjectOption(),
            CliOptionFactory.CreateIncludeNamespaceOption(),
            CliOptionFactory.CreateExcludeNamespaceOption(),
            CliOptionFactory.CreateExcludeTestsOption(),
            CliOptionFactory.CreateTestsOnlyOption(),
            CliOptionFactory.CreatePublicOnlyOption());
    }

    internal static CliParsedArgs Parse(ParseResult parseResult, CliOptions options)
    {
        return new CliParsedArgs(
            ConfigPath: parseResult.GetValue(options.Config),
            TargetPath: parseResult.GetValue(options.Path) ?? "",
            Output: new CliOutputOptions(
                PlaybookPath: parseResult.GetValue(options.Playbook),
                Verbose: parseResult.GetValue(options.Verbose)),
            Baseline: new CliBaselineOptions(
                CreateBaselinePath: parseResult.GetValue(options.CreateBaseline),
                BaselinePath: parseResult.GetValue(options.Baseline),
                OnlyChanged: parseResult.GetValue(options.OnlyChanged)),
            Maintenance: new CliMaintenanceOptions(
                AddDisableAll: parseResult.GetValue(options.AddDisableAll),
                RemoveDisableAll: parseResult.GetValue(options.RemoveDisableAll)),
            Scope: new CliScopeOptions(
                WaveReady: parseResult.GetValue(options.WaveReady),
                GitSince: parseResult.GetValue(options.GitSince)),
            DebtReport: parseResult.GetValue(options.DebtReport),
            Fix: parseResult.GetValue(options.Fix),
            Impact: new CliImpactOptions(
                HasImpact: parseResult.GetValue(options.Impact) is not null,
                ImpactRef: parseResult.GetValue(options.Impact)),
            SyncCursorRules: parseResult.GetValue(options.SyncCursorRules),
            Check: parseResult.GetValue(options.Check),
            NoCache: parseResult.GetValue(options.NoCache),
            CacheTtlMinutes: parseResult.GetValue(options.CacheTtl),
            Footprint: parseResult.GetValue(options.Footprint),
            Docs: parseResult.GetValue(options.Docs),
            ListRules: parseResult.GetValue(options.ListRules),
            DescribeRule: parseResult.GetValue(options.DescribeRule),
            SearchRules: parseResult.GetValue(options.SearchRules),
            MapType: parseResult.GetValue(options.Map),
            EvalType: parseResult.GetValue(options.Eval),
            ListEvals: parseResult.GetValue(options.ListEvals),
            SpecPaths: parseResult.GetValue(options.Spec) ?? [],
            IncludeProjects: ParseCommaSeparated(parseResult.GetValue(options.IncludeProjects)),
            ExcludeProjects: ParseCommaSeparated(parseResult.GetValue(options.ExcludeProjects)),
            IncludeNamespaces: ParseCommaSeparated(parseResult.GetValue(options.IncludeNamespaces)),
            ExcludeNamespaces: ParseCommaSeparated(parseResult.GetValue(options.ExcludeNamespaces)),
            ExcludeTests: parseResult.GetValue(options.ExcludeTests),
            TestsOnly: parseResult.GetValue(options.TestsOnly),
            PublicOnly: parseResult.GetValue(options.PublicOnly));
    }

    private static System.Collections.Generic.IReadOnlyList<string> ParseCommaSeparated(string[]? values)
    {
        if (values == null || values.Length == 0) return System.Array.Empty<string>();
        var list = new System.Collections.Generic.List<string>();
        foreach (var val in values)
        {
            if (string.IsNullOrWhiteSpace(val)) continue;
            foreach (var split in val.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries))
            {
                list.Add(split);
            }
        }
        return list;
    }
}
