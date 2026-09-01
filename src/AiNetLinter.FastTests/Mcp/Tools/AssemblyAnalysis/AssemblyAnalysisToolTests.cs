#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
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
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "ApiProbe", """
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
        Assert.Equal("decompiled", payload.Origin?.OriginKind);
        Assert.NotNull(payload.Origin);
        Assert.Contains("source", payload.Origin!.GeneratedDocumentPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InspectAssembly_SupportsExactTypeMultipleMemberFiltersAndParameterDetails()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "FilterProbe", """
            namespace Probe.Api;
            public sealed class PublicApi
            {
                public bool Save(bool abortOnWarning, ref int changeCount, string mode = "safe") => true;
                public static ref readonly int ReadOnly(ref readonly int value) => ref value;
                public void Escaped(string value = "line\n\t", char quote = '\'') { }
                public void NullDefault(string? value = null) { }
                public string this[int index] => index.ToString();
                public void SaveExtra() { }
                public bool Validate() => true;
            }
            public sealed class PublicApiHelper
            {
                public void Save() { }
            }
            """);

        var result = await InspectAssemblyTool.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, "Probe.Api", "PublicApi", null, true, 100, true, ["Save", "Validate"], 10),
            CancellationToken.None);
        var payload = Deserialize<InspectAssemblyPayload>(result);

        var type = Assert.Single(payload.Types);
        Assert.Equal(2, type.TotalMembers);
        Assert.False(type.MembersTruncated);
        Assert.DoesNotContain(type.Members, member => member.Name == "SaveExtra");
        var save = Assert.Single(type.Members, member => member.Name == "Save");
        Assert.Contains("abortOnWarning", save.Signature, StringComparison.Ordinal);
        Assert.Equal(
            ["abortOnWarning", "changeCount", "mode"],
            save.Parameters.Select(parameter => parameter.Name).ToArray());
        Assert.Equal("bool", save.Parameters[0].Type);
        Assert.Equal("none", save.Parameters[0].RefKind);
        Assert.Equal("ref", save.Parameters[1].RefKind);
        Assert.True(save.Parameters[2].IsOptional);
        Assert.Equal("\"safe\"", save.Parameters[2].DefaultValue);

        var metadataResult = await InspectAssemblyTool.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, "Probe.Api", "PublicApi", null, true, 100, true, ["ReadOnly", "Escaped", "NullDefault", "this[]"], 10),
            CancellationToken.None);
        var metadataType = Assert.Single(Deserialize<InspectAssemblyPayload>(metadataResult).Types);
        var readOnly = Assert.Single(metadataType.Members, member => member.Name == "ReadOnly");
        Assert.Equal("ref readonly", readOnly.Parameters[0].RefKind);
        var escaped = Assert.Single(metadataType.Members, member => member.Name == "Escaped");
        Assert.Equal("\"line\\n\\t\"", escaped.Parameters[0].DefaultValue);
        Assert.Equal("'\\''", escaped.Parameters[1].DefaultValue);
        var nullDefault = Assert.Single(metadataType.Members, member => member.Name == "NullDefault");
        Assert.Equal("null", nullDefault.Parameters[0].DefaultValue);
        var indexer = Assert.Single(metadataType.Members, member => member.Name == "this[]");
        Assert.Equal("property", indexer.Kind);
        Assert.Equal("index", indexer.Parameters[0].Name);
        Assert.Equal("int", indexer.Parameters[0].Type);

        var limitedResult = await InspectAssemblyTool.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, "Probe.Api", "PublicApi", null, true, 100, true, ["Save", "Validate"], 1),
            CancellationToken.None);
        var limitedType = Assert.Single(Deserialize<InspectAssemblyPayload>(limitedResult).Types);
        Assert.Single(limitedType.Members);
        Assert.Equal(2, limitedType.TotalMembers);
        Assert.True(limitedType.MembersTruncated);
    }

    [Fact]
    public async Task InspectAssembly_UsesResultLimitAndIgnoresUnrelatedInvalidDlls()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "LimitedProbe", """
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
    public async Task InspectAssembly_GlobalResponseBudgetUsesOneTypedSelectionForTextAndJson()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-response-budget-");
        var types = Enumerable.Range(0, 180)
            .Select(index => $"public sealed class Type{index:D3} {{ public string Value{index:D3} => \"value\"; public void Reset{index:D3}(string input) {{ }} }}");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ResponseBudgetProbe",
            $"namespace Probe.Budget; {string.Join(Environment.NewLine, types)}");

        var result = await InspectAssemblyTool.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 1000, MaxMembers: 1000),
            CancellationToken.None);

        var payload = Deserialize<InspectAssemblyPayload>(result);
        var text = TextOf(result);
        var structuredBytes = Encoding.UTF8.GetByteCount(result.StructuredContent!.Value.GetRawText());

        Assert.True(payload.TotalTypes > payload.ShownCount || payload.Types.Any(type => type.MembersTruncated));
        Assert.True(payload.Truncated);
        Assert.Contains("responseBudget", payload.TruncatedBy);
        Assert.Equal(payload.Types.Count, payload.ShownCount);
        Assert.True(structuredBytes <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.Contains(payload.Types, type => type.Members.Count > 0);
        Assert.All(
            payload.Types.SelectMany(type => type.Members),
            member =>
            {
                Assert.NotNull(member.Name);
                Assert.NotNull(member.Signature);
                Assert.NotNull(member.Parameters);
                Assert.NotNull(member.GenericParameters);
                Assert.NotNull(member.Constraints);
                Assert.Contains(member.Signature, text, StringComparison.Ordinal);
            });
        Assert.All(
            payload.Types,
            type => Assert.Contains($"`{type.Namespace}.{type.Name}`", text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindAssemblyExtensions_GlobalResponseBudgetKeepsCountsAndSharedSelection()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-extension-budget-");
        var extensions = Enumerable.Range(0, 180)
            .Select(index => $"public static string Extend{index:D3}(this object value, string input) => input;");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ExtensionResponseBudgetProbe",
            $"namespace Probe.Budget; public static class Extensions {{ {string.Join(Environment.NewLine, extensions)} }}");

        var result = await FindAssemblyExtensionsTool.ExecuteAsync(
            null,
            new FindAssemblyExtensionsArguments(assemblyPath, null, null, null, 1000),
            CancellationToken.None);

        var payload = Deserialize<FindAssemblyExtensionsPayload>(result);
        var text = TextOf(result);
        var structuredBytes = Encoding.UTF8.GetByteCount(result.StructuredContent!.Value.GetRawText());

        Assert.True(payload.TotalExtensions > payload.ShownCount);
        Assert.True(payload.Truncated);
        Assert.Equal(payload.Extensions.Count, payload.ShownCount);
        Assert.Contains("responseBudget", payload.TruncatedBy);
        Assert.True(structuredBytes <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.All(
            payload.Extensions,
            extension =>
            {
                Assert.NotNull(extension.Name);
                Assert.NotNull(extension.Signature);
                Assert.NotNull(extension.Parameters);
                Assert.NotNull(extension.GenericParameters);
                Assert.NotNull(extension.Constraints);
                Assert.Contains(extension.Signature, text, StringComparison.Ordinal);
            });
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
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "ExtensionsProbe", """
            namespace Probe.Extensions;
            public static class Extensions
            {
                public static string Mark(this object value, int count) => value.ToString()!;
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
        Assert.Equal(["value", "count"], extension.Parameters.Select(parameter => parameter.Name).ToArray());
        Assert.Equal("complete", payload.Completeness);
    }

    [Fact]
    public async Task FindAssemblyExtensions_ReceiverFilterWithoutMatchIsIndependentOfConsumerProject()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "ConsumerExtensions", """
            namespace Probe.Extensions;
            public sealed class Person { }
            public static class Extensions
            {
                public static string Mark(this object value) => value.ToString()!;
                public static string StringOnly(this string value) => value;
                public static string PersonOnly(this Person value) => value.ToString()!;
            }
            """);

        var result = await FindAssemblyExtensionsTool.ExecuteAsync(
            null,
            new FindAssemblyExtensionsArguments(assemblyPath, "Consumer.Person", null, null, 100),
            CancellationToken.None);
        var payload = Deserialize<FindAssemblyExtensionsPayload>(result);

        Assert.Empty(payload.Extensions);
        Assert.Equal(0, payload.TotalExtensions);
        Assert.Null(payload.ConsumerProject);
    }

    [Fact]
    public async Task FindAssemblyExtensions_ReceiverFilterMatchesUnqualifiedQualifiedAndGlobalPrefixOrdinal()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "ReceiverProbe", """
            namespace Probe.Extensions;
            public sealed class Person { }
            public static class Extensions
            {
                public static string Mark(this object value) => value.ToString()!;
                public static string StringOnly(this string value) => value;
                public static string PersonOnly(this Person value) => value.ToString()!;
            }
            """);

        var cases = new[]
        {
            ("Object", new[] { "Mark" }),
            ("Person", new[] { "PersonOnly" }),
            ("Probe.Extensions.Person", new[] { "PersonOnly" }),
            ("global::Probe.Extensions.Person", new[] { "PersonOnly" }),
            ("person", Array.Empty<string>()),
            ("string", Array.Empty<string>()),
        };

        foreach (var (receiverType, expectedNames) in cases)
        {
            var result = await FindAssemblyExtensionsTool.ExecuteAsync(
                null,
                new FindAssemblyExtensionsArguments(assemblyPath, receiverType, null, null, 100),
                CancellationToken.None);
            var payload = Deserialize<FindAssemblyExtensionsPayload>(result);

            Assert.Equal(receiverType, payload.ReceiverType);
            Assert.Equal(expectedNames, payload.Extensions.Select(extension => extension.Name).ToArray());
        }
    }

    [Fact]
    public async Task InspectAssembly_WithConsumerSolution_ResolvesAssemblyDirectoryDependencies()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(temp, "ConsumerDependency", "namespace Dependency; public sealed class Value { }");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ConsumerTarget",
            "namespace Target; public sealed class UsesDependency { public Dependency.Value Value { get; } = new(); }",
            dependencyPath);
        using var consumer = RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(temp.DirectoryPath, "Consumer.slnx"),
            new ProjectSpec("Consumer", [("Consumer.cs", "namespace Consumer; public sealed class Marker { }")]));
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: consumer.Solution)));

        var result = await InspectAssemblyTool.ExecuteAsync(
            server,
            new InspectAssemblyArguments(assemblyPath, null, "UsesDependency", null, true, 100),
            CancellationToken.None);
        var payload = Deserialize<InspectAssemblyPayload>(result);

        Assert.Equal("complete", payload.Completeness);
        Assert.DoesNotContain(payload.Diagnostics, diagnostic => diagnostic.Contains("ConsumerDependency", StringComparison.Ordinal));
        var dependency = Assert.Single(payload.References, reference => reference.Name == "ConsumerDependency");
        Assert.True(dependency.Resolved);
        Assert.Equal(dependency.ResolvedPath, Path.GetFullPath(dependency.ResolvedPath!));
        Assert.Contains(dependency.ResolvedPath!, TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectAssembly_UsesPeAssemblyIdentityInPayloadAndText()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-identity-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "VersionedProbe", """
            using System.Reflection;
            [assembly: AssemblyVersion("7.8.9.10")]
            namespace Probe;
            public sealed class Value { public int Number => 1; }
            """);

        var result = await InspectAssemblyTool.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, "Value", null, true, 100),
            CancellationToken.None);
        var payload = Deserialize<InspectAssemblyPayload>(result);

        Assert.Equal("7.8.9.10", payload.Identity?.Version);
        Assert.Contains("Version 7.8.9.10", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectAssembly_RejectsSameNameDependencyWithWrongVersion()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-reference-identity-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(temp, "VersionedDependency", """
            using System.Reflection;
            [assembly: AssemblyVersion("1.0.0.0")]
            namespace Dependency;
            public sealed class Value { }
            """);
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ReferenceIdentityProbe",
            "namespace Probe; public sealed class UsesDependency { public Dependency.Value Value { get; } = new(); }",
            dependencyPath);
        AssemblyTestHelper.EmitAssembly(temp, "VersionedDependency", """
            using System.Reflection;
            [assembly: AssemblyVersion("2.0.0.0")]
            namespace Dependency;
            public sealed class Value { }
            """);

        var result = await InspectAssemblyTool.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 100),
            CancellationToken.None);
        var payload = Deserialize<InspectAssemblyPayload>(result);
        var dependency = Assert.Single(payload.References, reference => reference.Name == "VersionedDependency");

        Assert.False(dependency.Resolved);
        Assert.Null(dependency.ResolvedPath);
        Assert.Contains(payload.Diagnostics, diagnostic => diagnostic.Contains("Identitätsgleich", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("nicht aufgelöst", TextOf(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InspectAssembly_MissingDependencyMarksPartialResult()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(temp, "MissingDependency", "namespace Missing; public sealed class DependencyType { }");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "PartialProbe", "namespace Probe; public sealed class UsesMissing { public Missing.DependencyType Value { get; } = new(); }", dependencyPath);
        File.Delete(dependencyPath);

        var result = await InspectAssemblyTool.ExecuteAsync(null, new InspectAssemblyArguments(assemblyPath, null, null, null, true, 100), CancellationToken.None);
        var payload = Deserialize<InspectAssemblyPayload>(result);

        Assert.Equal("partial", payload.Completeness);
        Assert.Contains(payload.Diagnostics, diagnostic => diagnostic.Contains("MissingDependency", StringComparison.Ordinal));
        Assert.Contains("partial", TextOf(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReferenceResolver_TraversesLocalMetadataReferencesTransitivelyAndDeduplicates()
    {
        using var temp = TestTempDirectory.Create("assembly-reference-transitive-");
        var leafPath = AssemblyTestHelper.EmitAssembly(temp, "ReferenceLeaf", "namespace Probe; public sealed class Leaf { }");
        var middlePath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ReferenceMiddle",
            "namespace Probe; public sealed class Middle { public Leaf Value { get; } = new(); }",
            leafPath);
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ReferenceRoot",
            "namespace Probe; public sealed class Root { public Middle Value { get; } = new(); }",
            middlePath,
            leafPath);

        var resolution = new AssemblyReferenceResolver().Resolve(rootPath);

        var middle = Assert.Single(resolution.References, reference => reference.Name == "ReferenceMiddle");
        var leaf = Assert.Single(resolution.References, reference => reference.Name == "ReferenceLeaf");
        Assert.True(middle.Resolved);
        Assert.True(leaf.Resolved);
        Assert.Equal(1, middle.Depth);
        Assert.Equal(2, leaf.Depth);
        Assert.Equal(2, resolution.References.Count(reference => reference.ResolvedPath is not null && reference.Name is "ReferenceMiddle" or "ReferenceLeaf"));
        Assert.Contains(resolution.MetadataReferences, reference => reference.Display?.EndsWith("ReferenceLeaf.dll", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void ReferenceResolver_ReportsMissingTransitiveReferenceAsVisiblePartialState()
    {
        using var temp = TestTempDirectory.Create("assembly-reference-missing-transitive-");
        var leafPath = AssemblyTestHelper.EmitAssembly(temp, "MissingLeaf", "namespace Probe; public sealed class Leaf { }");
        var middlePath = AssemblyTestHelper.EmitAssembly(
            temp,
            "MissingMiddle",
            "namespace Probe; public sealed class Middle { public Leaf Value { get; } = new(); }",
            leafPath);
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "MissingRoot",
            "namespace Probe; public sealed class Root { public Middle Value { get; } = new(); }",
            middlePath);
        File.Delete(leafPath);

        var resolution = new AssemblyReferenceResolver().Resolve(rootPath);

        var missing = Assert.Single(resolution.References, reference => reference.Name == "MissingLeaf");
        Assert.False(missing.Resolved);
        Assert.Equal("missing", missing.ResolutionState);
        Assert.Equal(2, missing.Depth);
        Assert.Contains(resolution.Diagnostics, diagnostic => diagnostic.Message.Contains("MissingLeaf", StringComparison.Ordinal));
    }

    [Fact]
    public void ReferenceResolver_ReportsCyclesWithoutRecursingUnboundedly()
    {
        using var temp = TestTempDirectory.Create("assembly-reference-cycle-");
        var firstPath = AssemblyTestHelper.EmitAssembly(temp, "CycleFirst", "namespace Probe; public sealed class First { }");
        var secondPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "CycleSecond",
            "namespace Probe; public sealed class Second { public First Value { get; } = new(); }",
            firstPath);
        AssemblyTestHelper.EmitAssembly(
            temp,
            "CycleFirst",
            "namespace Probe; public sealed class First { public Second Value { get; } = new(); }",
            secondPath);

        var resolution = new AssemblyReferenceResolver().Resolve(firstPath);

        var cycle = Assert.Single(resolution.References, reference => reference.Name == "CycleFirst");
        Assert.Equal("cycle", cycle.ResolutionState);
        Assert.True(cycle.Resolved);
        Assert.Contains(resolution.Diagnostics, diagnostic => diagnostic.Code == "assembly-reference-cycle");
        Assert.True(resolution.References.Count < AssemblyReferenceResolver.MaxReferenceNodes);
    }

    private static T Deserialize<T>(CallToolResult result)
    {
        Assert.True(result.StructuredContent.HasValue, TextOf(result));
        return JsonSerializer.Deserialize<T>(result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
    }

    private static string TextOf(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
