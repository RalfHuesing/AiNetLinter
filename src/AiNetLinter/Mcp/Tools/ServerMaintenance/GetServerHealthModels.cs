#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

// Ergebnis-Records fuer GetServerHealthTools StructuredContent. Pattern 1:1 von
// SafeguardModels.cs. Reine Datentraeger ohne eigenes Verhalten.

/// <summary>
/// Zustand eines einzelnen Projekt-Keys in <see cref="ServerHealthAggregatePayload"/>:
/// dieselben Scalar-Felder wie der Markdown-Projektabschnitt (<see cref="GetServerHealthTool"/>),
/// zusaetzlich maschinenlesbar inklusive der Felder des zweistufigen Zustandsvertrags
/// (<see cref="LastGoodStateUtc"/>/<see cref="LastLoadError"/>).
/// </summary>
internal sealed record ProjectHealthEntry(
    string TargetPath,
    string LoadState,
    string? SolutionPath,
    string? ConfigPath,
    DateTime? LastUsedUtc,
    double UptimeSeconds,
    int RefreshCount,
    long StalenessCheckCount,
    double StalenessCheckDurationMs,
    int StalenessWarningCount,
    string? LastStalenessWarning,
    DateTime? LastGoodStateUtc,
    string? LastLoadError);

/// <summary>
/// Zustand einer residenten Assembly- oder Source-Project-Session. Die Herkunfts- und
/// Snapshot-Felder entsprechen dem Metadata-Vertrag der Assembly-Tools.
/// </summary>
internal sealed record AssemblyHealthEntry(
    string TargetPath,
    string LoadState,
    string? OriginKind,
    string? ContentHash,
    string? GeneratedDocumentPath,
    string? Confidence,
    long? Generation,
    IReadOnlyList<string>? Diagnostics,
    AssemblyDiagnosticsSummary? DiagnosticsSummary = null,
    string? Completeness = null,
    IReadOnlyList<string>? TransitiveDiagnostics = null,
    string? DaemonProfile = null,
    string? LockStatus = null,
    string? LeaseStatus = null,
    string? CleanupStatus = null,
    string? ErrorCode = null,
    string? ErrorPhase = null,
    string? ErrorCause = null,
    string? NextAction = null);

/// <summary>Stable public health projection without cache, workspace or generation fields.</summary>
internal sealed record AssemblyHealthPublicEntry(
    string TargetPath,
    string LoadState,
    string? OriginKind,
    string? ContentHash,
    string? Confidence,
    AssemblyDiagnosticsSummary? DiagnosticsSummary = null,
    string? Completeness = null,
    IReadOnlyList<string>? Diagnostics = null,
    string? DaemonProfile = null,
    string? LockStatus = null,
    string? LeaseStatus = null,
    string? CleanupStatus = null,
    string? ErrorCode = null,
    string? ErrorPhase = null,
    string? ErrorCause = null,
    string? NextAction = null);

/// <summary>
/// StructuredContent-Payload fuer <c>get_server_health</c>: ein Eintrag je residentem
/// Projekt-Key und optional die Laufzeitdaten des Daemons.
/// </summary>
internal sealed record ServerHealthAggregatePayload(
    string Version,
    IReadOnlyList<ProjectHealthEntry> Projects,
    string? Repository = null,
    DaemonHealthPayload? Daemon = null,
    IReadOnlyList<AssemblyHealthPublicEntry>? Assemblies = null,
    bool DiagnosticsIncluded = false,
    int DiagnosticLimit = AssemblyAnalysisResponseLimits.DefaultMaxDiagnostics,
    bool SessionsIncluded = false,
    int TotalAssemblySessions = 0,
    int ShownSessionCount = 0,
    bool SessionsTruncated = false,
    IReadOnlyList<string>? SessionsTruncatedBy = null,
    IReadOnlyDictionary<string, int>? AssemblyStatusCounts = null,
    int AssemblyDiagnosticCount = 0);

internal sealed record DaemonHealthPayload(
    string Mode,
    int ConnectionId,
    int Connections,
    int ProcessId,
    double UptimeSeconds,
    IReadOnlyList<string> Keys,
    string DaemonVersion,
    string? DaemonProfile = null);

/// <summary>Target-bound health projection without process-wide daemon or aggregate fields.</summary>
internal sealed record TargetHealthPayload(
    TargetProjectHealthEntry? Project,
    TargetAssemblyHealthEntry? Assembly,
    bool DiagnosticsIncluded,
    int DiagnosticLimit);

internal sealed record TargetProjectHealthEntry(
    string TargetPath,
    string LoadState,
    string? SolutionPath,
    string? ConfigPath,
    DateTime? LastUsedUtc,
    int RefreshCount,
    long StalenessCheckCount,
    double StalenessCheckDurationMs,
    int StalenessWarningCount,
    string? LastStalenessWarning,
    DateTime? LastGoodStateUtc,
    string? LastLoadError);

internal sealed record TargetAssemblyHealthEntry(
    string TargetPath,
    string LoadState,
    string? OriginKind,
    string? ContentHash,
    string? Confidence,
    AssemblyDiagnosticsSummary? DiagnosticsSummary,
    string? Completeness,
    IReadOnlyList<string>? Diagnostics,
    string? LockStatus,
    string? LeaseStatus,
    string? CleanupStatus,
    string? ErrorCode,
    string? ErrorPhase,
    string? ErrorCause,
    string? NextAction);
