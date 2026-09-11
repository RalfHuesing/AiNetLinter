#nullable enable

namespace AiNetLinter.Mcp.Scope;

/// <summary>Gemeinsame Klassifikation eines MCP-Dokuments.</summary>
internal readonly record struct McpDocumentScope(
    McpProjectKind ProjectKind,
    McpSourceKind SourceKind);

internal readonly record struct McpSymbolScope(
    McpProjectKind ProjectKind,
    McpSourceKind SourceKind,
    bool HasSourceLocation,
    bool IsVisible);

public sealed record McpScopeMetadata(string RequestedType, bool IncludeGenerated);

/// <summary>Gemeinsame, bereits validierte Scope-Eingabe aller Symbol- und Composite-Tools.</summary>
internal readonly record struct McpScopeInput(
    McpScopeType ScopeType = McpScopeType.All,
    bool IncludeGenerated = false)
{
    internal McpScopeMetadata ToMetadata() => new(
        McpScopeValues.ToWireValue(ScopeType),
        IncludeGenerated);
}

internal sealed record McpScopeFilter(
    McpScopeType RequestedType,
    bool IncludeGenerated,
    McpScopeClassifier Classifier);
