#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// Token-basiertes Code-Clone-Detection auf Method-Granularitaet (CCFinder/Jaccard-N-Gram-Ansatz).
/// Reine Domain-Logik ohne Kenntnis von <c>RuleViolation</c> oder <c>CallToolResult</c> —
/// Solution rein, <see cref="DuplicateCluster"/>-Records raus, analog
/// <see cref="Mcp.Tools.DependencyGraph.DependencyGraphScanner"/>. Beide Konsumenten
/// (<c>DuplicateCodeChecker</c> fuer Lint, <c>find_duplicates</c> fuer MCP) rufen dieselbe
/// <see cref="ScanAsync"/>-Methode auf, deshalb liegt diese Klasse bewusst unter <c>Core/</c> statt
/// <c>Mcp/Tools/</c> (Mcp/Tools/* haengt von Core/ ab, nicht umgekehrt).
///
/// Pipeline: 1) Token-Extraktion pro Methode via <see cref="SyntaxNode.DescendantTokens()"/>
/// (Whitespace/Kommentare sind Trivia, nicht Teil des Token-Streams). 2) N-Gram-Shingling
/// (Sliding-Window, Default k=5) zu deterministischen FNV-1a-Hashes. 3) Inverted Index
/// (Hash → Methoden-Indizes). 4) Kandidaten-Paare ueber gemeinsame N-Gramme (Mindestanzahl
/// <see cref="DuplicateDetectionOptions.MinSharedNgrams"/>). 5) Exakter Jaccard-Score je
/// Kandidaten-Paar. 6) Transitive Cluster-Bildung (Union-Find). 7) Schwellwert-Staffelung
/// (<see cref="DuplicateSimilarityBucket"/>) statt hartem Cut.
///
/// <c>partial</c>, weil die Refactoring-Drift-Erweiterung dieselbe Fingerprint-Sammlung und
/// Jaccard-Berechnung wiederverwendet ("1 gegen alle" statt "alle gegen alle") — Erweiterung liegt
/// in <c>DuplicateDetectionEngine.RefactoringDrift.cs</c> (Datei-Split-Konvention wie
/// <c>RuleRegistry.Architecture.cs</c>/<c>RuleRegistry.General.cs</c>), damit diese
/// Verhaltens-Datei nicht ueber <c>MaxLineCount</c> waechst.
/// </summary>
internal static class DuplicateDetectionEngine
{
    /// <summary>
    /// Obergrenze fuer die Anzahl Methoden, die ein einzelnes N-Gram im Inverted Index zur
    /// Kandidaten-Paar-Bildung beitragen darf. Sehr haeufige N-Gramme (z. B. triviale
    /// Boilerplate-Sequenzen wie <c>"get => _value ;"</c>) sind nicht diskriminierend und wuerden
    /// ohne Kappung eine O(n²)-Paar-Explosion in dieser einen Bucket ausloesen — Standard-Technik
    /// bei Token-CPD-Implementierungen (vgl. CCFinder "frequency threshold").
    /// </summary>
    private const int MaxMethodsPerNgramBucket = 60;

    internal static async Task<DuplicateDetectionScanResult> ScanAsync(
        Solution solution, DuplicateDetectionOptions options, CancellationToken ct)
    {
        var fingerprints = await CollectFingerprintsAsync(solution, options, ct);
        var edges = FindCandidateEdges(fingerprints, options);
        var clusters = DuplicateClusterBuilder.Build(
            fingerprints,
            edges,
            options,
            (fingerprint, _) => new DuplicateClusterMember(
                fingerprint.FilePath,
                fingerprint.LineNumber,
                fingerprint.SignatureName,
                fingerprint.TokenCount));
        return new DuplicateDetectionScanResult(clusters, fingerprints.Count);
    }

    // ── 1) Fingerprint-Sammlung ──────────────────────────────────────────────────────────────

    internal static async Task<List<MethodFingerprint>> CollectFingerprintsAsync(
        Solution solution, DuplicateDetectionOptions options, CancellationToken ct)
    {
        var eligible = await DuplicateMethodCollector.CollectAsync(solution, options, ct);
        var result = new List<MethodFingerprint>(eligible.Count);
        foreach (var method in eligible)
        {
            var fingerprint = TryBuildFingerprint(method, options);
            if (fingerprint is not null) result.Add(fingerprint);
        }
        return result;
    }

    private static MethodFingerprint? TryBuildFingerprint(EligibleMethod method, DuplicateDetectionOptions options)
    {
        var tokens = method.Body.DescendantTokens().ToList();
        var ngramHashes = BuildNgramHashes(tokens, options.NgramSize, options.NormalizeIdentifiers);
        if (ngramHashes.Count == 0) return null;

        return new MethodFingerprint(
            method.FilePath, method.LineNumber, method.SignatureName, method.TokenCount, ngramHashes, method.Symbol);
    }

    // ── 2) N-Gram-Shingling ───────────────────────────────────────────────────────────────────

    private static HashSet<ulong> BuildNgramHashes(IReadOnlyList<SyntaxToken> tokens, int ngramSize, bool normalizeIdentifiers)
    {
        var hashes = new HashSet<ulong>();
        if (tokens.Count < ngramSize) return hashes;

        var representations = tokens.Select(t => GetTokenRepresentation(t, normalizeIdentifiers)).ToList();
        for (var start = 0; start <= representations.Count - ngramSize; start++)
        {
            hashes.Add(HashNgram(representations, start, ngramSize));
        }
        return hashes;
    }

    private static string GetTokenRepresentation(SyntaxToken token, bool normalizeIdentifiers)
    {
        if (!normalizeIdentifiers) return token.Text;
        if (token.IsKind(SyntaxKind.IdentifierToken)) return "$ID$";
        return IsLiteralToken(token) ? "$LIT$" : token.Text;
    }

    private static bool IsLiteralToken(SyntaxToken token) => token.Kind() switch
    {
        SyntaxKind.NumericLiteralToken => true,
        SyntaxKind.StringLiteralToken => true,
        SyntaxKind.CharacterLiteralToken => true,
        SyntaxKind.InterpolatedStringTextToken => true,
        SyntaxKind.TrueKeyword => true,
        SyntaxKind.FalseKeyword => true,
        SyntaxKind.NullKeyword => true,
        _ => false,
    };

    /// <summary>
    /// Deterministischer 64-Bit-Hash (FNV-1a) ueber die <paramref name="ngramSize"/> Token-Texte ab
    /// <paramref name="start"/>. Kein Kryptografie-Anspruch (siehe Ideensammlung §A.2 Schritt 2) —
    /// nur Determinismus ueber Prozessgrenzen hinweg (anders als <see cref="string.GetHashCode()"/>,
    /// das pro Prozess randomisiert ist). Ein separierendes Steuerzeichen zwischen Token-Texten
    /// verhindert Kollisionen durch Token-Grenzen-Verschiebung (z. B. "ab"+"c" vs. "a"+"bc").
    /// </summary>
    private static ulong HashNgram(IReadOnlyList<string> tokenTexts, int start, int ngramSize)
    {
        unchecked
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offsetBasis;
            for (var i = start; i < start + ngramSize; i++)
            {
                foreach (var ch in tokenTexts[i])
                {
                    hash ^= ch;
                    hash *= prime;
                }
                hash ^= '\u0001';
                hash *= prime;
            }
            return hash;
        }
    }

    // ── 3+4) Inverted Index + Kandidaten-Paare ───────────────────────────────────────────────

    private static List<DuplicateClusterEdge> FindCandidateEdges(
        IReadOnlyList<MethodFingerprint> fingerprints, DuplicateDetectionOptions options)
    {
        var invertedIndex = BuildInvertedIndex(fingerprints);
        var sharedCounts = AccumulateSharedNgramCounts(invertedIndex);
        return ComputeQualifyingEdges(fingerprints, sharedCounts, options);
    }

    private static Dictionary<ulong, List<int>> BuildInvertedIndex(IReadOnlyList<MethodFingerprint> fingerprints)
    {
        var index = new Dictionary<ulong, List<int>>();
        for (var i = 0; i < fingerprints.Count; i++)
        {
            foreach (var hash in fingerprints[i].NgramHashes)
            {
                if (!index.TryGetValue(hash, out var list))
                {
                    list = new List<int>();
                    index[hash] = list;
                }
                list.Add(i);
            }
        }
        return index;
    }

    private static Dictionary<(int A, int B), int> AccumulateSharedNgramCounts(Dictionary<ulong, List<int>> invertedIndex)
    {
        var sharedCounts = new Dictionary<(int A, int B), int>();
        foreach (var methodIndices in invertedIndex.Values)
        {
            if (methodIndices.Count < 2 || methodIndices.Count > MaxMethodsPerNgramBucket) continue;
            AccumulatePairsForBucket(methodIndices, sharedCounts);
        }
        return sharedCounts;
    }

    private static void AccumulatePairsForBucket(List<int> methodIndices, Dictionary<(int A, int B), int> sharedCounts)
    {
        for (var i = 0; i < methodIndices.Count; i++)
        {
            for (var j = i + 1; j < methodIndices.Count; j++)
            {
                var key = (methodIndices[i], methodIndices[j]);
                sharedCounts[key] = sharedCounts.GetValueOrDefault(key) + 1;
            }
        }
    }

    private static List<DuplicateClusterEdge> ComputeQualifyingEdges(
        IReadOnlyList<MethodFingerprint> fingerprints, Dictionary<(int A, int B), int> sharedCounts, DuplicateDetectionOptions options)
    {
        var edges = new List<DuplicateClusterEdge>();
        foreach (var ((a, b), shared) in sharedCounts)
        {
            if (shared < options.MinSharedNgrams) continue;
            var jaccard = ComputeJaccard(fingerprints[a].NgramHashes, fingerprints[b].NgramHashes);
            if (jaccard >= options.FuzzyThreshold) edges.Add(new DuplicateClusterEdge(a, b, jaccard));
        }
        return edges;
    }

    // ── 5) Jaccard-Similarity ────────────────────────────────────────────────────────────────

    internal static double ComputeJaccard(HashSet<ulong> a, HashSet<ulong> b)
    {
        var (smaller, larger) = a.Count <= b.Count ? (a, b) : (b, a);
        var intersection = smaller.Count(larger.Contains);
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    // ── 6+7) Cluster-Bildung (Union-Find) + Schwellwert-Staffelung ───────────────────────────

    internal static DuplicateSimilarityBucket ClassifyBucket(double score, DuplicateDetectionOptions options) => score switch
    {
        _ when score >= options.ExactThreshold => DuplicateSimilarityBucket.Exact,
        _ when score >= options.NearThreshold => DuplicateSimilarityBucket.Near,
        _ => DuplicateSimilarityBucket.Fuzzy,
    };

}
