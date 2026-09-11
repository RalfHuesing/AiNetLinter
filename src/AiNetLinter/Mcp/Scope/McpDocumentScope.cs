#nullable enable

namespace AiNetLinter.Mcp.Scope;

/// <summary>Gemeinsame Klassifikation eines MCP-Dokuments.</summary>
internal readonly record struct McpDocumentScope(
    McpProjectKind ProjectKind,
    McpSourceKind SourceKind);
