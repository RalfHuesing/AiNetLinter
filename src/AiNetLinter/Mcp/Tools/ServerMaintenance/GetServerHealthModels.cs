#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

// Ergebnis-Records fuer GetServerHealthTools StructuredContent. Pattern 1:1 von
// SafeguardModels.cs. Reine Datentraeger ohne eigenes Verhalten.

/// <summary>
/// StructuredContent-Payload fuer <c>get_server_health</c> — dieselben Scalar-Felder wie der
/// Markdown-Text (<see cref="GetServerHealthTool"/>), zusaetzlich maschinenlesbar.
/// <see cref="CallLog"/> ist <see langword="null"/>, wenn Observability deaktiviert ist oder kein
/// Log-Pfad vom Observability-Dienst bereitgestellt wird.
/// </summary>
internal sealed record ServerHealthPayload(
    string Version,
    string LoadState,
    string? SolutionPath,
    bool UsedDefaultConfig,
    string? ConfigPath,
    double UptimeSeconds,
    int RefreshCount,
    long StalenessCheckCount,
    double StalenessCheckDurationMs,
    int StalenessWarningCount,
    string? LastStalenessWarning,
    CallLogPayload? CallLog);

/// <summary>
/// Call-Log-Aggregat-Teil von <see cref="ServerHealthPayload"/>. Die Werte stammen aus dem
/// aktuell geoeffneten JSONL-Call-Log und werden mit demselben Auswerter wie beim Offline-CLI-
/// Kommando berechnet.
/// </summary>
internal sealed record CallLogPayload(
    string LogPath,
    int EntryCount,
    int ErrorCount,
    IReadOnlyDictionary<string, int> CallCountsByTool,
    string? AnalysisError = null);
