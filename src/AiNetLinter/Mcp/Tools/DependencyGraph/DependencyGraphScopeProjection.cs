#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Core.Documents;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.DependencyGraph;

/// <summary>
/// Klassifiziert und projiziert die vollständige Dependency-Graph-Domäne. Scope-/Generated-
/// Entscheidungen bleiben hier gebündelt; der Scanner orchestriert nur die BFS.
/// </summary>
internal static class DependencyGraphScopeProjection
{
    internal static async Task ClassifyNodeAsync(
        Solution solution,
        string relativePath,
        McpScopeClassifier classifier,
        Dictionary<string, McpDocumentScope> nodeScopes,
        CancellationToken ct)
    {
        if (nodeScopes.ContainsKey(relativePath)) return;
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, relativePath);
        if (document is null) return;
        nodeScopes[relativePath] = await classifier.ClassifyAsync(document, ct).ConfigureAwait(false);
    }

    internal static async Task<Dictionary<string, DependencyGraphEdgeAccumulator>> FilterDiscoveredEdgesAsync(
        DependencyGraphFilterRequest filter,
        CancellationToken ct)
    {
        var visible = new Dictionary<string, DependencyGraphEdgeAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var (otherFile, accumulator) in filter.Discovered)
        {
            await ClassifyNodeAsync(
                filter.Request.Solution,
                otherFile,
                filter.ScopeState.Classifier,
                filter.ScopeState.NodeScopes,
                ct).ConfigureAwait(false);
            if (!filter.ScopeState.NodeScopes.TryGetValue(otherFile, out var scope)) continue;

            if (IsAdmitted(scope, filter.Request, filter.ScopeState.Classifier))
            {
                visible[otherFile] = accumulator;
                continue;
            }

            filter.ScopeState.ExcludedNodes.Add(otherFile);
            filter.ScopeState.ExcludedEdges.Add(CreateEdgeKey(filter, otherFile));
        }

        return visible;
    }

    private static (string From, string To, string Direction) CreateEdgeKey(
        DependencyGraphFilterRequest filter,
        string otherFile) =>
        filter.Direction == "outgoing"
            ? (filter.AnchorFile, otherFile, filter.Direction)
            : (otherFile, filter.AnchorFile, filter.Direction);

    private static bool IsAdmitted(
        McpDocumentScope scope,
        DependencyGraphScanRequest request,
        McpScopeClassifier classifier)
    {
        var inRequestedScope = classifier.MatchesScope(scope, request.ScopeType);
        var visibleSource = request.IncludeGenerated || scope.SourceKind != McpSourceKind.Generated;
        return inRequestedScope && (visibleSource || scope.SourceKind == McpSourceKind.Generated);
    }

    internal static DependencyGraphResult BuildResult(DependencyGraphBuildRequest build)
    {
        var allEdges = OrderEdges(build.EdgeMap, build.NodeScopes);
        var reachable = FindReachableNodes(build.TargetFile, allEdges);
        var useful = FindUsefulNodes(build, allEdges, reachable);
        var retainedEdges = allEdges
            .Where(edge => useful.Contains(GetTraversalSource(edge))
                && useful.Contains(GetTraversalTarget(edge)))
            .ToList();
        return CreateResult(build, allEdges, retainedEdges);
    }

    private static List<DependencyEdge> OrderEdges(
        Dictionary<(string From, string To, string Direction), DependencyGraphEdgeAccumulator> edgeMap,
        Dictionary<string, McpDocumentScope> nodeScopes) =>
        edgeMap
            .OrderBy(kv => kv.Key.Direction == "outgoing" ? 0 : 1)
            .ThenBy(kv => GetNodeRank(
                nodeScopes,
                kv.Key.Direction == "outgoing" ? kv.Key.To : kv.Key.From))
            .ThenBy(kv => kv.Key.From, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.To, StringComparer.Ordinal)
            .Select(kv => new DependencyEdge(
                kv.Key.From,
                kv.Key.To,
                kv.Key.Direction,
                kv.Value.TypeNames.OrderBy(n => n, StringComparer.Ordinal).ToList(),
                kv.Value.ReferenceCount))
            .ToList();

    private static DependencyGraphResult CreateResult(
        DependencyGraphBuildRequest build,
        IReadOnlyList<DependencyEdge> allEdges,
        IReadOnlyList<DependencyEdge> retainedEdges)
    {
        var effectiveMax = Math.Max(build.Request.MaxResults, 1);
        var shown = retainedEdges.Count <= effectiveMax
            ? retainedEdges.ToList()
            : retainedEdges.Take(effectiveMax).ToList();
        var allNodePaths = GetNodePaths(build.TargetFile, retainedEdges);
        var shownNodePaths = GetNodePaths(build.TargetFile, shown);
        var nodes = CreateNodes(build, shownNodePaths);
        var excludedNodeCount = build.ExcludedNodes.Count + CountDeadGeneratedNodes(build, allNodePaths);
        var excludedEdgeCount = build.ExcludedEdges.Count + allEdges.Count - retainedEdges.Count;
        var truncated = retainedEdges.Count > effectiveMax || build.NodeCapReached;
        var truncatedBy = CreateTruncationReasons(retainedEdges.Count > effectiveMax, build.NodeCapReached);

        return new DependencyGraphResult(
            Edges: shown,
            TotalEdgeCount: retainedEdges.Count,
            ProjectReferences: BuildProjectReferences(build.Request.Solution, build.TargetFile),
            IncludeOutgoing: build.Request.IncludeOutgoing,
            IncludeIncoming: build.Request.IncludeIncoming,
            RequestedDepth: build.Request.Depth,
            ClampedDepth: build.ClampedDepth,
            DepthWasClamped: build.Request.Depth != build.ClampedDepth,
            NodeCapReached: build.NodeCapReached,
            Truncated: truncated,
            Nodes: nodes,
            TotalNodeCount: allNodePaths.Count,
            ShownNodeCount: shownNodePaths.Count,
            ExcludedNodeCount: excludedNodeCount,
            ExcludedEdgeCount: excludedEdgeCount,
            Scope: new McpScopeMetadata(
                McpScopeValues.ToWireValue(build.Request.ScopeType),
                build.Request.IncludeGenerated),
            TruncatedBy: truncatedBy);
    }

    private static IReadOnlyList<DependencyGraphNode> CreateNodes(
        DependencyGraphBuildRequest build,
        HashSet<string> paths) =>
        paths
            .Select(path => CreateNode(path, build))
            .OrderBy(node => GetNodeRank(build.NodeScopes, node.Path))
            .ThenBy(node => node.Path, StringComparer.Ordinal)
            .ToList();

    private static DependencyGraphNode CreateNode(
        string path,
        DependencyGraphBuildRequest build)
    {
        var scope = build.NodeScopes.TryGetValue(path, out var value)
            ? value
            : new McpDocumentScope(McpProjectKind.Unknown, McpSourceKind.Editable);
        return new DependencyGraphNode(
            path,
            McpScopeValues.ToWireValue(scope.ProjectKind),
            McpScopeValues.ToWireValue(scope.SourceKind),
            path != build.TargetFile
                && !build.Request.IncludeGenerated
                && scope.SourceKind == McpSourceKind.Generated);
    }

    private static int CountDeadGeneratedNodes(
        DependencyGraphBuildRequest build,
        HashSet<string> retainedPaths) =>
        build.NodeScopes.Count(pair =>
            !retainedPaths.Contains(pair.Key)
            && pair.Value.SourceKind == McpSourceKind.Generated);

    private static IReadOnlyList<string> CreateTruncationReasons(
        bool maxResultsReached,
        bool nodeCapReached)
    {
        var reasons = new List<string>();
        if (maxResultsReached) reasons.Add("maxResults");
        if (nodeCapReached) reasons.Add("nodeLimit");
        return reasons;
    }

    private static HashSet<string> FindReachableNodes(
        string targetFile,
        IReadOnlyList<DependencyEdge> edges)
    {
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { targetFile };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var edge in edges)
            {
                if (reachable.Contains(GetTraversalSource(edge))
                    && reachable.Add(GetTraversalTarget(edge))) changed = true;
            }
        }

        return reachable;
    }

    private static HashSet<string> FindUsefulNodes(
        DependencyGraphBuildRequest build,
        IReadOnlyList<DependencyEdge> edges,
        HashSet<string> reachable)
    {
        var useful = CreateVisibleNodeSet(build, reachable);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var edge in edges)
            {
                var source = GetTraversalSource(edge);
                var target = GetTraversalTarget(edge);
                if (reachable.Contains(source) && reachable.Contains(target)
                    && useful.Contains(target) && useful.Add(source)) changed = true;
            }
        }

        return useful;
    }

    private static HashSet<string> CreateVisibleNodeSet(
        DependencyGraphBuildRequest build,
        IEnumerable<string> reachable)
    {
        var classifier = build.Request.ScopeClassifier ?? new McpScopeClassifier();
        var useful = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { build.TargetFile };
        foreach (var path in reachable)
        {
            if (IsVisibleNode(build, classifier, path)) useful.Add(path);
        }

        return useful;
    }

    private static bool IsVisibleNode(
        DependencyGraphBuildRequest build,
        McpScopeClassifier classifier,
        string path) =>
        build.NodeScopes.TryGetValue(path, out var scope)
        && classifier.MatchesScope(scope, build.Request.ScopeType)
        && (build.Request.IncludeGenerated || scope.SourceKind != McpSourceKind.Generated);

    private static HashSet<string> GetNodePaths(
        string targetFile,
        IReadOnlyList<DependencyEdge> edges)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { targetFile };
        foreach (var edge in edges)
        {
            paths.Add(edge.From);
            paths.Add(edge.To);
        }

        return paths;
    }

    private static int GetNodeRank(
        Dictionary<string, McpDocumentScope> nodeScopes,
        string path)
    {
        if (!nodeScopes.TryGetValue(path, out var scope)) return 2;
        var projectRank = scope.ProjectKind switch
        {
            McpProjectKind.Production => 0,
            McpProjectKind.Tests => 1,
            _ => 2,
        };
        return projectRank * 2 + (scope.SourceKind == McpSourceKind.Generated ? 1 : 0);
    }

    private static string GetTraversalSource(DependencyEdge edge) =>
        edge.Direction == "outgoing" ? edge.From : edge.To;

    private static string GetTraversalTarget(DependencyEdge edge) =>
        edge.Direction == "outgoing" ? edge.To : edge.From;

    private static IReadOnlyList<ProjectReferenceEntry> BuildProjectReferences(
        Solution solution,
        string targetFile)
    {
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, targetFile);
        var project = document?.Project;
        if (project is null) return Array.Empty<ProjectReferenceEntry>();

        var refs = project.ProjectReferences
            .Select(pr => solution.GetProject(pr.ProjectId)?.Name)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        return refs.Count == 0
            ? Array.Empty<ProjectReferenceEntry>()
            : new[] { new ProjectReferenceEntry(project.Name, refs) };
    }
}
