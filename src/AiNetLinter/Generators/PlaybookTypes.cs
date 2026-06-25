#nullable enable

using System.Collections.Generic;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

namespace AiNetLinter.Generators;

/// <summary>
/// Optionen für die Playbook-Generierung.
/// </summary>
public sealed record PlaybookOptions(
    bool Verbose = false,
    Config? Config = null,
    string ConfigPath = "rules.json",
    IReadOnlyCollection<RuleViolation>? PrecomputedViolations = null);

internal sealed record PlaybookDocInfo(
    string FilePath,
    string ProjectName,
    bool HasDisableAll,
    int LineCount,
    List<string> Namespaces
);

internal sealed record PlaybookDocScanResult(
    int ResultMethods,
    int Throws,
    bool HasDisableAll,
    int LineCount,
    List<string> Namespaces
);

internal sealed record PlaybookStats(
    int TotalResultMethods,
    int TotalThrows,
    Dictionary<string, int> SuppressionCounts,
    List<PlaybookDocInfo> DocInfos,
    List<RuleViolation> Violations
);

internal sealed record PlaybookBuildContext(
    PlaybookStats Stats,
    string SolutionDir,
    Config? Config,
    string ConfigPath,
    string Version);
