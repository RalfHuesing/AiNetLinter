using System.CommandLine;

namespace AiNetLinter.Cli;

/// <summary>
/// Sammelt die Option-Definitionen fuer die System.CommandLine-Bindings.
/// </summary>
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
    Option<bool> SyncCursorRules,
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
    Option<string[]> Spec);

/// <summary>
/// Aufgeloeste Output-Optionen (Playbook, Verbose).
/// </summary>
internal sealed record CliOutputOptions(
    string? PlaybookPath,
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
/// Aufgeloeste Scope-Optionen (WaveReady, GitSince).
/// </summary>
internal sealed record CliScopeOptions(
    bool WaveReady,
    string? GitSince);

/// <summary>
/// Aufgeloeste Impact-Optionen (HasImpact, ImpactRef).
/// </summary>
internal sealed record CliImpactOptions(
    bool HasImpact,
    string? ImpactRef);

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
    bool DebtReport,
    bool Fix,
    CliImpactOptions Impact,
    bool SyncCursorRules,
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
    IReadOnlyList<string> SpecPaths);
