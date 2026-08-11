#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core.DuplicateDetection;
using Xunit;

namespace AiNetLinter.Tests.Core.DuplicateDetection;

/// <summary>
/// Ground-Truth-Tests fuer <see cref="DuplicateDetectionEngine"/> (Token-CPD/Jaccard-N-Gram).
/// Statt eines einzelnen langen Body wird eine 20-Statement-Basismethode
/// (<see cref="BaseStatements"/>) genutzt, damit Klon-Varianten ueber gezielte Statement-Swaps
/// kalibriert werden koennen: ein einzelner vollstaendig ersetzter 7-Token-Statement-Swap landet
/// verlaesslich im <c>near</c>-Bucket, vier weit auseinanderliegende Swaps im <c>fuzzy</c>-Bucket,
/// sechs Swaps klar unterhalb der <c>fuzzy</c>-Schwelle — die jeweiligen Score-Bereiche sind per
/// Testlauf verifiziert (nicht nur ueberschlagen) und mit grosszuegigem Sicherheitsabstand zu den
/// Bucket-Grenzen gewaehlt, um robust gegen kleine Aenderungen an der Tokenisierung zu bleiben.
/// <para/>
/// Auf zwei Dateien aufgeteilt (<c>partial class</c>, hier: Ground-Truth-Klonstufen + transitive
/// Cluster + Nicht-Klone + Regressionstest; Fortsetzung in
/// <see cref="DuplicateDetectionEngineTests"/> in
/// <c>DuplicateDetectionEngineFalsePositiveTests.cs</c>: False-Positive-Disziplin,
/// Identifier-Normalisierung, Schwellwerte, Test-Infrastruktur), weil sonst die
/// <c>MaxLineCount</c>-Grenze (500 Zeilen) ueberschritten wuerde — siehe Klassen-Doc-Kommentar
/// dort.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class DuplicateDetectionEngineTests
{
    private static readonly DuplicateDetectionOptions DefaultOptions = new(
        MinTokens: DuplicateDetectionDefaults.MinTokens,
        NgramSize: DuplicateDetectionDefaults.NgramSize,
        MinSharedNgrams: DuplicateDetectionDefaults.MinSharedNgrams,
        ExactThreshold: DuplicateDetectionDefaults.ExactThreshold,
        NearThreshold: DuplicateDetectionDefaults.NearThreshold,
        FuzzyThreshold: DuplicateDetectionDefaults.FuzzyThreshold,
        NormalizeIdentifiers: false);

    // 20 unabhaengige "int X = expr;"-Statements (je 7 Tokens) — Basis fuer alle Klon-Varianten.
    private static readonly string[] BaseStatements =
    [
        "int a = x + 1;",
        "int b = x + 2;",
        "int c = x + 3;",
        "int d = x + 4;",
        "int e = x + 5;",
        "int f = a + b;",
        "int g = c + d;",
        "int h = e + f;",
        "int i = g + h;",
        "int j = i - a;",
        "int k = j - b;",
        "int l = k - c;",
        "int m = l - d;",
        "int n = m - e;",
        "int o = n * 2;",
        "int p = o / 2;",
        "int q = p + 1;",
        "int r = q + 2;",
        "int s = r + 3;",
        "int t = s + 4;",
    ];

    private static string BuildMethod(string className, string methodName, IReadOnlyList<string> statements)
    {
        var body = string.Join("\n        ", statements);
        return $$"""
            public static class {{className}}
            {
                public static int {{methodName}}(int x)
                {
                    {{body}}
                    return t;
                }
            }
            """;
    }

    private static string[] WithReplacedStatements(int[] indices, string[] replacements)
    {
        var copy = (string[])BaseStatements.Clone();
        for (var i = 0; i < indices.Length; i++) copy[indices[i]] = replacements[i];
        return copy;
    }

    // ── Ground-Truth: exact/near/fuzzy Klon-Stufen ───────────────────────────────────────────

    [Fact]
    public async Task ScanAsync_ByteIdenticalBodies_ClassifiesAsExact()
    {
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("A.cs", BuildMethod("A", "ComputeOne", BaseStatements)),
            ("B.cs", BuildMethod("B", "ComputeTwo", BaseStatements)));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(2, cluster.Members.Count);
        Assert.Equal(1.0, cluster.Score, precision: 6);
        Assert.Equal(DuplicateSimilarityBucket.Exact, cluster.Bucket);
    }

    [Fact]
    public async Task ScanAsync_OneStatementChanged_ClassifiesAsNear()
    {
        // Ein vollstaendig ersetztes Statement (gleiche Token-Anzahl) veraendert ~11 von ~138
        // N-Grammen -> Jaccard ~0.85, sicher innerhalb [0.80, 0.95).
        var variant = WithReplacedStatements([8], ["int i = a * 7;"]);
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("A.cs", BuildMethod("A", "ComputeBase", BaseStatements)),
            ("B.cs", BuildMethod("B", "ComputeNear", variant)));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.InRange(cluster.Score, 0.80, 0.9499);
        Assert.Equal(DuplicateSimilarityBucket.Near, cluster.Bucket);
    }

    [Fact]
    public async Task ScanAsync_TwoNonOverlappingStatementsChanged_ClassifiesAsFuzzy()
    {
        // Vier weit auseinanderliegende ersetzte Statements -> empirisch kalibriert auf einen
        // Jaccard-Score innerhalb [0.65, 0.80) (siehe Kommentar an der Klasse fuer die Methodik —
        // exakte Fensterzahlen wurden per Testlauf verifiziert, nicht nur ueberschlagen).
        var variant = WithReplacedStatements(
            [1, 6, 11, 17],
            ["int b = x * 9;", "int g = c * 9;", "int l = k * 9;", "int r = q * 9;"]);
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("A.cs", BuildMethod("A", "ComputeBase", BaseStatements)),
            ("B.cs", BuildMethod("B", "ComputeFuzzy", variant)));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.InRange(cluster.Score, 0.65, 0.7999);
        Assert.Equal(DuplicateSimilarityBucket.Fuzzy, cluster.Bucket);
    }

    [Fact]
    public async Task ScanAsync_SixStatementsChanged_FallsBelowFuzzyThreshold_NoCluster()
    {
        // Sechs weit auseinanderliegende ersetzte Statements -> ~66 von ~138 N-Grammen veraendert,
        // Jaccard ~0.35 -- klar unterhalb der fuzzy-Schwelle (0.65), kein Cluster.
        var variant = WithReplacedStatements(
            [0, 3, 6, 9, 12, 18],
            ["int a = x * 11;", "int d = x * 12;", "int g = a * 13;", "int j = a * 14;", "int m = a * 15;", "int s = a * 16;"]);
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("A.cs", BuildMethod("A", "ComputeBase", BaseStatements)),
            ("B.cs", BuildMethod("B", "ComputeDifferent", variant)));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    // ── Transitive Cluster-Bildung (A~B, B~C ⇒ Cluster {A,B,C}) ──────────────────────────────

    [Fact]
    public async Task ScanAsync_ChainOfSimilarMethods_FormsSingleTransitiveCluster()
    {
        var variantB = WithReplacedStatements([8], ["int i = a * 7;"]);
        var variantC = WithReplacedStatements([14], ["int o = a * 8;"]);
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("A.cs", BuildMethod("A", "ComputeBase", BaseStatements)),
            ("B.cs", BuildMethod("B", "ComputeNearB", variantB)),
            ("C.cs", BuildMethod("C", "ComputeNearC", variantC)));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(3, cluster.Members.Count);
    }

    // ── 5 kuenstliche Nicht-Klone (aehnliche Struktur, unterschiedliche Semantik) ────────────

    [Fact]
    public async Task ScanAsync_TwoDisposeMethodsWithDifferentFields_NoCluster()
    {
        const string classA = """
            public sealed class ResourceA : System.IDisposable
            {
                private System.IO.Stream? _stream;
                private System.Threading.Mutex? _mutex;
                private bool _disposed;

                public void Dispose()
                {
                    if (_disposed) return;
                    _stream?.Flush();
                    _stream?.Dispose();
                    _stream = null;
                    _mutex?.ReleaseMutex();
                    _mutex?.Dispose();
                    _mutex = null;
                    _disposed = true;
                }
            }
            """;
        const string classB = """
            public sealed class ResourceB : System.IDisposable
            {
                private System.Net.Sockets.Socket? _socket;
                private System.Timers.Timer? _timer;
                private bool _closed;

                public void Dispose()
                {
                    if (_closed) return;
                    _socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both);
                    _socket?.Close();
                    _socket = null;
                    _timer?.Stop();
                    _timer?.Dispose();
                    _timer = null;
                    _closed = true;
                }
            }
            """;
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path, ("A.cs", classA), ("B.cs", classB));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    private static string BuildCustomMethod(string className, string methodName, IReadOnlyList<string> statements)
    {
        var body = string.Join("\n        ", statements);
        return $$"""
            public static class {{className}}
            {
                public static int {{methodName}}(int x)
                {
                    {{body}}
                }
            }
            """;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ScanAsync_StructurallySimilarButSemanticallyDifferentPairs_NoCluster(int pairIndex)
    {
        // Distinkte Variablen-Buchstaben je Seite (p1..p5 vs q1..q5) — verhindert zufaellige
        // Grenz-N-Gramm-Uebereinstimmungen durch wiederverwendete Bezeichner-Konvention und macht
        // die Dissimilaritaet unabhaengig von Fenster-Ueberlappungs-Schaetzungen.
        var pairs = new (string A, string B)[]
        {
            (
                BuildCustomMethod("ValidatorA", "Validate",
                    ["if (x < 0) { return -1; }", "int p1 = x + 100;", "int p2 = p1 * 3;", "int p3 = p2 - 7;", "int p4 = p3 / 2;", "return p4;"]),
                BuildCustomMethod("ValidatorB", "Validate",
                    ["if (x > 1000) { return -2; }", "int q1 = x - 50;", "int q2 = q1 / 4;", "int q3 = q2 + 11;", "int q4 = q3 * 9;", "return q4;"])
            ),
            (
                BuildCustomMethod("ParserA", "Parse",
                    ["int p1 = x % 7;", "int p2 = p1 ^ 3;", "int p3 = p2 << 1;", "int p4 = p3 | p1;", "int p5 = p4 + p2;", "return p5;"]),
                BuildCustomMethod("ParserB", "Parse",
                    ["int q1 = x & 15;", "int q2 = q1 >> 2;", "int q3 = q2 + q1;", "int q4 = q3 % 5;", "int q5 = q4 - q2;", "return q5;"])
            ),
            (
                BuildCustomMethod("FormatterA", "Format",
                    ["int p1 = x * x;", "int p2 = p1 + x;", "int p3 = p2 * p2;", "int p4 = p3 - p1;", "int p5 = p4 % 1000;", "return p5;"]),
                BuildCustomMethod("FormatterB", "Format",
                    ["int q1 = x + 7;", "int q2 = q1 - 3;", "int q3 = q2 + 9;", "int q4 = q3 - 5;", "int q5 = q4 + 2;", "return q5;"])
            ),
            (
                BuildCustomMethod("CacheA", "Lookup",
                    ["int p1 = x * 2 + 1;", "int p2 = p1 % 97;", "int p3 = p2 + 13;", "int p4 = p3 * 17;", "int p5 = p4 - p1;", "return p5;"]),
                BuildCustomMethod("CacheB", "Lookup",
                    ["int q1 = x / 3 - 1;", "int q2 = q1 % 53;", "int q3 = q2 - 19;", "int q4 = q3 * 23;", "int q5 = q4 + q1;", "return q5;"])
            ),
        };

        var (bodyA, bodyB) = pairs[pairIndex];
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path, ("A.cs", bodyA), ("B.cs", bodyB));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    // ── Historischer Regressionstest: McpJsonOptions-Duplikations-Fall (nachgebildet) ────────

    /// <summary>
    /// Nachgebildet nach dem realen <c>JsonSerializerOptions</c>-Duplikationsfall (siehe
    /// Doc-Kommentar in <c>src/AiNetLinter/Mcp/McpJsonOptions.cs</c>) — 4 Methoden, die jeweils
    /// ein Objekt mit denselben 5 Optionen instanziieren. Beweisstueck, dass die Engine diesen
    /// Anwendungsfall tatsaechlich loest: MUSS als ein exact/near-Cluster erkannt werden.
    /// </summary>
    [Fact]
    public async Task ScanAsync_FourMethodsInstantiatingSameOptionsObject_DetectedAsOneCluster()
    {
        const string stubTypes = """
            public sealed class SerializerOptionsStub
            {
                public NamingPolicyStub? PropertyNamingPolicy { get; set; }
                public bool WriteIndented { get; set; }
                public bool IgnoreNullValues { get; set; }
                public int MaxDepth { get; set; }
                public EncoderStub? Encoder { get; set; }
            }
            public sealed class NamingPolicyStub
            {
                public static NamingPolicyStub CamelCase { get; } = new();
            }
            public sealed class EncoderStub
            {
                public static EncoderStub Default { get; } = new();
            }
            """;

        static string BuildOptionsMethod(string className, string methodName) => $$"""
            public static class {{className}}
            {
                public static SerializerOptionsStub {{methodName}}()
                {
                    var options = new SerializerOptionsStub
                    {
                        PropertyNamingPolicy = NamingPolicyStub.CamelCase,
                        WriteIndented = false,
                        IgnoreNullValues = true,
                        MaxDepth = 32,
                        Encoder = EncoderStub.Default,
                    };
                    return options;
                }
            }
            """;

        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("Stubs.cs", stubTypes),
            ("HandlerA.cs", BuildOptionsMethod("HandlerA", "BuildOptions")),
            ("HandlerB.cs", BuildOptionsMethod("HandlerB", "BuildOptions")),
            ("HandlerC.cs", BuildOptionsMethod("HandlerC", "BuildOptions")),
            ("HandlerD.cs", BuildOptionsMethod("HandlerD", "BuildOptions")));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(4, cluster.Members.Count);
        Assert.True(cluster.Bucket is DuplicateSimilarityBucket.Exact or DuplicateSimilarityBucket.Near);
    }
}
