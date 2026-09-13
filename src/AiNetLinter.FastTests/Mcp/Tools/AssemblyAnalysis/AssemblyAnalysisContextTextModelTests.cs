#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.MetricsLookup;
using AiNetLinter.TestKit;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Unit")]
public sealed class AssemblyAnalysisContextTextModelTests
{
    [Fact]
    public async Task MetricsForAssembly_ReturnsUnsupportedInDirectAndCompositeCalls()
    {
        using var temp = TestTempDirectory.Create("assembly-context-metrics-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextMetricsProbe",
            "namespace Probe.Api; public sealed class PublicApi { }");
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var direct = await MetricsLookupTool.ExecuteAsync(
            lease.Server,
            ["Probe.Api.PublicApi"],
            CancellationToken.None);
        var directText = AssemblyAnalysisTestSupport.TextOf(direct);
        Assert.Contains("ASSEMBLY_TARGET_UNSUPPORTED", directText, StringComparison.Ordinal);
        Assert.DoesNotContain("ainetlinter-rules.json", directText, StringComparison.Ordinal);

        var composite = await AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            new AssemblyAnalysisContextArguments(
                SymbolIdentifier: "Probe.Api.PublicApi",
                IncludeMetrics: true,
                IncludeReferences: false,
                IncludeCallers: false,
                IncludeImpact: false,
                IncludeBody: false,
                IncludeClassStructure: false,
                MaxResults: 10,
                MaxBodyLines: 80,
                MaxCallers: 10,
                Depth: 1,
                TopN: 10,
                MaxResponseBytes: 2_048,
                DetailLevel: null,
                Cursor: null),
            CancellationToken.None);
        var compositeText = AssemblyAnalysisTestSupport.TextOf(composite);
        Assert.Contains("Abschnitt: metrics", compositeText, StringComparison.Ordinal);
        Assert.Contains("ASSEMBLY_TARGET_UNSUPPORTED", compositeText, StringComparison.Ordinal);
        Assert.DoesNotContain("ainetlinter-rules.json", compositeText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteWithoutSymbol_ProvidesAssemblyOverview()
    {
        using var temp = TestTempDirectory.Create("assembly-context-overview-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextOverviewProbe",
            "namespace Probe.Api; public sealed class PublicApi { }");
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var result = await AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            new AssemblyAnalysisContextArguments(
                SymbolIdentifier: null,
                IncludeMetrics: false,
                IncludeReferences: true,
                IncludeCallers: false,
                IncludeImpact: false,
                IncludeBody: false,
                IncludeClassStructure: false,
                MaxResults: 10,
                MaxBodyLines: 80,
                MaxCallers: 10,
                Depth: 1,
                TopN: 10,
                MaxResponseBytes: 2_048,
                DetailLevel: null,
                Cursor: null),
            CancellationToken.None);

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Abschnitt: assembly", text, StringComparison.Ordinal);
        Assert.Contains("Assembly: `ContextOverviewProbe`", text, StringComparison.Ordinal);
        Assert.Contains("Zielframework:", text, StringComparison.Ordinal);
        Assert.Contains("Öffentliche Namespaces: 1", text, StringComparison.Ordinal);
        Assert.Contains("`Probe.Api`", text, StringComparison.Ordinal);
        Assert.Contains("Referenzen:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("API-Typen:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatContextOverview_ProvidesAssemblyDiscoveryWithoutTypeDump()
    {
        var payload = new InspectAssemblyPayload(
            "fixture.dll",
            new AssemblyIdentityDto("Probe.Library", "1.2.3.4", "neutral", "null"),
            ["Probe.Api"],
            [new AssemblyReferenceDto("Probe.Dependency", "2.0.0.0", "neutral", true)],
            [],
            [],
            "complete",
            false,
            1,
            0,
            []);

        var text = InspectAssemblyFormatter.FormatContextOverview(payload, ".NETCoreApp,Version=v10.0");

        Assert.Contains("Assembly: `Probe.Library`", text, StringComparison.Ordinal);
        Assert.Contains("Identität: Probe.Library, Version 1.2.3.4", text, StringComparison.Ordinal);
        Assert.Contains("Zielframework: `.NETCoreApp,Version=v10.0`", text, StringComparison.Ordinal);
        Assert.Contains("Öffentliche Namespaces: 1", text, StringComparison.Ordinal);
        Assert.Contains("`Probe.Api`", text, StringComparison.Ordinal);
        Assert.Contains("Probe.Dependency, Version 2.0.0.0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("API-Typen:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PreservesSectionTextAndContinuationFromTypedModel()
    {
        var model = new AssemblyAnalysisContextTextModel(
            TotalCount: 3,
            ReturnedCount: 1,
            IsTruncated: true,
            ContinuationToken: "next-page",
            Scope: "root",
            Completeness: "partial",
            SymbolIdentifier: "M:Probe.Type.Read",
            Sections: new Dictionary<string, string>
            {
                ["body"] = "handoffId: `h:owner`",
            });

        var text = AssemblyAnalysisContextTool.RenderText(model);

        Assert.Contains("Assembly-Kontext: 1 von 3", text, System.StringComparison.Ordinal);
        Assert.Contains("Abschnitt: body", text, System.StringComparison.Ordinal);
        Assert.Contains("handoffId: `h:owner`", text, System.StringComparison.Ordinal);
        Assert.Contains("continuationToken=next-page", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InspectHandoffFeedsClassStructureWithoutInternalIdLeak()
    {
        using var temp = TestTempDirectory.Create("assembly-context-inspect-handoff-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextInspectHandoffProbe",
            "namespace Probe; public sealed class Target { public void Read() { } }");
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var inspection = await InspectAssemblyTool.ExecuteAsync(
            lease,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 10));
        AssertContainsOnlyOpaqueHandoffs(AssemblyAnalysisTestSupport.TextOf(inspection));
        var result = await AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            CreateArguments(ExtractHandoffId(AssemblyAnalysisTestSupport.TextOf(inspection)), includeClassStructure: true),
            CancellationToken.None);

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("Symbol: h:", text, StringComparison.Ordinal);
        Assert.Contains("Abschnitt: classStructure", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: `a:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SearchHandoffFeedsBodyWithoutInternalIdLeak()
    {
        using var temp = TestTempDirectory.Create("assembly-context-search-handoff-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextSearchHandoffProbe",
            "namespace Probe; public sealed class Target { public void Read() { } }");
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var search = await AssemblySearchTool.ExecuteAsync(
            lease,
            new AssemblySearchArguments("Read", false, "text", 10, DeclarationOnly: true, Kind: "method"),
            CancellationToken.None);
        AssertContainsOnlyOpaqueHandoffs(AssemblyAnalysisTestSupport.TextOf(search));
        var result = await AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            CreateArguments(ExtractHandoffId(AssemblyAnalysisTestSupport.TextOf(search)), includeBody: true),
            CancellationToken.None);

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("Abschnitt: body", text, StringComparison.Ordinal);
        Assert.Contains("Read", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[ERROR]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: `a:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ExtensionHandoffFeedsBodyWithoutInternalIdLeak()
    {
        using var temp = TestTempDirectory.Create("assembly-context-extension-handoff-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextExtensionHandoffProbe",
            "namespace Probe; public static class Extensions { public static string Extend(this object value) => value.ToString(); }");
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var extensions = await FindAssemblyExtensionsTool.ExecuteAsync(
            lease,
            new FindAssemblyExtensionsArguments(assemblyPath, null, null, null, 10));
        AssertContainsOnlyOpaqueHandoffs(AssemblyAnalysisTestSupport.TextOf(extensions));
        var result = await AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            CreateArguments(ExtractHandoffId(AssemblyAnalysisTestSupport.TextOf(extensions)), includeBody: true),
            CancellationToken.None);

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("Abschnitt: body", text, StringComparison.Ordinal);
        Assert.Contains("Extend", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[ERROR]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: `a:", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a:legacy:identifier")]
    [InlineData("s:legacy:identifier")]
    public async Task ExecuteAsync_RejectsLegacyAssemblyHandoffWithParameterError(string legacyHandoff)
    {
        using var temp = TestTempDirectory.Create("assembly-context-legacy-handoff-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextLegacyHandoffProbe",
            "namespace Probe; public sealed class Target { }");
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var result = await AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            CreateArguments(legacyHandoff, includeBody: true),
            CancellationToken.None);

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("UNSUPPORTED_HANDOFF_FORMAT", text, StringComparison.Ordinal);
        Assert.Contains("fieldPath: $.symbolIdentifier", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_PassesDirectSemanticIdentifierThroughToAssemblyConsumer()
    {
        using var temp = TestTempDirectory.Create("assembly-context-semantic-input-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextSemanticInputProbe",
            "namespace Probe; public sealed class Target { public void Read() { } }");
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var result = await AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            CreateArguments("Probe.Target", includeClassStructure: true),
            CancellationToken.None);

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("Symbol: Probe.Target", text, StringComparison.Ordinal);
        Assert.Contains("Abschnitt: classStructure", text, StringComparison.Ordinal);
    }

    private static AssemblyAnalysisContextArguments CreateArguments(
        string? symbolIdentifier,
        bool includeBody = false,
        bool includeClassStructure = false) =>
        new(
            symbolIdentifier,
            IncludeMetrics: false,
            IncludeReferences: false,
            IncludeCallers: false,
            IncludeImpact: false,
            IncludeBody: includeBody,
            IncludeClassStructure: includeClassStructure,
            MaxResults: 10,
            MaxBodyLines: 80,
            MaxCallers: 10,
            Depth: 1,
            TopN: 10,
            MaxResponseBytes: 2_048,
            DetailLevel: null,
            Cursor: null);

    private static string ExtractHandoffId(string text)
    {
        var match = Regex.Match(text, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant);
        return match.Success
            ? match.Groups["id"].Value
            : throw new InvalidOperationException("Producer-Antwort enthält kein opaques Handoff-Handle.");
    }

    private static void AssertContainsOnlyOpaqueHandoffs(string text)
    {
        Assert.NotEmpty(ExtractHandoffId(text));
        Assert.DoesNotContain("handoffId: `a:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: `s:", text, StringComparison.Ordinal);
    }
}
