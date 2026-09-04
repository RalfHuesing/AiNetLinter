#nullable enable

using Microsoft.CodeAnalysis;
using AiNetLinter.Mcp.Tools.MetricsTree;

namespace AiNetLinter.Mcp.Tools.CallTree;

internal enum CallTreeDirection
{
    Incoming,
    Outgoing,
    Both,
}

internal static class CallTreeDirectionNames
{
    internal const string Incoming = "incoming";
    internal const string Outgoing = "outgoing";
    internal const string Both = "both";

    internal static string For(CallTreeDirection direction) => direction switch
    {
        CallTreeDirection.Incoming => Incoming,
        CallTreeDirection.Outgoing => Outgoing,
        CallTreeDirection.Both => Both,
        _ => string.Empty,
    };
}

internal sealed record GetCallTreeInput(
    string? SymbolIdentifier,
    int Depth,
    string? Format,
    int TopN,
    string? Direction = null,
    string? Symbol = null)
{
    public string? EffectiveSymbolIdentifier =>
        !string.IsNullOrWhiteSpace(SymbolIdentifier) ? SymbolIdentifier : Symbol;
}

internal sealed record CallTreeBuildRequest(
    Solution Solution,
    ISymbol SeedSymbol,
    int RequestedDepth,
    int TopN,
    CallTreeDirection Direction,
    bool AbsolutePaths = false);

internal sealed record CallTreePayload(
    MetricsTreeNode Root,
    string Direction,
    int RequestedDepth,
    int TopN,
    bool Truncated,
    bool TopNTruncated);
