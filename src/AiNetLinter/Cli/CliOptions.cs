#nullable enable

using System.CommandLine;

namespace AiNetLinter.Cli;

/// <summary>
/// Sammelt die Option-Definitionen fuer die System.CommandLine-Bindings.
/// </summary>
internal sealed record CliOptions(
    Option<string?> Config,
    Option<string?> Path,
    Option<bool> Verbose,
    Option<string?> CreateBaseline,
    Option<string?> Baseline,
    Option<bool> AddDisableAll,
    Option<bool> RemoveDisableAll,
    Option<bool> WaveReady,
    Option<bool> OnlyChanged,
    Option<bool> Fix,
    Option<bool> SyncAgentRules,
    Option<bool> SyncAgentRulesOnly,
    Option<string?> AgentRulesPath,
    Option<bool> NoCache,
    Option<int> CacheTtl,
    Option<string?> Docs,
    Option<bool> ListRules,
    Option<string?> DescribeRule,
    Option<string?> SearchRules,
    Option<bool> McpServer,
    Option<int?> ParentPid,
    Option<decimal?> McpProjectTtlMinutes,
    Option<int?> McpMaxProjects,
    Option<bool> DaemonStart,
    Option<decimal?> McpDaemonIdleExitMinutes);

/// <summary>
/// Aufgeloeste Output-Optionen (Verbose).
/// </summary>
internal sealed record CliOutputOptions(
    bool Verbose);

/// <summary>
/// Aufgeloeste Baseline-Optionen (CreateBaseline, Baseline, OnlyChanged).
/// </summary>
internal sealed record CliBaselineOptions(
    string? CreateBaselinePath,
    string? BaselinePath,
    bool OnlyChanged);

/// <summary>
/// Aufgeloeste Maintenance-Optionen (AddDisableAll, RemoveDisableAll).
/// </summary>
internal sealed record CliMaintenanceOptions(
    bool AddDisableAll,
    bool RemoveDisableAll);

/// <summary>
/// Aufgeloeste Scope-Optionen (WaveReady).
/// </summary>
internal sealed record CliScopeOptions(
    bool WaveReady);

/// <summary>
/// Vollstaendig aufgeloestes ParsedArgs-Aggregat nach dem CLI-Parse-Schritt.
/// </summary>
internal sealed record CliParsedArgs(
    string? ConfigPath,
    string TargetPath,
    CliOutputOptions Output,
    CliBaselineOptions Baseline,
    CliMaintenanceOptions Maintenance,
    CliScopeOptions Scope,
    bool Fix,
    bool SyncAgentRules,
    bool SyncAgentRulesOnly,
    string? AgentRulesPath,
    bool NoCache,
    int CacheTtlMinutes,
    string? Docs,
    bool ListRules,
    string? DescribeRule,
    string? SearchRules,
    bool McpServer,
    int? ParentPid,
    decimal? McpProjectTtlMinutes,
    int? McpMaxProjects,
    bool DaemonStart,
    decimal? McpDaemonIdleExitMinutes);
