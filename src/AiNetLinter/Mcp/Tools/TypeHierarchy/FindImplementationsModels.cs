#nullable enable

using System.Collections.Generic;
using System.Threading;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.SymbolGraph;

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
    string DisplayLocation,
    string? Id = null,
    string? HandoffKind = null,
    string? ScopeType = null,
    string? SourceKind = null);

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
    IReadOnlyList<string> TruncationReasons,
    FindSymbolScopeDto? Scope = null);

internal sealed record FindImplementationsRequest(
    ISolutionStateProvider State,
    string? SymbolIdentifier,
    int MaxResults,
    McpScopeType ScopeType,
    bool IncludeGenerated,
    CancellationToken CancellationToken,
    int MaxResponseBytes = FindImplementationsTool.DefaultMaxResponseBytes);
