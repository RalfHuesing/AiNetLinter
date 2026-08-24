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
            options.Fix, options.Impact, options.SyncAgentRules, options.SyncAgentRulesOnly, options.AgentRulesPath, options.Check, options.NoCache, options.CacheTtl,
            options.Footprint, options.Docs,
            options.ListRules, options.DescribeRule, options.SearchRules,
            options.IncludeProjects, options.ExcludeProjects, options.IncludeNamespaces, options.ExcludeNamespaces,
            options.ExcludeTests, options.TestsOnly, options.PublicOnly, options.IgnoreSuppressions,
            options.McpServer,
            options.McpLog,
            options.ParentPid,
            options.McpProjectTtlMinutes,
            options.McpMaxProjects,
            options.DaemonStart,
            options.McpDaemonIdleExitMinutes,
            options.AnalyzeMcpLog,
            options.Format,
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
            CliOptionFactory.CreateSyncAgentRulesOption(),
            CliOptionFactory.CreateSyncAgentRulesOnlyOption(),
            CliOptionFactory.CreateAgentRulesPathOption(),
            CliOptionFactory.CreateCheckOption(),
            CliOptionFactory.CreateNoCacheOption(),
            CliOptionFactory.CreateCacheTtlOption(),
            CliOptionFactory.CreateFootprintOption(),
            CliOptionFactory.CreateDocsOption(),
            CliOptionFactory.CreateListRulesOption(),
            CliOptionFactory.CreateDescribeRuleOption(),
            CliOptionFactory.CreateSearchRulesOption(),
            CliOptionFactory.CreateIncludeProjectOption(),
            CliOptionFactory.CreateExcludeProjectOption(),
            CliOptionFactory.CreateIncludeNamespaceOption(),
            CliOptionFactory.CreateExcludeNamespaceOption(),
            CliOptionFactory.CreateExcludeTestsOption(),
            CliOptionFactory.CreateTestsOnlyOption(),
            CliOptionFactory.CreatePublicOnlyOption(),
            CliOptionFactory.CreateIgnoreSuppressionsOption(),
            CliOptionFactory.CreateMcpServerOption(),
            CliOptionFactory.CreateMcpLogOption(),
            CliOptionFactory.CreateParentPidOption(),
            CliOptionFactory.CreateMcpProjectTtlOption(),
            CliOptionFactory.CreateMcpMaxProjectsOption(),
            CliOptionFactory.CreateDaemonStartOption(),
            CliOptionFactory.CreateMcpDaemonIdleExitOption(),
            CliOptionFactory.CreateAnalyzeMcpLogOption(),
            CliOptionFactory.CreateFormatOption());
    }

    internal static CliParsedArgs Parse(ParseResult parseResult, CliOptions options)
    {
        IReadOnlyList<string>? ignoreSuppressions = null;
        if (parseResult.GetResult(options.IgnoreSuppressions) is not null)
        {
            var rawValues = parseResult.GetValue(options.IgnoreSuppressions);
            var parsedList = ParseCommaSeparated(rawValues);
            ignoreSuppressions = parsedList.Count == 0 ? new[] { "all" } : parsedList;
        }

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
            SyncAgentRules: parseResult.GetValue(options.SyncAgentRules),
            SyncAgentRulesOnly: parseResult.GetValue(options.SyncAgentRulesOnly),
            AgentRulesPath: parseResult.GetValue(options.AgentRulesPath),
            Check: parseResult.GetValue(options.Check),
            NoCache: parseResult.GetValue(options.NoCache),
            CacheTtlMinutes: parseResult.GetValue(options.CacheTtl),
            Footprint: parseResult.GetValue(options.Footprint),
            Docs: parseResult.GetValue(options.Docs),
            ListRules: parseResult.GetValue(options.ListRules),
            DescribeRule: parseResult.GetValue(options.DescribeRule),
            SearchRules: parseResult.GetValue(options.SearchRules),
            IncludeProjects: ParseCommaSeparated(parseResult.GetValue(options.IncludeProjects)),
            ExcludeProjects: ParseCommaSeparated(parseResult.GetValue(options.ExcludeProjects)),
            IncludeNamespaces: ParseCommaSeparated(parseResult.GetValue(options.IncludeNamespaces)),
            ExcludeNamespaces: ParseCommaSeparated(parseResult.GetValue(options.ExcludeNamespaces)),
            ExcludeTests: parseResult.GetValue(options.ExcludeTests),
            TestsOnly: parseResult.GetValue(options.TestsOnly),
            PublicOnly: parseResult.GetValue(options.PublicOnly),
            IgnoreSuppressions: ignoreSuppressions,
            McpServer: parseResult.GetValue(options.McpServer),
            McpLog: parseResult.GetResult(options.McpLog) is null ? null : (parseResult.GetValue(options.McpLog) ?? string.Empty),
            ParentPid: parseResult.GetValue(options.ParentPid),
            McpProjectTtlMinutes: parseResult.GetValue(options.McpProjectTtlMinutes),
            McpMaxProjects: parseResult.GetValue(options.McpMaxProjects),
            DaemonStart: parseResult.GetValue(options.DaemonStart),
            McpDaemonIdleExitMinutes: parseResult.GetValue(options.McpDaemonIdleExitMinutes),
            AnalyzeMcpLog: parseResult.GetValue(options.AnalyzeMcpLog),
            Format: parseResult.GetValue(options.Format),
            FormatSpecified: parseResult.GetResult(options.Format) is { Implicit: false });
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
