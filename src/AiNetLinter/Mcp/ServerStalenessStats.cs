#nullable enable

namespace AiNetLinter.Mcp;

/// <summary>
/// Diagnose-Schnappschuss des Staleness-Subsystems (Konzept 02, c): Check-Anzahl und
/// kumulierte Dauer als Evidenzbasis fuer Kosten/Frequenz, Warnungszähler und letzte
/// Warnmeldung fuer unzugängliche Teilbäume (Konzept 02, C). Ein Record statt vier
/// einzelner Properties haelt die oeffentliche API-Oberflaeche von
/// <see cref="McpCodeGraphServer"/> unter dem MaxPublicMembersPerType-Limit.
/// </summary>
internal sealed record ServerStalenessStats(
    long CheckCount,
    double TotalMilliseconds,
    int WarningCount,
    string? LastWarning);
