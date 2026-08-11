#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;
using AiNetLinter.Models;

namespace AiNetLinter.Tests;

internal static class TestHelper
{
    public static Config CreateDefaultConfig()
    {
        return new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig()
        };
    }
    public static (SyntaxTree Tree, SemanticModel Model) ParseCode(string source)
    {
        try
        {
            _ = typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly;
            _ = typeof(System.Dynamic.DynamicObject).Assembly;
        }
        catch {}

        var tree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(references)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Any())
        {
            throw new Exception("Compilation errors:\n" + string.Join("\n", errors));
        }

        var semanticModel = compilation.GetSemanticModel(tree);
        return (tree, semanticModel);
    }

    public static CheckerContext CreateContext(
        Config? config = null,
        SemanticModel? semanticModel = null,
        bool isTestFile = false,
        string filePath = "Test.cs",
        string? projectName = null)
    {
        config ??= new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig()
        };

        if (semanticModel == null)
        {
            var (_, model) = ParseCode("// empty");
            semanticModel = model;
        }

        return new CheckerContext(filePath, config, semanticModel, projectName, new DocumentLoadState(isTestFile, ProjectHasLoadDiagnostics: false));
    }

    /// <summary>
    /// Wie <see cref="CreateContext"/>, aber mit explizitem <see cref="DocumentLoadState.ProjectHasLoadDiagnostics"/> —
    /// eigener Overload statt eines zweiten bool-Parameters auf <see cref="CreateContext"/>, um dessen
    /// <c>MaxBoolParameterCount</c>-Limit (1) nicht zu ueberschreiten.
    /// </summary>
    public static CheckerContext CreateContextWithLoadDiagnostics(
        Config config,
        SemanticModel semanticModel,
        bool projectHasLoadDiagnostics,
        string filePath = "Test.cs",
        string? projectName = null)
    {
        return new CheckerContext(filePath, config, semanticModel, projectName,
            new DocumentLoadState(IsTestFile: false, projectHasLoadDiagnostics));
    }

    /// <summary>
    /// Best-effort-Aufraeumen eines Test-Temp-Verzeichnisses fuer <c>IDisposable</c>-Test-Fixtures —
    /// zentral statt in jeder Fixture-Klasse dupliziert (IOException/UnauthorizedAccessException
    /// sind erwartbar, wenn der Handle noch kurz haengt, z. B. unter Windows-Datei-Locking).
    /// </summary>
    public static void TryDeleteDirectoryRecursive(string path)
    {
        try
        {
            if (System.IO.Directory.Exists(path)) System.IO.Directory.Delete(path, true);
        }
        catch (System.IO.IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Loescht ein Temp-Verzeichnis rekursiv ohne Fehlerbehandlung (wirft bei Fehlschlag) — fuer
    /// <c>IDisposable.Dispose()</c>-Implementierungen von Test-Klassen, die ein im Konstruktor
    /// erzeugtes Temp-Verzeichnis wieder entfernen. Zentral statt in mehreren Testklassen dupliziert.
    /// </summary>
    public static void DeleteDirectoryIfExists(string path)
    {
        if (System.IO.Directory.Exists(path))
            System.IO.Directory.Delete(path, recursive: true);
    }

    /// <summary>
    /// Loescht eine einzelne Datei, falls vorhanden — kein Fehler wenn sie nicht existiert.
    /// Zentral statt in mehreren Testklassen dupliziert.
    /// </summary>
    public static void DeleteFileIfExists(string path)
    {
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }
    }

    /// <summary>
    /// Best-effort-Aufraeumen einer Log-Datei samt ihres Elternverzeichnisses (z. B. MCP-Call-Log-Tests)
    /// — schluckt alle Fehler, kein Test-Fail durch fehlgeschlagenes Cleanup. Zentral statt in
    /// mehreren Testklassen dupliziert.
    /// </summary>
    public static void TryDeleteLogFileAndDirectory(string path)
    {
        try
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            var dir = System.IO.Path.GetDirectoryName(path);
            if (dir is not null && System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // best-effort cleanup, kein Test-Fail
        }
    }

    /// <summary>
    /// Sucht die <c>.slnx</c>-Datei ausgehend vom Testlauf-Basisverzeichnis aufwaerts im Dateibaum.
    /// Zentral statt in mehreren Testklassen dupliziert.
    /// </summary>
    public static string? FindSlnxFile()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var files = dir.GetFiles("*.slnx");
            if (files.Length > 0) return files[0].FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Kalibrierte 20-Statement-Basismethode fuer Clone-Detection-Tests (<c>DuplicateCodeCheckerTests</c>,
    /// <c>DuplicateDetectionScannerTests</c>, <c>DuplicateDetectionToolTests</c>) — die Statement-Anzahl
    /// ist bewusst so gewaehlt, dass die generierte Methode oberhalb des <c>minTokens</c>-Schwellwerts
    /// der Clone-Detection liegt. Zentral statt in mehreren Testklassen dupliziert.
    /// </summary>
    public static string BuildCalibratedMethod(string className, string methodName) => $$"""
        public static class {{className}}
        {
            public static int {{methodName}}(int x)
            {
                {{string.Join("\n            ", CalibratedBaseStatements)}}
                return t;
            }
        }
        """;

    /// <summary>
    /// Rohe Statement-Liste hinter <see cref="BuildCalibratedMethod"/> — oeffentlich, damit Tests
    /// gezielte Statement-Swaps fuer near-/fuzzy-Kalibrierung bauen koennen (<c>Clone()</c> vor
    /// Mutation, dieses Array bleibt unveraendert).
    /// </summary>
    public static readonly string[] CalibratedBaseStatements =
    [
        "int a = x + 1;", "int b = x + 2;", "int c = x + 3;", "int d = x + 4;", "int e = x + 5;",
        "int f = a + b;", "int g = c + d;", "int h = e + f;", "int i = g + h;", "int j = i - a;",
        "int k = j - b;", "int l = k - c;", "int m = l - d;", "int n = m - e;", "int o = n * 2;",
        "int p = o / 2;", "int q = p + 1;", "int r = q + 2;", "int s = r + 3;", "int t = s + 4;",
    ];

    /// <summary>
    /// Leichtgewichtiges <see cref="SemanticModel"/> fuer isolierte Checker-Tests, die nur Kern-BCL-
    /// Typen aufloesen muessen (kein voller AppDomain-Assembly-Scan wie <see cref="ParseCode"/>).
    /// Zentral statt in mehreren Testklassen dupliziert.
    /// </summary>
    public static SemanticModel CreateSemanticModel(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetSemanticModel(tree);
    }

    /// <summary>
    /// Baut eine minimale In-Memory-Solution mit genau einem Dokument, dessen Text-Zugriff ueber
    /// <see cref="AiNetLinter.Tests.Fixtures.ThrowingTextLoader"/> immer eine Exception wirft — simuliert deterministisch eine
    /// reale LinterEngine-Malfunction (z. B. Quelldatei zwischen Indexierung und Analyse vom
    /// Dateisystem verschwunden), statt auf einen fragilen realen Timing-Race zu warten. Zentral
    /// statt in mehreren MCP-Tool-Testklassen dupliziert (<c>GetViolationsToolTests</c>,
    /// <c>SafeguardToolTests</c>, <c>SafeguardScannerTests</c>, <c>PatternDetectScannerTests</c>).
    /// <paramref name="probeDir"/> muss real auf der Platte existieren und wird vom Aufrufer
    /// angelegt/aufgeraeumt — die dort erzeugte Datei muss real sein (sonst entfernt ein
    /// Refresh-Sweep sie schon vor der Analyse), ihr Inhalt ist irrelevant, weil
    /// <see cref="AiNetLinter.Tests.Fixtures.ThrowingTextLoader"/> den Text-Zugriff uebernimmt.
    /// </summary>
    public static Solution CreateFaultySolution(string probeDir)
    {
        var faultyPath = Path.Combine(probeDir, "Faulty.cs");
        File.WriteAllText(faultyPath, "class Faulty {}");

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId, VersionStamp.Create(), "FaultyProject", "FaultyProject", LanguageNames.CSharp);
        var solution = workspace.CurrentSolution.AddProject(projectInfo);

        var documentId = DocumentId.CreateNewId(projectId);
        var documentInfo = DocumentInfo.Create(
            documentId, "Faulty.cs", filePath: faultyPath, loader: new AiNetLinter.Tests.Fixtures.ThrowingTextLoader());
        return solution.AddDocument(documentInfo);
    }
}
