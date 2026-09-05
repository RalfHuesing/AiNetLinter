#nullable enable

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record FindReferencesRequest(
    string? SymbolIdentifier,
    int MaxResults,
    int Depth,
    string? Symbol = null)
{
    public string? EffectiveSymbolIdentifier =>
        !string.IsNullOrWhiteSpace(SymbolIdentifier) ? SymbolIdentifier : Symbol;
}
