#nullable enable

using System.Collections.Generic;
using AiNetLinter.Mcp.Scope;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record TypeHierarchyPayload(
    string TypeName,
    IReadOnlyList<TypeHierarchyEntryDto> BaseTypes,
    IReadOnlyList<TypeHierarchyEntryDto> Interfaces,
    string SubtypeHeading,
    IReadOnlyList<TypeHierarchyEntryDto> Subtypes,
    int TotalSubtypeCount,
    int ShownSubtypeCount,
    bool SubtypesTruncated,
    IReadOnlyList<string> SubtypesTruncatedBy,
    IReadOnlyList<string> DiRegistrations,
    FindSymbolScopeDto? Scope = null);

internal sealed record TypeHierarchyEntryDto(
    string Name,
    string Kind,
    string? FilePath = null,
    int? Line = null,
    string? Id = null,
    string? HandoffKind = null,
    AssemblyNavigationOrigin? Origin = null,
    string? ScopeType = null,
    string? SourceKind = null);
