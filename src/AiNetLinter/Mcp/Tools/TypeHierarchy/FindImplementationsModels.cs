#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.TypeHierarchy;

/// <summary>
/// Beschreibt eine gefundene konkrete oder abgeleitete Implementierung / ein Override.
/// </summary>
public sealed record ImplementationItemDto(
    string TypeName,
    string? MemberName,
    string Kind,
    string Status,
    string? FilePath,
    int? Line,
    int? Column,
    string DisplayLocation);

/// <summary>
/// Strukturiertes Ergebnis für das MCP-Tool <c>find_implementations</c>.
/// </summary>
public sealed record FindImplementationsResultDto(
    string TargetSymbol,
    string TargetKind,
    IReadOnlyList<ImplementationItemDto> Implementations,
    int TotalCount,
    int ShownCount,
    bool IsTruncated,
    IReadOnlyList<string> TruncationReasons);
