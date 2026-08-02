#nullable enable

using System.CommandLine;

namespace AiNetLinter.Cli;

internal sealed record CliOptions(
    Option<string?> Config,
    Option<string?> Path,
    Option<string?> Playbook,
    Option<bool> Verbose,
    Option<string?> CreateBaseline,
    Option<string?> Baseline,
    Option<bool> AddDisableAll,
    Option<bool> RemoveDisableAll,
    Option<bool> DebtReport,
    Option<bool> WaveReady,
    Option<bool> OnlyChanged,
    Option<string?> GitSince,
    Option<bool> Fix,
    Option<string?> Impact,
    Option<bool> SyncAgentRules,
    Option<bool> SyncAgentRulesOnly,
    Option<string?> AgentRulesPath,
    Option<bool> Check,
    Option<bool> NoCache,
    Option<int> CacheTtl,
    Option<string?> Footprint,
    Option<string?> Docs,
    Option<bool> ListRules,
    Option<string?> DescribeRule,
    Option<string?> SearchRules,
    Option<string?> Map,
    Option<string?> Eval,
    Option<bool> ListEvals,
    Option<string[]> Spec,
    Option<string[]> IncludeProjects,
    Option<string[]> ExcludeProjects,
    Option<string[]> IncludeNamespaces,
    Option<string[]> ExcludeNamespaces,
    Option<bool> ExcludeTests,
    Option<bool> TestsOnly,
    Option<bool> PublicOnly,
    Option<string[]> IgnoreSuppressions,
    Option<bool> McpServer);

internal sealed record CliOutputOptions(
    string? PlaybookPath,
    bool Verbose);

internal sealed record CliBaselineOptions(
    string? CreateBaselinePath,
    string? BaselinePath,
    bool OnlyChanged);

internal sealed record CliMaintenanceOptions(
    bool AddDisableAll,
    bool RemoveDisableAll);

internal sealed record CliScopeOptions(
    bool WaveReady,
    string? GitSince);

internal sealed record CliImpactOptions(
    bool HasImpact,
    string? ImpactRef);

internal sealed record CliParsedArgs(
    string? ConfigPath,
    string TargetPath,
    CliOutputOptions Output,
    CliBaselineOptions Baseline,
    CliMaintenanceOptions Maintenance,
    CliScopeOptions Scope,
    bool DebtReport,
    bool Fix,
    CliImpactOptions Impact,
    bool SyncAgentRules,
    bool SyncAgentRulesOnly,
    string? AgentRulesPath,
    bool Check,
    bool NoCache,
    int CacheTtlMinutes,
    string? Footprint,
    string? Docs,
    bool ListRules,
    string? DescribeRule,
    string? SearchRules,
    string? MapType,
    string? EvalType,
    bool ListEvals,
    IReadOnlyList<string> SpecPaths,
    IReadOnlyList<string> IncludeProjects,
    IReadOnlyList<string> ExcludeProjects,
    IReadOnlyList<string> IncludeNamespaces,
    IReadOnlyList<string> ExcludeNamespaces,
    bool ExcludeTests,
    bool TestsOnly,
    bool PublicOnly,
    IReadOnlyList<string>? IgnoreSuppressions,
    bool McpServer);
