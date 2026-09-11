#nullable enable

using System;

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

internal static class McpScopeValues
{
    internal static string ToWireValue(McpScopeType value) => value switch
    {
        McpScopeType.Production => "production",
        McpScopeType.Tests => "tests",
        _ => "all",
    };

    internal static string ToWireValue(McpProjectKind value) => value switch
    {
        McpProjectKind.Production => "production",
        McpProjectKind.Tests => "tests",
        _ => "unknown",
    };

    internal static string ToWireValue(McpSourceKind value) => value == McpSourceKind.Generated
        ? "generated"
        : "editable";
}
