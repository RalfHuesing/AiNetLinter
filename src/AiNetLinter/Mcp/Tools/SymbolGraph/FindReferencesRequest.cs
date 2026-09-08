#nullable enable

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record FindReferencesRequest(
    string? SymbolIdentifier,
    int MaxResults,
    int Depth)
{
    public string? EffectiveSymbolIdentifier =>
        string.IsNullOrWhiteSpace(SymbolIdentifier) ? null : SymbolIdentifier;
}
