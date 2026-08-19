#nullable enable

using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.CallTree;

internal enum CallTreeDirection
{
    Incoming,
    Outgoing,
    Both,
}

internal sealed record GetCallTreeInput(
    string? SymbolIdentifier,
    int Depth,
    string? Format,
    int TopN,
    string? Direction = null);

internal sealed record CallTreeBuildRequest(
    Solution Solution,
    ISymbol SeedSymbol,
    int RequestedDepth,
    int TopN,
    CallTreeDirection Direction);