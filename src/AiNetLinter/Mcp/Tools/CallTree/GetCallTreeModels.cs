#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
    bool IncludeBcl = false)
;

internal sealed record CallTreeBuildRequest(
    Solution Solution,
    ISymbol SeedSymbol,
    int RequestedDepth,
    int TopN,
    CallTreeDirection Direction,
    bool AbsolutePaths = false,
    bool IncludeBcl = false,
    AnalysisSymbolIdentity? HandoffIdentity = null);

internal sealed record CallTreePayload(
    MetricsTreeNode Root,
    string Direction,
    int RequestedDepth,
    int EffectiveDepth,
    bool DepthWasClamped,
    int TopN,
    bool Truncated,
    bool TopNTruncated);

/// <summary>
/// Kompakte, graphbasierte Darstellung des Aufrufgraphen. Die lokalen IDs sind nur fuer diese
/// Nutzlast gueltig; stabile Symbolidentitaet bleibt separat auf dem Knoten erhalten.
/// </summary>
internal sealed record CallGraphPayload(
    string RootNodeId,
    IReadOnlyList<CallGraphNode> Nodes,
    IReadOnlyList<CallGraphEdge> Edges);

internal sealed class CallGraphNode : IEquatable<CallGraphNode>
{
    internal CallGraphNode(string nodeId, ISymbol symbol, string symbolId)
    {
        NodeId = nodeId;
        Symbol = symbol;
        SymbolId = symbolId;
        Name = symbol.Name;
    }

    internal string NodeId { get; }
    internal ISymbol Symbol { get; }
    internal string SymbolId { get; }
    internal string Name { get; }

    public bool Equals(CallGraphNode? other) =>
        other is not null
        && string.Equals(NodeId, other.NodeId, StringComparison.Ordinal)
        && string.Equals(SymbolId, other.SymbolId, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as CallGraphNode);

    public override int GetHashCode() => HashCode.Combine(NodeId, SymbolId);
}

internal sealed class CallGraphEdge : IEquatable<CallGraphEdge>
{
    internal CallGraphEdge(
        string fromNodeId,
        string toNodeId,
        IReadOnlyList<CallGraphCallSite> callSites,
        string? dispatchKind = null)
    {
        FromNodeId = fromNodeId;
        ToNodeId = toNodeId;
        CallSites = callSites;
        DispatchKind = dispatchKind;
    }

    internal string FromNodeId { get; }
    internal string ToNodeId { get; }
    internal IReadOnlyList<CallGraphCallSite> CallSites { get; }
    internal string? DispatchKind { get; }

    public bool Equals(CallGraphEdge? other) =>
        other is not null
        && string.Equals(FromNodeId, other.FromNodeId, StringComparison.Ordinal)
        && string.Equals(ToNodeId, other.ToNodeId, StringComparison.Ordinal)
        && string.Equals(DispatchKind, other.DispatchKind, StringComparison.Ordinal)
        && CallSites.SequenceEqual(other.CallSites);

    public override bool Equals(object? obj) => Equals(obj as CallGraphEdge);

    public override int GetHashCode() => HashCode.Combine(
        FromNodeId,
        ToNodeId,
        DispatchKind,
        CallSites.Count);
}

internal sealed record CallGraphCallSite(
    string FilePath,
    int Line,
    int Column,
    string ProjectName);
