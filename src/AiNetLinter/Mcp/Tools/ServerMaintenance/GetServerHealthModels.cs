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
    string ProjectRoot,
    string LoadState,
    string? SolutionPath,
    bool UsedDefaultConfig,
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
    string? SourceProjectPath,
    SourceSnapshotIdentity? SourceSnapshot,
    string? ContentHash,
    string? GeneratedDocumentPath,
    string? Confidence,
    string? Trust,
    long? Generation,
    IReadOnlyList<string>? Diagnostics,
    AssemblyDiagnosticsSummary? DiagnosticsSummary = null,
    string? Completeness = null,
    IReadOnlyList<string>? TransitiveDiagnostics = null);

/// <summary>
/// StructuredContent-Payload fuer <c>get_server_health</c>: ein Eintrag je residentem
/// Projekt-Key und optional die Laufzeitdaten des Daemons.
/// </summary>
internal sealed record ServerHealthAggregatePayload(
    string Version,
    IReadOnlyList<ProjectHealthEntry> Projects,
    DaemonHealthPayload? Daemon = null,
    IReadOnlyList<AssemblyHealthEntry>? Assemblies = null,
    bool DiagnosticsIncluded = false,
    int DiagnosticLimit = AssemblyAnalysisResponseLimits.DefaultMaxDiagnostics);

internal sealed record DaemonHealthPayload(
    string Mode,
    int ConnectionId,
    int Connections,
    int ProcessId,
    double UptimeSeconds,
    IReadOnlyList<string> Keys,
    string DaemonVersion);
