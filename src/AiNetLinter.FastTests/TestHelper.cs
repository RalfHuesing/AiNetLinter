#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;
using AiNetLinter.Models;

namespace AiNetLinter.FastTests;

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
}
