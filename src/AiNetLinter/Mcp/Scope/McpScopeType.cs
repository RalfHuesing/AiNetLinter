#nullable enable

namespace AiNetLinter.Mcp.Scope;

/// <summary>Der vom MCP angeforderte Dokument-Scope.</summary>
internal enum McpScopeType
{
    All,
    Production,
    Tests,
}

/// <summary>Die fachliche Rolle des Roslyn-Projekts eines Dokuments.</summary>
internal enum McpProjectKind
{
    Production,
    Tests,
    Unknown,
}

/// <summary>Gibt an, ob ein Dokument bearbeitbare oder generierte Quelle enthält.</summary>
internal enum McpSourceKind
{
    Editable,
    Generated,
}
