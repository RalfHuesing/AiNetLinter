#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Scope;

namespace AiNetLinter.Mcp.Tools.DependencyGraph;

// Parameter- und Ergebnis-Records fuer DependencyGraphTool/DependencyGraphScanner — aus den
// beiden Verhaltens-Dateien ausgelagert, damit deren AIContextFootprint/MaxLineCount nicht durch
// reine Datentraeger aufgeblaeht wird (Pattern konsistent mit SafeguardModels.cs).

/// <summary>
/// Parameter-Record fuer <see cref="DependencyGraphTool.ExecuteAsync"/>. Kapselt 5
/// Konfigurations-Eingaenge in einem Record, damit <c>MaxMethodParameterCount: 4</c>
/// (siehe Linter-Regel <c>MaxMethodParameterCount</c>) eingehalten wird — Records sind von diesem Limit ausgenommen.
/// Genau eines von <see cref="FilePath"/>/<see cref="SymbolIdentifier"/> muss gesetzt sein (siehe
/// <see cref="DependencyGraphTool.ExecuteAsync"/>).
/// </summary>
internal sealed record DependencyGraphInput(
    string? FilePath,
    string? SymbolIdentifier,
    string? Direction,
    int Depth,
    int MaxResults,
    string ScopeType = "all",
    bool IncludeGenerated = false,
    int MaxResponseBytes = DependencyGraphTool.DefaultMaxResponseBytes);

/// <summary>
/// Buendelt die Scan-Konfiguration fuer <see cref="DependencyGraphScanner.ScanFileAsync"/> /
/// <see cref="DependencyGraphScanner.ScanTypeAsync"/> — Solution separat von Dokument/Typ
/// uebergeben, weil die beiden Einstiegspunkte unterschiedliche erste Parameter haben
/// (Document vs. INamedTypeSymbol), aber dieselbe restliche Konfiguration teilen.
/// </summary>
internal sealed record DependencyGraphScanRequest(
    Microsoft.CodeAnalysis.Solution Solution,
    bool IncludeOutgoing,
    bool IncludeIncoming,
    int Depth,
    int MaxResults,
    McpScopeType ScopeType = McpScopeType.All,
    bool IncludeGenerated = false,
    McpScopeClassifier? ScopeClassifier = null);

/// <summary>
/// Auflösungsziel von <c>dependency_graph</c>: entweder eine ganze Datei (<c>Kind == "file"</c>,
/// <see cref="TypeName"/> <see langword="null"/>) oder ein einzelner Typ innerhalb einer Datei
/// (<c>Kind == "type"</c>, engerer Scope als die ganze Datei — siehe Scanner-Doc-Kommentar).
/// </summary>
internal sealed record DependencyGraphTarget(
    string Kind,
    string Path,
    string? TypeName);

/// <summary>
/// Eine Datei-zu-Datei-Abhaengigkeitskante, annotiert mit den Typnamen, die den Uebergang
/// ausgeloest haben, und der Gesamtzahl der zugrunde liegenden Typreferenzen. <see cref="Direction"/>
/// ist <c>"outgoing"</c> oder <c>"incoming"</c> (nie <c>"both"</c> — das ist nur der Eingabe-Filter).
/// </summary>
internal sealed record DependencyEdge(
    string From,
    string To,
    string Direction,
    IReadOnlyList<string> TypeNames,
    int ReferenceCount);

/// <summary>Ein eindeutiger Graphknoten mit gemeinsamer MCP-Scope-Klassifikation.</summary>
internal sealed record DependencyGraphNode(
    string Path,
    string ScopeType,
    string SourceKind,
    bool IsBridge);

/// <summary>
/// Ausgehende Projekt-Referenzen (<c>Project.ProjectReferences</c>) des Projekts, das die
/// Zieldatei enthaelt — guenstig zu ermitteln (keine NuGet-Aufrufe), daher immer mitgeliefert.
/// </summary>
internal sealed record ProjectReferenceEntry(
    string Project,
    IReadOnlyList<string> References);

/// <summary>
/// Gesamtergebnis eines <c>dependency_graph</c>-Scans. <see cref="Truncated"/> ist <see langword="true"/>,
/// wenn <see cref="Edges"/> durch <c>maxResults</c> gekappt wurde ODER <see cref="NodeCapReached"/>
/// gesetzt ist (Traversierungs-Hard-Cap, siehe <see cref="DependencyGraphScanner.MaxVisitedFiles"/>) —
/// echtes Bool-Feld statt String-Heuristik (siehe get_call_tree-Lehre in <c>GetCallTreeTool.cs</c>).
/// </summary>
internal sealed record DependencyGraphResult(
    IReadOnlyList<DependencyEdge> Edges,
    int TotalEdgeCount,
    IReadOnlyList<ProjectReferenceEntry> ProjectReferences,
    bool IncludeOutgoing,
    bool IncludeIncoming,
    int RequestedDepth,
    int ClampedDepth,
    bool DepthWasClamped,
    bool NodeCapReached,
    bool Truncated,
    IReadOnlyList<DependencyGraphNode>? Nodes = null,
    int TotalNodeCount = 0,
    int ShownNodeCount = 0,
    int ExcludedNodeCount = 0,
    int ExcludedEdgeCount = 0,
    McpScopeMetadata? Scope = null,
    IReadOnlyList<string>? TruncatedBy = null);

internal sealed record DependencyGraphWirePayload(
    DependencyGraphTarget Target,
    string Direction,
    IReadOnlyList<DependencyGraphNode> Nodes,
    IReadOnlyList<DependencyEdge> Edges,
    IReadOnlyList<ProjectReferenceEntry> ProjectReferences,
    int RequestedDepth,
    int EffectiveDepth,
    bool DepthWasClamped,
    bool Truncated,
    int TotalEdgeCount,
    int ShownEdgeCount,
    int TotalNodeCount,
    int ShownNodeCount,
    int ExcludedNodeCount,
    int ExcludedEdgeCount,
    McpScopeMetadata Scope,
    IReadOnlyList<string> TruncatedBy);

internal sealed record DependencyGraphScopeState(
    McpScopeClassifier Classifier,
    Dictionary<string, McpDocumentScope> NodeScopes,
    HashSet<string> ExcludedNodes,
    HashSet<(string From, string To, string Direction)> ExcludedEdges);

internal sealed record DependencyGraphTraversalState(
    DependencyGraphScanRequest Request,
    List<string> Frontier,
    HashSet<string> Visited,
    Dictionary<(string From, string To, string Direction), DependencyGraphEdgeAccumulator> EdgeMap,
    DependencyGraphScopeState ScopeState);

internal sealed record DependencyGraphFirstHopRequest(
    string TargetFile,
    Func<CancellationToken, Task<Dictionary<string, DependencyGraphEdgeAccumulator>>>? Outgoing,
    Func<CancellationToken, Task<Dictionary<string, DependencyGraphEdgeAccumulator>>>? Incoming,
    DependencyGraphTraversalState State);

internal sealed record DependencyGraphFilterRequest(
    DependencyGraphScanRequest Request,
    string AnchorFile,
    string Direction,
    Dictionary<string, DependencyGraphEdgeAccumulator> Discovered,
    DependencyGraphScopeState ScopeState);

internal sealed record DependencyGraphBuildRequest(
    DependencyGraphScanRequest Request,
    string TargetFile,
    Dictionary<(string From, string To, string Direction), DependencyGraphEdgeAccumulator> EdgeMap,
    Dictionary<string, McpDocumentScope> NodeScopes,
    HashSet<string> ExcludedNodes,
    HashSet<(string From, string To, string Direction)> ExcludedEdges,
    int ClampedDepth,
    bool NodeCapReached);

internal sealed class DependencyGraphEdgeAccumulator
{
    internal HashSet<string> TypeNames { get; } = new(StringComparer.Ordinal);
    internal int ReferenceCount { get; set; }
}
