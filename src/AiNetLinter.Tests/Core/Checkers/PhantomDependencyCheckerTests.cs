#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core.Checkers;

[Trait("Category", "Unit")]
public sealed class PhantomDependencyCheckerTests
{
    [Fact]
    public void PhantomDependencyChecker_Reports_ReflectionInvocation()
    {
        var (tree, model) = TestHelper.ParseCode(@"
using System;
public class Foo
{
    public void Load()
    {
        var type = Type.GetType(""SomeClass"");
    }
}");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { DetectAndBanPhantomDependencies = true } },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        PhantomDependencyChecker.CheckPhantomReflection(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("DetectAndBanPhantomDependencies", ctx.Violations.First().RuleName);
    }

    /// <summary>
    /// Baut bewusst eine Compilation MIT einem unaufloesbaren using (CS0246) — anders als
    /// <see cref="TestHelper.ParseCode"/>, das bei Compile-Fehlern wirft. Simuliert exakt den
    /// Zustand, den <see cref="PhantomDependencyChecker.CheckPhantomNamespace"/> erkennen soll.
    /// </summary>
    private static UsingDirectiveSyntax GetUnresolvableUsingNode(out SemanticModel model)
    {
        const string source = @"
using TotallyMissing.PhantomPackage;
public class Foo
{
}";
        var tree = CSharpSyntaxTree.ParseText(source);
        var references = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create("PhantomTestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(references)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        model = compilation.GetSemanticModel(tree);
        return tree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>().First();
    }

    [Fact]
    public void PhantomDependencyChecker_Reports_UnresolvableUsing_WhenProjectLoadedCleanly()
    {
        var node = GetUnresolvableUsingNode(out var model);
        var ctx = TestHelper.CreateContextWithLoadDiagnostics(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { DetectAndBanPhantomDependencies = true } },
            semanticModel: model,
            projectHasLoadDiagnostics: false
        );

        // Projekt lud sauber (kein Lade-Problem) — das using selbst ist der echte, isolierte
        // Phantom-Fall und muss weiterhin gemeldet werden.
        PhantomDependencyChecker.CheckPhantomNamespace(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("DetectAndBanPhantomDependencies", ctx.Violations.First().RuleName);
    }

    [Fact]
    public void PhantomDependencyChecker_SuppressesUnresolvableUsing_WhenProjectHasLoadDiagnostics()
    {
        var node = GetUnresolvableUsingNode(out var model);
        var ctx = TestHelper.CreateContextWithLoadDiagnostics(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { DetectAndBanPhantomDependencies = true } },
            semanticModel: model,
            projectHasLoadDiagnostics: true
        );

        // Gleiches unaufloesbares using wie im "sauber geladen"-Fall oben — hier aber ein
        // Folgefehler eines Projekt-Lade-Problems (z. B. fehlender Restore), kein echter Phantom.
        PhantomDependencyChecker.CheckPhantomNamespace(node, ctx);

        Assert.Empty(ctx.Violations);
    }
}
