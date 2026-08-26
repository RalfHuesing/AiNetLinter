#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisToolTests
{
    [Fact]
    public async Task InspectAssembly_ReturnsPublicApiWithOverloadsGenericsAndAttributes()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var assemblyPath = EmitAssembly(temp, "ApiProbe", """
            using System;
            namespace Probe.Api;
            [Obsolete]
            public sealed class PublicApi
            {
                public string Name { get; set; } = "";
                public event EventHandler? Changed;
                public int Convert(string value) => value.Length;
                public int Convert(int value) => value;
                public T Echo<T>(T value) where T : class => value;
                private void Hidden() { }
            }
            """);

        var result = await InspectAssemblyTool.ExecuteAsync(
            state: null,
            new InspectAssemblyArguments(assemblyPath, "Probe.Api", "PublicApi", null, true, 100),
            CancellationToken.None);

        var payload = Deserialize<InspectAssemblyPayload>(result);
        var type = Assert.Single(payload.Types);
        Assert.Equal("Probe.Api", type.Namespace);
        Assert.Equal("PublicApi", type.Name);
        Assert.Contains("Probe.Api", payload.Namespaces);
        Assert.Contains(type.Members, member => member.Name == "Name" && member.Kind == "property");
        Assert.Contains(type.Members, member => member.Name == "Changed" && member.Kind == "event");
        Assert.DoesNotContain(type.Members, member => member.Name is "get_Name" or "set_Name" or "add_Changed");
        Assert.Equal(3, type.Members.Count(member => member.Name is "Convert" or "Echo"));
        Assert.Contains(type.Members, member => member.Name == "Echo" && member.GenericParameters.Contains("T") && member.Constraints.Any(constraint => constraint.StartsWith("T:", StringComparison.Ordinal)));
        Assert.Contains(type.Attributes, attribute => attribute.Contains("Obsolete", StringComparison.Ordinal));
        Assert.Equal("complete", payload.Completeness);
    }

    [Fact]
    public async Task InspectAssembly_UsesResultLimitAndIgnoresUnrelatedInvalidDlls()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var assemblyPath = EmitAssembly(temp, "LimitedProbe", """
            namespace Probe;
            public sealed class First { }
            public sealed class Second { }
            """);
        File.WriteAllBytes(temp.GetPath("unrelated.dll"), [0, 1, 2, 3]);

        var result = await InspectAssemblyTool.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 1),
            CancellationToken.None);
        var payload = Deserialize<InspectAssemblyPayload>(result);

        Assert.Single(payload.Types);
        Assert.Equal(2, payload.TotalTypes);
        Assert.True(payload.Truncated);
        Assert.Equal("complete", payload.Completeness);
        Assert.DoesNotContain(payload.Diagnostics, diagnostic => diagnostic.Contains("unrelated.dll", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InspectAssembly_RejectsRelativeAndMissingPathsWithoutRuntimeLoading()
    {
        var relative = await InspectAssemblyTool.ExecuteAsync(null, new InspectAssemblyArguments("relative.dll", null, null, null, true, 100), CancellationToken.None);
        Assert.Contains("INVALID_ARGUMENT", TextOf(relative), StringComparison.Ordinal);

        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var missing = await InspectAssemblyTool.ExecuteAsync(null, new InspectAssemblyArguments(Path.Combine(temp.DirectoryPath, "missing.dll"), null, null, null, true, 100), CancellationToken.None);
        Assert.Contains("nicht gefunden", TextOf(missing), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindAssemblyExtensions_UsesRoslynExtensionMarkerAndFilters()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var assemblyPath = EmitAssembly(temp, "ExtensionsProbe", """
            namespace Probe.Extensions;
            public static class Extensions
            {
                public static string Mark(this object value) => value.ToString()!;
                public static string Other(this string value) => value;
                public static T Generic<T>(this T value) where T : class => value;
                public static string NotAnExtension(string value) => value;
            }
            """);

        var result = await FindAssemblyExtensionsTool.ExecuteAsync(null, new FindAssemblyExtensionsArguments(assemblyPath, null, "Mark", "Probe.Extensions", 100), CancellationToken.None);
        var payload = Deserialize<FindAssemblyExtensionsPayload>(result);

        var extension = Assert.Single(payload.Extensions);
        Assert.Equal("Mark", extension.Name);
        Assert.Equal("not_decidable", extension.Applicability);
        Assert.Equal("complete", payload.Completeness);
    }

    [Fact]
    public async Task FindAssemblyExtensions_UsesConsumerCompilationForApplicability()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var assemblyPath = EmitAssembly(temp, "ConsumerExtensions", """
            namespace Probe.Extensions;
            public static class Extensions
            {
                public static string Mark(this object value) => value.ToString()!;
                public static string StringOnly(this string value) => value;
            }
            """);
        using var consumer = RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(temp.DirectoryPath, "Consumer.slnx"),
            new ProjectSpec("Consumer", [("Consumer.cs", "namespace Consumer; public sealed class Person { }")]));
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: consumer.Solution)));

        var result = await FindAssemblyExtensionsTool.ExecuteAsync(
            server,
            new FindAssemblyExtensionsArguments(assemblyPath, "Consumer.Person", null, null, 100),
            CancellationToken.None);
        var payload = Deserialize<FindAssemblyExtensionsPayload>(result);

        Assert.Equal("applicable", Assert.Single(payload.Extensions, extension => extension.Name == "Mark").Applicability);
        Assert.Equal("not_applicable", Assert.Single(payload.Extensions, extension => extension.Name == "StringOnly").Applicability);
        Assert.Equal("Consumer", payload.ConsumerProject);
    }

    [Fact]
    public async Task InspectAssembly_MissingDependencyMarksPartialResult()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var dependencyPath = EmitAssembly(temp, "MissingDependency", "namespace Missing; public sealed class DependencyType { }");
        var assemblyPath = EmitAssembly(temp, "PartialProbe", "namespace Probe; public sealed class UsesMissing { public Missing.DependencyType Value { get; } = new(); }", dependencyPath);
        File.Delete(dependencyPath);

        var result = await InspectAssemblyTool.ExecuteAsync(null, new InspectAssemblyArguments(assemblyPath, null, null, null, true, 100), CancellationToken.None);
        var payload = Deserialize<InspectAssemblyPayload>(result);

        Assert.Equal("partial", payload.Completeness);
        Assert.Contains(payload.Diagnostics, diagnostic => diagnostic.Contains("MissingDependency", StringComparison.Ordinal));
        Assert.Contains("partial", TextOf(result), StringComparison.OrdinalIgnoreCase);
    }

    private static string EmitAssembly(TestTempDirectory temp, string name, string source, params string[] additionalReferences)
    {
        var outputPath = temp.GetPath(name + ".dll");
        var references = RoslynTestSolutionFactory.CoreReferences
            .Concat(additionalReferences.Select(path => MetadataReference.CreateFromFile(path)))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(outputPath);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return outputPath;
    }

    private static T Deserialize<T>(CallToolResult result)
    {
        Assert.NotNull(result.StructuredContent);
        return JsonSerializer.Deserialize<T>(result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
    }

    private static string TextOf(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
