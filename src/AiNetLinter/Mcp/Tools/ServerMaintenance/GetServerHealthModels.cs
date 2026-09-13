#nullable enable

using System.Collections.Generic;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

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

internal sealed record DaemonHealthPayload(
    string Mode,
    int ConnectionId,
    int Connections,
    int ProcessId,
    double UptimeSeconds,
    IReadOnlyList<string> Keys,
    string DaemonVersion,
    string? DaemonProfile = null);
