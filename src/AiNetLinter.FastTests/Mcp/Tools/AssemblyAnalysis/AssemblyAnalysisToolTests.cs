#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
public sealed partial class AssemblyAnalysisToolTests
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

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            state: null,
            new InspectAssemblyArguments(assemblyPath, "Probe.Api", "PublicApi", null, true, 100),
            CancellationToken.None);

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Vollständigkeit: `complete`", text, StringComparison.Ordinal);
        Assert.Contains("Quelle: Dekompilat", text, StringComparison.Ordinal);
        Assert.Contains("`Probe.Api.PublicApi`; handoffId: `a:", text, StringComparison.Ordinal);
        Assert.Contains("property: `Probe.Api.PublicApi.Name`", text, StringComparison.Ordinal);
        Assert.Contains("event: `Probe.Api.PublicApi.Changed`", text, StringComparison.Ordinal);
        Assert.Contains("Convert(string value)", text, StringComparison.Ordinal);
        Assert.Contains("Convert(int value)", text, StringComparison.Ordinal);
        Assert.Contains("Echo<T>(T value)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("get_Name", text, StringComparison.Ordinal);
        Assert.DoesNotContain("set_Name", text, StringComparison.Ordinal);
        Assert.DoesNotContain("add_Changed", text, StringComparison.Ordinal);
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

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 1),
            CancellationToken.None);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Öffentliche API-Typen: 1 von 2 (gekürzt: maxResults)", text, StringComparison.Ordinal);
        Assert.StartsWith("v1.1.", AssemblyAnalysisTestSupport.ContinuationTokenOf(result), StringComparison.Ordinal);
        Assert.Contains("Vollständigkeit: `complete`", text, StringComparison.Ordinal);
        Assert.DoesNotContain("unrelated.dll", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InspectAssembly_CursorReturnsTheNextStablePage()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-paging-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "PagingProbe", """
            namespace Probe;
            public sealed class Alpha { }
            public sealed class Beta { }
            public sealed class Gamma { }
            """);

        var first = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 1),
            CancellationToken.None);
        var firstText = AssemblyAnalysisTestSupport.TextOf(first);
        var firstToken = AssemblyAnalysisTestSupport.ContinuationTokenOf(first);
        var second = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 1, Cursor: firstToken),
            CancellationToken.None);
        var secondText = AssemblyAnalysisTestSupport.TextOf(second);

        Assert.Contains("`Probe.Alpha`", firstText, StringComparison.Ordinal);
        Assert.DoesNotContain("`Probe.Alpha`", secondText, StringComparison.Ordinal);
        Assert.Contains("Öffentliche API-Typen: 1 von 3", secondText, StringComparison.Ordinal);
        Assert.StartsWith("v1.2.", AssemblyAnalysisTestSupport.ContinuationTokenOf(second), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectAssembly_RejectsRelativeAndMissingPathsWithoutRuntimeLoading()
    {
        var relative = await InspectAssemblyToolDispatch.ExecuteAsync(null, new InspectAssemblyArguments("relative.dll", null, null, null, true, 100), CancellationToken.None);
        Assert.Contains("INVALID_ARGUMENT", AssemblyAnalysisTestSupport.TextOf(relative), StringComparison.Ordinal);

        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var missing = await InspectAssemblyToolDispatch.ExecuteAsync(null, new InspectAssemblyArguments(Path.Combine(temp.DirectoryPath, "missing.dll"), null, null, null, true, 100), CancellationToken.None);
        Assert.Contains("nicht gefunden", AssemblyAnalysisTestSupport.TextOf(missing), StringComparison.OrdinalIgnoreCase);
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

        var result = await FindAssemblyExtensionsToolDispatch.ExecuteAsync(null, new FindAssemblyExtensionsArguments(assemblyPath, null, "Mark", "Probe.Extensions", 100), CancellationToken.None);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Assembly-Extensions: 1 von 1", text, StringComparison.Ordinal);
        Assert.Contains("Vollständigkeit: `complete`", text, StringComparison.Ordinal);
        Assert.Contains("`Probe.Extensions.Mark` für `object` — not_decidable; handoffId: `a:", text, StringComparison.Ordinal);
        Assert.Contains("Signatur: `string Probe.Extensions.Extensions.Mark(object value, int count)`", text, StringComparison.Ordinal);
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

        var result = await FindAssemblyExtensionsToolDispatch.ExecuteAsync(
            null,
            new FindAssemblyExtensionsArguments(assemblyPath, "Consumer.Person", null, null, 100),
            CancellationToken.None);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Assembly-Extensions: 0 von 0", text, StringComparison.Ordinal);
        Assert.Contains("Receiver: `Consumer.Person`", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Consumer:", text, StringComparison.Ordinal);
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
            var result = await FindAssemblyExtensionsToolDispatch.ExecuteAsync(
                null,
                new FindAssemblyExtensionsArguments(assemblyPath, receiverType, null, null, 100),
                CancellationToken.None);
            var text = AssemblyAnalysisTestSupport.TextOf(result);

            Assert.Contains($"Receiver: `{receiverType}`", text, StringComparison.Ordinal);
            foreach (var expectedName in expectedNames)
            {
                Assert.Contains($".{expectedName}` für", text, StringComparison.Ordinal);
            }

            if (expectedNames.Length == 0)
            {
                Assert.Contains("Assembly-Extensions: 0 von 0", text, StringComparison.Ordinal);
            }
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

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            server,
            new InspectAssemblyArguments(
                assemblyPath,
                null,
                "UsesDependency",
                null,
                true,
                100,
                IncludeReferences: true),
            CancellationToken.None);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Vollständigkeit: `complete`", text, StringComparison.Ordinal);
        Assert.Contains("ConsumerDependency", text, StringComparison.Ordinal);
        Assert.Contains($"Pfad `{Path.GetFullPath(dependencyPath)}`", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectAssembly_TargetedInspectionRequiresExplicitReferenceDetails()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-targeted-references-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetedDependency",
            "namespace Dependency; public sealed class Value { }");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetedReferenceProbe",
            "namespace Probe; public sealed class UsesDependency { public Dependency.Value Value { get; } = new(); }",
            dependencyPath);

        var defaultResult = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, "UsesDependency", null, true, 100),
            CancellationToken.None);
        var explicitFalseResult = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(
                assemblyPath,
                null,
                "UsesDependency",
                null,
                true,
                100,
                IncludeReferences: false),
            CancellationToken.None);
        var explicitTrueResult = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(
                assemblyPath,
                null,
                "UsesDependency",
                null,
                true,
                100,
                IncludeReferences: true),
            CancellationToken.None);
        var defaultText = AssemblyAnalysisTestSupport.TextOf(defaultResult);
        var explicitFalseText = AssemblyAnalysisTestSupport.TextOf(explicitFalseResult);
        var explicitTrueText = AssemblyAnalysisTestSupport.TextOf(explicitTrueResult);

        AssertReferenceDetailsExcluded(defaultText);
        AssertReferenceDetailsExcluded(explicitFalseText);
        Assert.Contains("Referenzen: 2 von 2", explicitTrueText, StringComparison.Ordinal);
        Assert.Contains("TargetedDependency", explicitTrueText, StringComparison.Ordinal);
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

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, "Value", null, true, 100),
            CancellationToken.None);
        Assert.Contains("Version 7.8.9.10", AssemblyAnalysisTestSupport.TextOf(result), StringComparison.Ordinal);
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

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 100),
            CancellationToken.None);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("VersionedDependency, Version 1.0.0.0", text, StringComparison.Ordinal);
        Assert.Contains("nicht aufgelöst", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("identitätsgleicher", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InspectAssembly_MissingDependencyMarksPartialResult()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(temp, "MissingDependency", "namespace Missing; public sealed class DependencyType { }");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "PartialProbe", "namespace Probe; public sealed class UsesMissing { public Missing.DependencyType Value { get; } = new(); }", dependencyPath);
        File.Delete(dependencyPath);

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(null, new InspectAssemblyArguments(assemblyPath, null, null, null, true, 100), CancellationToken.None);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Vollständigkeit: `partial`", text, StringComparison.Ordinal);
        Assert.Contains("MissingDependency", text, StringComparison.Ordinal);
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

    private static void AssertReferenceDetailsExcluded(string text)
    {
        Assert.Contains("Referenzen: 0 von", text, StringComparison.Ordinal);
        Assert.Contains("Referenzdetails nicht angefordert; includeReferences=true", text, StringComparison.Ordinal);
        Assert.Contains("Referenz-Sessions: 0 von 0", text, StringComparison.Ordinal);
    }
}
