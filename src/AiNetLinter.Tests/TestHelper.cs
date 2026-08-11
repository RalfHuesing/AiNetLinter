#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
}
