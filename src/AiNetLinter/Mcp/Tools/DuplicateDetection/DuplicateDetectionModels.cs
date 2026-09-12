#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

// Parameter- und Ergebnis-Records fuer DuplicateDetectionTool/DuplicateDetectionScanner — aus den
// beiden Verhaltens-Dateien ausgelagert (Pattern konsistent mit DependencyGraphModels.cs).

/// <summary>
/// Parameter-Record fuer <c>find_duplicates</c>. <see cref="Mode"/>/<see cref="HelperSymbol"/>
/// wurden als zusaetzliche, optionale Trailing-Felder ergaenzt (Teil C, Refactoring-Drift) — ohne
/// bestehende Konstruktions-Aufrufe mit 5 Positionsargumenten umzusortieren (Default <see
/// langword="null"/> haelt sie kompatibel). <see cref="Mode"/> ist <c>"clone"</c> (Default, Teil A)
/// oder <c>"refactoring-drift"</c> (Teil C, <see cref="HelperSymbol"/> dann Pflicht) oder
/// <c>"structural"</c> (Typ-4-Strukturprofile, Cosine-Similarity; <see cref="HelperSymbol"/>
/// wird ignoriert) — Format von
/// <see cref="HelperSymbol"/> identisch zu <c>find_references</c>/<c>get_impact</c>s
/// <c>symbolIdentifier</c> (stabile DocumentationCommentId, Datei:Zeile:Spalte oder qualifizierter
/// Name).
/// </summary>
internal sealed record DuplicateDetectionInput(
    int? MinTokens,
    string? SimilarityThreshold,
    bool? NormalizeIdentifiers,
    string? ScopeDir,
    int? MaxResults,
    string? Mode = null,
    string? HelperSymbol = null,
    string? ScopeType = null);

/// <summary>Ergebnis von <see cref="DuplicateDetectionScanner.ScanAsync"/> — reine Daten, keine
/// Text-/JSON-Formatierung (die macht <see cref="DuplicateDetectionTool"/>, analog
/// <c>DependencyGraphScanner</c>/<c>DependencyGraphTool</c>). <see cref="Truncated"/> ist ein
/// echtes Bool-Feld statt einer String-Heuristik (siehe <c>get_call_tree</c>-Lehre).</summary>
internal sealed record DuplicateDetectionScanResultForTool(
    IReadOnlyList<Core.DuplicateDetection.DuplicateCluster> ShownClusters,
    int TotalClusters,
    int MethodsScanned,
    bool Truncated);
