#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Mcp.Tools.SymbolGraph;

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
    bool IncludeBcl = false,
    string ScopeType = "all",
    bool IncludeGenerated = false,
    int MaxResponseBytes = GetCallTreeTool.DefaultMaxResponseBytes)
;

internal sealed record CallTreeBuildRequest(
    Solution Solution,
    ISymbol SeedSymbol,
    int RequestedDepth,
    int TopN,
    CallTreeDirection Direction,
    bool AbsolutePaths = false,
    bool IncludeBcl = false,
    AnalysisSymbolIdentity? HandoffIdentity = null,
    McpScopeType ScopeType = McpScopeType.All,
    bool IncludeGenerated = false,
    McpScopeClassifier? ScopeClassifier = null);

internal sealed record CallTreePayload(
    CallGraphPayload Graph,
    string Direction,
    int RequestedDepth,
    int EffectiveDepth,
    bool DepthWasClamped,
    int TopN,
    bool Truncated,
    bool TopNTruncated,
    int TotalNodeCount = 0,
    int TotalEdgeCount = 0,
    int ReturnedNodeCount = 0,
    int ReturnedEdgeCount = 0,
    IReadOnlyList<string>? TruncatedBy = null,
    McpScopeMetadata? Scope = null,
    AssemblyNavigationSummary? AssemblyNavigation = null);

/// <summary>
/// Kompakte, graphbasierte Darstellung des Aufrufgraphen. Die lokalen IDs sind nur fuer diese
/// Nutzlast gueltig; stabile Symbolidentitaet bleibt separat auf dem Knoten erhalten.
/// </summary>
internal sealed record CallGraphPayload(
    string RootNodeId,
    IReadOnlyList<CallGraphNode> Nodes,
    IReadOnlyList<CallGraphEdge> Edges,
    IReadOnlyList<CallGraphMethodHint>? MethodHints = null,
    bool TopNTruncated = false,
    bool ScopeFiltered = false,
    int HiddenEdgeCount = 0,
    bool HardCapTruncated = false);

internal sealed class CallGraphNode : IEquatable<CallGraphNode>
{
    [System.Text.Json.Serialization.JsonConstructor]
    public CallGraphNode(string nodeId, string symbolId, string name, string displayLine, string kind)
    {
        NodeId = nodeId;
        Symbol = null!;
        SymbolId = symbolId;
        Name = name;
        DisplayLine = displayLine;
        Kind = kind;
    }

    public string NodeId { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    internal ISymbol Symbol { get; }
    public string SymbolId { get; }
    public string Name { get; }
    public string DisplayLine { get; }
    public string Kind { get; }

    internal CallGraphNode(
        string nodeId,
        ISymbol symbol,
        string symbolId,
        string? name = null,
        string? displayLine = null)
    {
        NodeId = nodeId;
        Symbol = symbol;
        SymbolId = symbolId;
        Name = name ?? symbol.Name;
        DisplayLine = displayLine ?? string.Empty;
        Kind = symbol.Kind.ToString().ToLowerInvariant();
    }

    public bool Equals(CallGraphNode? other) =>
        other is not null
        && string.Equals(NodeId, other.NodeId, StringComparison.Ordinal)
        && string.Equals(SymbolId, other.SymbolId, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as CallGraphNode);

    public override int GetHashCode() => HashCode.Combine(NodeId, SymbolId);
}

internal sealed class CallGraphEdge : IEquatable<CallGraphEdge>
{
    [System.Text.Json.Serialization.JsonConstructor]
    public CallGraphEdge(
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

    public string FromNodeId { get; }
    public string ToNodeId { get; }
    public IReadOnlyList<CallGraphCallSite> CallSites { get; }
    public string? DispatchKind { get; }

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

internal sealed record CallGraphMethodHint(
    string Name,
    string SymbolId,
    string DisplayLine);
