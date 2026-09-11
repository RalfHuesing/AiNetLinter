#nullable enable

using AiNetLinter.Mcp.Scope;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record FindReferencesRequest(
    string? SymbolIdentifier,
    int MaxResults,
    int Depth,
    McpScopeType ScopeType = McpScopeType.All,
    bool IncludeGenerated = false,
    McpScopeClassifier? ScopeClassifier = null,
    int MaxResponseBytes = FindReferencesTool.DefaultMaxResponseBytes)
{
    public string? EffectiveSymbolIdentifier =>
        string.IsNullOrWhiteSpace(SymbolIdentifier) ? null : SymbolIdentifier;
}
