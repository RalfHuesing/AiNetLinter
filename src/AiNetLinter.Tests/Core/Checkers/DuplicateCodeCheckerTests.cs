#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Core.Checkers;
using AiNetLinter.Metrics;
using AiNetLinter.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AiNetLinter.Tests.Core.Checkers;

/// <summary>
/// Tests fuer <see cref="DuplicateCodeChecker"/> — solution-weite Nachpruefung, die
/// <see cref="DuplicateDetection.DuplicateDetectionEngine"/> auf <see cref="AnalysisState.Solution"/>
/// aufruft und nur <c>exact</c>-Cluster (nicht <c>near</c>/<c>fuzzy</c>) als je eine
/// <see cref="RuleViolation"/> pro Cluster meldet. Nutzt dieselbe 20-Statement-Basismethode/
/// Kalibrierung wie <see cref="DuplicateDetection.DuplicateDetectionEngineTests"/> fuer die
/// exact-/near-Faelle.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DuplicateCodeCheckerTests : IDisposable
{
    private readonly string _tempDir;

    public DuplicateCodeCheckerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ainetlinter-dupchecker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    private static string BuildMethod(string className, string methodName) =>
        TestHelper.BuildCalibratedMethod(className, methodName);

    private static Solution CreateAdhocSolution(string baseDir, params (string FileName, string Content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var runtimeAsm = MetadataReference.CreateFromFile(typeof(System.Runtime.GCLatencyMode).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
            projectId, VersionStamp.Create(), "TestProject", "TestProject", LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib, runtimeAsm })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create(), filePath: Path.Combine(baseDir, "Test.slnx"));
        var solution = workspace.AddSolution(solutionInfo).AddProject(projectInfo);
        foreach (var file in files)
        {
            var fullPath = Path.Combine(baseDir, file.FileName);
            File.WriteAllText(fullPath, file.Content);
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.FileName, file.Content, filePath: fullPath);
        }
        return solution;
    }

    private static AnalysisState CreateState(Solution solution) => new(
        solution,
        new ConcurrentBag<RuleViolation>(),
        new TestCoverageIndex(),
        new ConcurrentBag<ClassInfo>(),
        new ConcurrentBag<PartialClassPart>(),
        new ConcurrentDictionary<string, string>());

    private static Config CreateConfig(GlobalConfig global) => TestHelper.CreateDefaultConfig() with { Global = global };

    [Fact]
    public async Task RunAsync_TwoExactClones_ReportsOneViolationForCluster()
    {
        // Ein Duplikat-Fund ist EIN Befund (repraesentatives Cluster-Mitglied, analog
        // PostAnalysisChecks.RunMaxPartialClassFilesCheck), nicht eine Violation pro Mitglied —
        // siehe Klassen-Doc-Kommentar von DuplicateCodeChecker (Live-Dogfood-Befund 2026-08-11:
        // eine Violation pro Mitglied blies den Safeguard-Score auf dem eigenen Repo auf 0).
        var solution = CreateAdhocSolution(_tempDir,
            ("A.cs", BuildMethod("A", "ComputeOne")),
            ("B.cs", BuildMethod("B", "ComputeTwo")));
        var state = CreateState(solution);

        await DuplicateCodeChecker.RunAsync(state, CreateConfig(new GlobalConfig()));

        var violation = Assert.Single(state.Violations.Where(v => v.RuleName == LinterRuleIds.DuplicateCode));
        Assert.Contains("exact", violation.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_NearClusterOnly_ReportsNoViolations()
    {
        // Ein ersetztes Statement (gleiche Token-Anzahl) -> Jaccard ~0.85, sicher im near-Bucket
        // [0.80, 0.95) -- dieselbe Kalibrierung wie
        // DuplicateDetectionEngineTests.ScanAsync_OneStatementChanged_ClassifiesAsNear (identische
        // BaseStatements). near-Cluster werden bewusst NICHT mehr automatisch gemeldet (siehe
        // Klassen-Doc-Kommentar) -- weiterhin ueber find_duplicates/den Drift-Audit-Skill sichtbar.
        var variantStatements = (string[])TestHelper.CalibratedBaseStatements.Clone();
        variantStatements[8] = "int i = a * 7;";
        var variantBody = $$"""
            public static class B
            {
                public static int ComputeNear(int x)
                {
                    {{string.Join("\n            ", variantStatements)}}
                    return t;
                }
            }
            """;
        var solution = CreateAdhocSolution(_tempDir, ("A.cs", BuildMethod("A", "ComputeOne")), ("B.cs", variantBody));
        var state = CreateState(solution);

        await DuplicateCodeChecker.RunAsync(state, CreateConfig(new GlobalConfig()));

        Assert.Empty(state.Violations.Where(v => v.RuleName == LinterRuleIds.DuplicateCode));
    }

    [Fact]
    public async Task RunAsync_ViolationDetails_MentionOtherClusterMembers()
    {
        var solution = CreateAdhocSolution(_tempDir,
            ("A.cs", BuildMethod("A", "ComputeOne")),
            ("B.cs", BuildMethod("B", "ComputeTwo")));
        var state = CreateState(solution);

        await DuplicateCodeChecker.RunAsync(state, CreateConfig(new GlobalConfig()));

        var violation = state.Violations.First(v => v.RuleName == LinterRuleIds.DuplicateCode);
        Assert.Contains("ComputeOne", violation.Details + violation.Guidance);
        Assert.Contains("ComputeTwo", violation.Details + violation.Guidance);
    }

    [Fact]
    public async Task RunAsync_DisabledViaConfig_ReportsNoViolations()
    {
        var solution = CreateAdhocSolution(_tempDir,
            ("A.cs", BuildMethod("A", "ComputeOne")),
            ("B.cs", BuildMethod("B", "ComputeTwo")));
        var state = CreateState(solution);

        await DuplicateCodeChecker.RunAsync(state, CreateConfig(new GlobalConfig { EnableDuplicateCodeCheck = false }));

        Assert.Empty(state.Violations.Where(v => v.RuleName == LinterRuleIds.DuplicateCode));
    }

    [Fact]
    public async Task RunAsync_FuzzyClusterOnly_ReportsNoViolations()
    {
        // Sechs weit auseinanderliegende Statement-Swaps (siehe DuplicateDetectionEngineTests-
        // Kalibrierung) druecken den Score klar unter die fuzzy-Schwelle (0.65) -> ueberhaupt kein
        // Cluster, erst recht keine Violation.
        var variantStatements = (string[])TestHelper.CalibratedBaseStatements.Clone();
        variantStatements[0] = "int a = x * 11;";
        variantStatements[3] = "int d = x * 12;";
        variantStatements[6] = "int g = a * 13;";
        variantStatements[9] = "int j = a * 14;";
        variantStatements[12] = "int m = a * 15;";
        variantStatements[18] = "int s = a * 16;";
        var variantBody = $$"""
            public static class B
            {
                public static int ComputeDifferent(int x)
                {
                    {{string.Join("\n            ", variantStatements)}}
                    return t;
                }
            }
            """;
        var solution = CreateAdhocSolution(_tempDir, ("A.cs", BuildMethod("A", "ComputeOne")), ("B.cs", variantBody));
        var state = CreateState(solution);

        await DuplicateCodeChecker.RunAsync(state, CreateConfig(new GlobalConfig()));

        Assert.Empty(state.Violations.Where(v => v.RuleName == LinterRuleIds.DuplicateCode));
    }

    // Zweiter, strukturell unabhaengiger Klon-Familie (andere Bezeichner-Vokabel, andere
    // Operatoren, anderer Parametername) — bewusst ohne jede Token-Ueberschneidung zur ersten
    // Familie (<see cref="BaseStatements"/>), damit die beiden Cluster garantiert getrennt bleiben
    // und nicht durch zufaellige N-Gramm-Ueberlappung zu einem grossen Cluster verschmelzen.
    private static readonly string[] SecondFamilyStatements =
    [
        "int uu = yy ^ 11;", "int vv = yy ^ 12;", "int ww = yy ^ 13;", "int xx2 = yy ^ 14;", "int zz = yy ^ 15;",
        "int aa2 = uu | vv;", "int bb2 = ww | xx2;", "int cc2 = zz | aa2;", "int dd2 = bb2 | cc2;", "int ee2 = dd2 & uu;",
        "int ff2 = ee2 & vv;", "int gg2 = ff2 & ww;", "int hh2 = gg2 & xx2;", "int ii2 = hh2 & zz;", "int jj2 = ii2 << 1;",
        "int kk2 = jj2 >> 1;", "int ll2 = kk2 | 1;", "int mm2 = ll2 | 2;", "int nn2 = mm2 | 3;", "int oo2 = nn2 | 4;",
    ];

    private static string BuildSecondFamilyMethod(string className, string methodName) => $$"""
        public static class {{className}}
        {
            public static int {{methodName}}(int yy)
            {
                {{string.Join("\n            ", SecondFamilyStatements)}}
                return oo2;
            }
        }
        """;

    [Fact]
    public async Task RunAsync_MaxResults_CapsNumberOfReportedClusters()
    {
        // Zwei unabhaengige exakte Klon-Paare aus unterschiedlichen Bezeichner-/Operator-Familien
        // (4 Dateien) -> 2 getrennte Cluster. MaxResults=1 kappt auf genau 1 Cluster -> genau
        // 1 Violation (ein Fund pro Cluster, nicht pro Mitglied).
        var solution = CreateAdhocSolution(_tempDir,
            ("A1.cs", BuildMethod("A1", "F1")),
            ("A2.cs", BuildMethod("A2", "F2")),
            ("B1.cs", BuildSecondFamilyMethod("B1", "G1")),
            ("B2.cs", BuildSecondFamilyMethod("B2", "G2")));
        var state = CreateState(solution);

        await DuplicateCodeChecker.RunAsync(state, CreateConfig(new GlobalConfig { DuplicateCodeMaxResults = 1 }));

        var violations = state.Violations.Where(v => v.RuleName == LinterRuleIds.DuplicateCode).ToList();
        Assert.Single(violations);
    }

    [Fact]
    public async Task RunAsync_SuppressCommentInEitherFile_ReportsNoViolations()
    {
        // '// ainetlinter-disable DuplicateCode' in EINER der beiden beteiligten Dateien reicht
        // (nicht zwingend in der Datei des repraesentativen Cluster-Mitglieds) — siehe
        // DuplicateCodeChecker.IsClusterSuppressed-Doc-Kommentar: der Fund ist eine Aussage ueber
        // die Beziehung zwischen den Methoden, keine pro Datei unabhaengige.
        var suppressedB = "// ainetlinter-disable DuplicateCode -- bewusst strukturell gleich\n"
            + BuildMethod("B", "ComputeTwo");
        var solution = CreateAdhocSolution(_tempDir,
            ("A.cs", BuildMethod("A", "ComputeOne")),
            ("B.cs", suppressedB));
        var state = CreateState(solution);

        await DuplicateCodeChecker.RunAsync(state, CreateConfig(new GlobalConfig()));

        Assert.Empty(state.Violations.Where(v => v.RuleName == LinterRuleIds.DuplicateCode));
    }

    [Fact]
    public async Task RunAsync_NoClones_ReportsNoViolations()
    {
        var solution = CreateAdhocSolution(_tempDir, ("A.cs", BuildMethod("A", "ComputeOne")));
        var state = CreateState(solution);

        await DuplicateCodeChecker.RunAsync(state, CreateConfig(new GlobalConfig()));

        Assert.Empty(state.Violations);
    }
}
