#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        var clusters = BuildClusters(fingerprints, edges, options);
        return new DuplicateDetectionScanResult(clusters, fingerprints.Count);
    }

    // ── 1) Fingerprint-Sammlung ──────────────────────────────────────────────────────────────

    internal static async Task<List<MethodFingerprint>> CollectFingerprintsAsync(
        Solution solution, DuplicateDetectionOptions options, CancellationToken ct)
    {
        var solutionDir = System.IO.Path.GetDirectoryName(solution.FilePath) ?? "";
        var result = new List<MethodFingerprint>();

        foreach (var project in solution.Projects)
        {
            if (!project.SupportsCompilation) continue;
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            foreach (var document in project.Documents)
            {
                ct.ThrowIfCancellationRequested();
                if (!IsEligibleDocument(document, solutionDir, options)) continue;
                await CollectDocumentFingerprintsAsync(document, compilation, options, result, ct);
            }
        }
        return result;
    }

    private static bool IsEligibleDocument(Document document, string solutionDir, DuplicateDetectionOptions options)
    {
        // Document.FilePath ist bei manchen In-Memory-Test-Solutions (AdhocWorkspace ohne
        // explizites filePath) null — SourceFileCatalog.IsValidDocument laesst das bewusst durch
        // (IsInSolutionDir gibt bei solutionDir/filePath == null true zurueck, siehe dortigen
        // Kommentar), aber ohne echten Pfad kann diese Engine die Methode weder eindeutig einem
        // Fundort zuordnen noch die Verzeichnis-Ausschluesse pruefen — daher hier zusaetzlich
        // explizit ausgeschlossen.
        if (string.IsNullOrEmpty(document.FilePath)) return false;
        if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) return false;
        var path = document.FilePath;
        if (IsPermanentlyExcludedPath(path)) return false;
        if (!PathNormalizer.MatchesScope(path, options.PathScopeFilter)) return false;
        return MatchesScopeType(document, path, options.ScopeType);
    }

    private static bool MatchesScopeType(Document document, string path, string? scopeType)
    {
        if (string.IsNullOrEmpty(scopeType) || string.Equals(scopeType, "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var isTest = PathNormalizer.IsTestFile(path) ||
                     document.Project.Name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
                     document.Project.Name.EndsWith(".TestKit", StringComparison.OrdinalIgnoreCase);

        return string.Equals(scopeType, "production", StringComparison.OrdinalIgnoreCase) ? !isTest : isTest;
    }

    /// <summary>
    /// Zusaetzlich zu <see cref="SourceFileCatalog.IsGeneratedPath"/> (bin/obj/worktrees/*.g.cs)
    /// ausgeschlossene Verzeichnisse, spezifisch fuer Drift-Audits (siehe Ideensammlung §A.3 und
    /// Safeguard-Fix-Review-Lehre 2026-08-06: absichtlich regelverletzende Fixture-Verzeichnisse
    /// duerfen nie in Audit-Ergebnisse einfliessen).
    /// </summary>
    private static bool IsPermanentlyExcludedPath(string path)
    {
        var normalized = PathNormalizer.NormalizeSeparators(path);
        return normalized.Contains("/.ainetlinter/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/tests/fixtures/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CollectDocumentFingerprintsAsync(
        Document document, Compilation compilation, DuplicateDetectionOptions options,
        List<MethodFingerprint> result, CancellationToken ct)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync(ct);
        if (syntaxTree is null) return;
        var root = await syntaxTree.GetRootAsync(ct);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        foreach (var candidate in FindCandidateMethods(root))
        {
            ct.ThrowIfCancellationRequested();
            var fingerprint = TryBuildFingerprint(candidate, document.FilePath!, semanticModel, options);
            if (fingerprint is not null) result.Add(fingerprint);
        }
    }

    private static IEnumerable<MethodCandidate> FindCandidateMethods(SyntaxNode root)
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is MethodDeclarationSyntax method)
            {
                var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
                if (body is not null) yield return new MethodCandidate(method, body);
            }
            else if (node is LocalFunctionStatementSyntax localFunction)
            {
                var body = (SyntaxNode?)localFunction.Body ?? localFunction.ExpressionBody;
                if (body is not null) yield return new MethodCandidate(localFunction, body);
            }
        }
    }

    private static MethodFingerprint? TryBuildFingerprint(
        MethodCandidate candidate, string filePath, SemanticModel semanticModel, DuplicateDetectionOptions options)
    {
        var symbol = semanticModel.GetDeclaredSymbol(candidate.Declaration) as IMethodSymbol;
        if (symbol is null || IsGenerated(symbol)) return null;

        var tokens = candidate.Body.DescendantTokens().ToList();
        if (tokens.Count < options.MinTokens) return null;

        var ngramHashes = BuildNgramHashes(tokens, options.NgramSize, options.NormalizeIdentifiers);
        if (ngramHashes.Count == 0) return null;

        var lineNumber = candidate.Declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        return new MethodFingerprint(filePath, lineNumber, symbol.ToDisplayString(), tokens.Count, ngramHashes, symbol);
    }

    private static bool IsGenerated(IMethodSymbol symbol) =>
        HasGeneratedCodeAttribute(symbol) || (symbol.ContainingType is { } t && HasGeneratedCodeAttribute(t));

    private static bool HasGeneratedCodeAttribute(ISymbol symbol) =>
        symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name is "GeneratedCodeAttribute" or "GeneratedCode");

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

    private static List<FingerprintEdge> FindCandidateEdges(
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

    private static List<FingerprintEdge> ComputeQualifyingEdges(
        IReadOnlyList<MethodFingerprint> fingerprints, Dictionary<(int A, int B), int> sharedCounts, DuplicateDetectionOptions options)
    {
        var edges = new List<FingerprintEdge>();
        foreach (var ((a, b), shared) in sharedCounts)
        {
            if (shared < options.MinSharedNgrams) continue;
            var jaccard = ComputeJaccard(fingerprints[a].NgramHashes, fingerprints[b].NgramHashes);
            if (jaccard >= options.FuzzyThreshold) edges.Add(new FingerprintEdge(a, b, jaccard));
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

    private static List<DuplicateCluster> BuildClusters(
        IReadOnlyList<MethodFingerprint> fingerprints, IReadOnlyList<FingerprintEdge> edges, DuplicateDetectionOptions options)
    {
        var unionFind = new UnionFind(fingerprints.Count);
        foreach (var edge in edges) unionFind.Union(edge.A, edge.B);

        var groups = new Dictionary<int, List<int>>();
        for (var i = 0; i < fingerprints.Count; i++)
        {
            var root = unionFind.Find(i);
            if (!groups.TryGetValue(root, out var members))
            {
                members = new List<int>();
                groups[root] = members;
            }
            members.Add(i);
        }

        var edgesByRoot = edges.ToLookup(e => unionFind.Find(e.A));
        return groups.Values
            .Where(members => members.Count >= 2)
            .Select(members => BuildCluster(fingerprints, members, edgesByRoot[unionFind.Find(members[0])], options))
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Members[0].FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Members[0].LineNumber)
            .ToList();
    }

    /// <summary>
    /// <see cref="DuplicateCluster.Score"/> ist das Minimum aller tatsaechlich berechneten
    /// paarweisen Kanten-Scores innerhalb des Clusters — nicht approximiert, sondern aus den in
    /// <see cref="ComputeQualifyingEdges"/> bereits berechneten Werten uebernommen. Konservativ:
    /// "diese Methoden sind MINDESTENS so aehnlich" statt eines optimistischen Durchschnitts, der
    /// eine schwache Randverbindung ueberdecken wuerde.
    /// </summary>
    private static DuplicateCluster BuildCluster(
        IReadOnlyList<MethodFingerprint> fingerprints, IReadOnlyList<int> memberIndices,
        IEnumerable<FingerprintEdge> clusterEdges, DuplicateDetectionOptions options)
    {
        var memberSet = new HashSet<int>(memberIndices);
        var minScore = clusterEdges
            .Where(e => memberSet.Contains(e.A) && memberSet.Contains(e.B))
            .Select(e => e.Jaccard)
            .DefaultIfEmpty(1.0)
            .Min();

        var members = memberIndices
            .Select(i => fingerprints[i])
            .OrderBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.LineNumber)
            .Select(f => new DuplicateClusterMember(f.FilePath, f.LineNumber, f.SignatureName, f.TokenCount))
            .ToList();

        return new DuplicateCluster(members, minScore, ClassifyBucket(minScore, options));
    }

    private static DuplicateSimilarityBucket ClassifyBucket(double score, DuplicateDetectionOptions options) => score switch
    {
        _ when score >= options.ExactThreshold => DuplicateSimilarityBucket.Exact,
        _ when score >= options.NearThreshold => DuplicateSimilarityBucket.Near,
        _ => DuplicateSimilarityBucket.Fuzzy,
    };

    private readonly record struct MethodCandidate(SyntaxNode Declaration, SyntaxNode Body);

    private readonly record struct FingerprintEdge(int A, int B, double Jaccard);

    /// <summary>Minimaler Union-Find (Path-Compression, ohne Union-by-Rank — Methodenzahlen pro
    /// Solution sind klein genug, dass Rank-Optimierung keinen messbaren Unterschied macht).</summary>
    private sealed class UnionFind
    {
        private readonly int[] _parent;

        internal UnionFind(int size)
        {
            _parent = new int[size];
            for (var i = 0; i < size; i++) _parent[i] = i;
        }

        internal int Find(int x)
        {
            while (_parent[x] != x)
            {
                _parent[x] = _parent[_parent[x]];
                x = _parent[x];
            }
            return x;
        }

        internal void Union(int a, int b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB) _parent[rootA] = rootB;
        }
    }
}
