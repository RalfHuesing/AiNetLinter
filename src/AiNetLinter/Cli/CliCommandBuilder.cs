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
            options.Config, options.Path, options.Verbose,
            options.CreateBaseline, options.Baseline, options.AddDisableAll, options.RemoveDisableAll,
            options.WaveReady, options.OnlyChanged,
            options.Fix, options.SyncAgentRules, options.SyncAgentRulesOnly, options.AgentRulesPath, options.NoCache, options.CacheTtl,
            options.Docs,
            options.ListRules, options.DescribeRule, options.SearchRules,
            options.McpServer,
            options.ParentPid,
            options.McpProjectTtlMinutes,
            options.McpMaxProjects,
            options.McpExternalMaxDiskBytes,
            options.McpExternalMaxMemoryBytes,
            options.McpExternalMaxParallelOperations,
            options.McpExternalMaxResidentResources,
            options.McpExternalIdleTtlMinutes,
            options.DaemonStart,
            options.McpDaemonIdleExitMinutes,
            options.DaemonInstance,
        };

        return (root, options);
    }

    private static CliOptions CreateOptions()
    {
        return new CliOptions(
            CliOptionFactory.CreateConfigOption(),
            CliOptionFactory.CreatePathOption(),
            CliOptionFactory.CreateVerboseOption(),
            CliOptionFactory.CreateBaselineCreateOption(),
            CliOptionFactory.CreateBaselineOption(),
            CliOptionFactory.CreateAddDisableAllOption(),
            CliOptionFactory.CreateRemoveDisableAllOption(),
            CliOptionFactory.CreateWaveReadyOption(),
            CliOptionFactory.CreateOnlyChangedOption(),
            CliOptionFactory.CreateFixOption(),
            CliOptionFactory.CreateSyncAgentRulesOption(),
            CliOptionFactory.CreateSyncAgentRulesOnlyOption(),
            CliOptionFactory.CreateAgentRulesPathOption(),
            CliOptionFactory.CreateNoCacheOption(),
            CliOptionFactory.CreateCacheTtlOption(),
            CliOptionFactory.CreateDocsOption(),
            CliOptionFactory.CreateListRulesOption(),
            CliOptionFactory.CreateDescribeRuleOption(),
            CliOptionFactory.CreateSearchRulesOption(),
            CliOptionFactory.CreateMcpServerOption(),
            CliOptionFactory.CreateParentPidOption(),
            CliOptionFactory.CreateMcpProjectTtlOption(),
            CliOptionFactory.CreateMcpMaxProjectsOption(),
            CliOptionFactory.CreateMcpExternalMaxDiskBytesOption(),
            CliOptionFactory.CreateMcpExternalMaxMemoryBytesOption(),
            CliOptionFactory.CreateMcpExternalMaxParallelOperationsOption(),
            CliOptionFactory.CreateMcpExternalMaxResidentResourcesOption(),
            CliOptionFactory.CreateMcpExternalIdleTtlOption(),
            CliOptionFactory.CreateDaemonStartOption(),
            CliOptionFactory.CreateMcpDaemonIdleExitOption(),
            CliOptionFactory.CreateDaemonInstanceOption());
    }

    internal static CliParsedArgs Parse(ParseResult parseResult, CliOptions options)
    {
        return new CliParsedArgs(
            ConfigPath: parseResult.GetValue(options.Config),
            TargetPath: parseResult.GetValue(options.Path) ?? "",
            Output: new CliOutputOptions(
                Verbose: parseResult.GetValue(options.Verbose)),
            Baseline: new CliBaselineOptions(
                CreateBaselinePath: parseResult.GetValue(options.CreateBaseline),
                BaselinePath: parseResult.GetValue(options.Baseline),
                OnlyChanged: parseResult.GetValue(options.OnlyChanged)),
            Maintenance: new CliMaintenanceOptions(
                AddDisableAll: parseResult.GetValue(options.AddDisableAll),
                RemoveDisableAll: parseResult.GetValue(options.RemoveDisableAll)),
            Scope: new CliScopeOptions(
                WaveReady: parseResult.GetValue(options.WaveReady)),
            Fix: parseResult.GetValue(options.Fix),
            SyncAgentRules: parseResult.GetValue(options.SyncAgentRules),
            SyncAgentRulesOnly: parseResult.GetValue(options.SyncAgentRulesOnly),
            AgentRulesPath: parseResult.GetValue(options.AgentRulesPath),
            NoCache: parseResult.GetValue(options.NoCache),
            CacheTtlMinutes: parseResult.GetValue(options.CacheTtl),
            Docs: parseResult.GetValue(options.Docs),
            ListRules: parseResult.GetValue(options.ListRules),
            DescribeRule: parseResult.GetValue(options.DescribeRule),
            SearchRules: parseResult.GetValue(options.SearchRules),
            McpServer: parseResult.GetValue(options.McpServer),
            ParentPid: parseResult.GetValue(options.ParentPid),
            McpProjectTtlMinutes: parseResult.GetValue(options.McpProjectTtlMinutes),
            McpMaxProjects: parseResult.GetValue(options.McpMaxProjects),
            McpExternalMaxDiskBytes: parseResult.GetValue(options.McpExternalMaxDiskBytes),
            McpExternalMaxMemoryBytes: parseResult.GetValue(options.McpExternalMaxMemoryBytes),
            McpExternalMaxParallelOperations: parseResult.GetValue(options.McpExternalMaxParallelOperations),
            McpExternalMaxResidentResources: parseResult.GetValue(options.McpExternalMaxResidentResources),
            McpExternalIdleTtlMinutes: parseResult.GetValue(options.McpExternalIdleTtlMinutes),
            DaemonStart: parseResult.GetValue(options.DaemonStart),
            McpDaemonIdleExitMinutes: parseResult.GetValue(options.McpDaemonIdleExitMinutes),
            DaemonInstance: parseResult.GetValue(options.DaemonInstance));
    }
}
