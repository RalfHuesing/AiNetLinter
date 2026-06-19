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
    public static LinterConfig CreateDefaultConfig()
    {
        return new LinterConfig
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
        LinterConfig? config = null,
        SemanticModel? semanticModel = null,
        bool isTestFile = false,
        string filePath = "Test.cs",
        string? projectName = null)
    {
        config ??= new LinterConfig
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig()
        };

        if (semanticModel == null)
        {
            var (_, model) = ParseCode("// empty");
            semanticModel = model;
        }

        return new CheckerContext(filePath, config, semanticModel, isTestFile, projectName);
    }
}
