#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

// Ergebnis-Records fuer GetServerHealthTools StructuredContent. Pattern 1:1 von
// SafeguardModels.cs. Reine Datentraeger ohne eigenes Verhalten.

/// <summary>
/// StructuredContent-Payload fuer <c>get_server_health</c> — dieselben Scalar-Felder wie der
/// Markdown-Text (<see cref="GetServerHealthTool"/>), zusaetzlich maschinenlesbar.
/// <see cref="CallLog"/> ist <see langword="null"/>, wenn <c>--mcp-log</c> beim Start nicht gesetzt
/// wurde (identisch zur Text-Fallunterscheidung in <c>DescribeCallLog</c>).
/// </summary>
internal sealed record ServerHealthPayload(
    string LoadState,
    string? SolutionPath,
    bool UsedDefaultConfig,
    string? ConfigPath,
    double UptimeSeconds,
    int RefreshCount,
    CallLogPayload? CallLog);

/// <summary>
/// Call-Log-Aggregat-Teil von <see cref="ServerHealthPayload"/> — 1:1-Mapping der bereits
/// vorhandenen <c>McpCallLog</c>-Aggregate (<see cref="McpCallLog.EntryCount"/>,
/// <see cref="McpCallLog.ErrorCount"/>, <see cref="McpCallLog.CallCountsByTool"/>).
/// </summary>
internal sealed record CallLogPayload(
    string LogPath,
    int EntryCount,
    int ErrorCount,
    IReadOnlyDictionary<string, int> CallCountsByTool);
