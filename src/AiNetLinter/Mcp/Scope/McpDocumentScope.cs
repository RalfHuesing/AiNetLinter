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

internal sealed record McpScopeMetadata(string RequestedType, bool IncludeGenerated);

internal sealed record McpScopeFilter(
    McpScopeType RequestedType,
    bool IncludeGenerated,
    McpScopeClassifier Classifier);
