#nullable enable

using System;

namespace AiNetLinter.Mcp.Scope;

/// <summary>Validiert den geschlossenen öffentlichen <c>scopeType</c>-Wertebereich.</summary>
internal static class McpScopeTypeValidator
{
    internal static bool TryParse(string? value, out McpScopeType scopeType, out string? fieldPath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            scopeType = McpScopeType.All;
            fieldPath = null;
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "all":
                scopeType = McpScopeType.All;
                fieldPath = null;
                return true;
            case "production":
                scopeType = McpScopeType.Production;
                fieldPath = null;
                return true;
            case "tests":
                scopeType = McpScopeType.Tests;
                fieldPath = null;
                return true;
            default:
                scopeType = default;
                fieldPath = "$.scopeType";
                return false;
        }
    }
}
