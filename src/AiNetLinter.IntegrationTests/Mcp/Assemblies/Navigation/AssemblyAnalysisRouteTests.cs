#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies.Navigation;

[Trait("Category", "Integration")]
// @covers AssemblyAnalysisRegistry
// @covers AssemblyAnalysisLease
// @covers AssemblyReferenceSessionExpander
// @covers AssemblyGetCallTreeTool
// @covers AssemblyFindReferencesTool
public sealed class AssemblyAnalysisRouteTests
{
    [Fact]
    public async Task AssemblyRoute_ResolvesRootReferenceAndAllowsLazyTransitiveTarget()
    {
        using var temp = TestTempDirectory.Create("assembly-route-reference-target-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedReferenceTarget",
            "namespace Probe; public sealed class DependencyType { public int Value => 1; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedReferenceRoot",
            "namespace Probe; public sealed class Root { public DependencyType Value { get; } = new(); }",
            dependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();

        var rootResult = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => InspectAssemblyTool.ExecuteAsync(
                        lease,
                        new InspectAssemblyArguments(lease.CanonicalPath, null, null, null, true, 100)),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));
        Assert.NotNull(rootResult.StructuredContent);
        var reference = Assert.Single(
            rootResult.StructuredContent!.Value.GetProperty("references").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "RoutedReferenceTarget");
        var resolvedPath = reference.GetProperty("resolvedPath").GetString();
        Assert.Equal(Path.GetFullPath(dependencyPath), resolvedPath, StringComparer.OrdinalIgnoreCase);

        var dependencySession = Assert.Single(
            rootResult.StructuredContent.Value.GetProperty("referenceSessions").EnumerateArray(),
            item => item.GetProperty("reference").GetProperty("name").GetString() == "RoutedReferenceTarget");
        Assert.Equal("RoutedReferenceTarget", dependencySession.GetProperty("identity").GetProperty("name").GetString());
        Assert.Equal("complete", dependencySession.GetProperty("sessionStatus").GetString());
        Assert.Contains("RoutedReferenceTarget", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(rootResult.Content)).Text, StringComparison.Ordinal);
        Assert.Equal(1, registry.ResidentCount);
    }

    [Fact]
    public async Task AssemblyRoute_IncludeReferencesNavigatesSymbolsReferencesAndCallTree()
    {
        using var temp = TestTempDirectory.Create("assembly-route-symbol-graph-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedSymbolDependency",
            "namespace Probe; public sealed class DependencyType { public int Value => 1; public int Read() => Value; }");
        var secondDependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedSymbolSecondDependency",
            "namespace Probe; public sealed class ExclusiveDependencyType { public int Value => 2; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RoutedSymbolRoot",
            "namespace Probe; public sealed class Root { public int Read() => new DependencyType().Read(); public int ReadOther() => new ExclusiveDependencyType().Value; }",
            dependencyPath,
            secondDependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();
        var route = AssemblyAnalysisDispatcher.CreateRoute(registry);

        var symbolResult = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                    new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(
                        lease,
                        new AssemblyFindSymbolRequest(["ExclusiveDependencyType"], null, 50, true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.NotEqual(true, symbolResult.IsError);
        var symbolPayload = symbolResult.StructuredContent!.Value;
        var symbolMatch = Assert.Single(
            symbolPayload.GetProperty("results")[0].GetProperty("matches").EnumerateArray());
        Assert.Equal("Probe.ExclusiveDependencyType", symbolMatch.GetProperty("name").GetString());
        Assert.Equal("decompiled", symbolMatch.GetProperty("origin").GetProperty("originKind").GetString());
        Assert.True(symbolPayload.GetProperty("navigation").GetProperty("includeReferences").GetBoolean());
        Assert.True(symbolPayload.GetProperty("navigation").GetProperty("totalAssemblyCount").GetInt32() >= 3);
        Assert.Equal("partial", symbolPayload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
        Assert.NotEmpty(symbolPayload.GetProperty("navigation").GetProperty("diagnostics").EnumerateArray());

        var impactResult = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => GetImpactTool.ExecuteAsync(
                        lease,
                        new GetImpactInput(null, "Probe.Root.Read", 50, 1),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.NotEqual(true, impactResult.IsError);
        var impactPayload = impactResult.StructuredContent!.Value;
        Assert.Equal("decompiled", impactPayload.GetProperty("analysis").GetProperty("origin").GetString());
        Assert.DoesNotContain("ASSEMBLY_TARGET_UNSUPPORTED", Text(impactResult), StringComparison.Ordinal);

        var referenceResult = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindReferencesTool.ExecuteAsync(
                        lease,
                        new AssemblyFindReferencesRequest("Probe.DependencyType.Read", 50, 1, true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.True(
            referenceResult.IsError != true,
            string.Join("\n", referenceResult.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(block => block.Text)));
        var referencePayload = referenceResult.StructuredContent!.Value;
        Assert.True(referencePayload.GetProperty("navigation").GetProperty("includeReferences").GetBoolean());
        Assert.Equal("partial", referencePayload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
        Assert.True(referencePayload.TryGetProperty("callSites", out _));

        var treeResult = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyGetCallTreeTool.ExecuteAsync(
                        lease,
                        new AssemblyGetCallTreeRequest(
                            new GetCallTreeInput("Probe.DependencyType.Read", 2, null, 10, null),
                            true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.NotEqual(true, treeResult.IsError);
        Assert.Contains("assembly=", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(treeResult.Content)).Text, StringComparison.Ordinal);
        var treePayload = treeResult.StructuredContent!.Value;
        Assert.True(treePayload.GetProperty("navigation").GetProperty("includeReferences").GetBoolean());
    }

    [Fact]
    public async Task AssemblyRoute_FindSymbolHandoffIdStaysBoundToItsReferencedAssembly()
    {
        using var temp = TestTempDirectory.Create("assembly-route-symbol-handoff-");
        var alphaPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "SemanticDependencyAlpha",
            "namespace Probe; public sealed class SharedTarget { public int Read() => 1; } " +
            "public sealed class AlphaCaller { public int Invoke() => new SharedTarget().Read(); } " +
            "public sealed class AlphaMarker { }");
        var betaPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "SemanticDependencyBeta",
            "namespace Probe; public sealed class SharedTarget { public int Read() => 2; } " +
            "public sealed class BetaCaller { public int Invoke() => new SharedTarget().Read(); } " +
            "public sealed class BetaMarker { }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "SemanticNavigationRoot",
            "namespace Probe; public sealed class Root { public AlphaMarker Alpha { get; } = new(); public BetaMarker Beta { get; } = new(); }",
            betaPath,
            alphaPath);
        await using var registry = new AssemblyAnalysisRegistry();
        var route = AssemblyAnalysisDispatcher.CreateRoute(registry);

        var symbolResult = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(
                        lease,
                        new AssemblyFindSymbolRequest(["SharedTarget"], null, 50, true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.NotEqual(true, symbolResult.IsError);
        var matches = symbolResult.StructuredContent!.Value.GetProperty("results")[0]
            .GetProperty("matches").EnumerateArray();
        Assert.True(matches.Any(), Text(symbolResult));
        var alphaMatch = Assert.Single(
            matches,
            item => string.Equals(
                item.GetProperty("origin").GetProperty("canonicalPath").GetString(),
                Path.GetFullPath(alphaPath),
                StringComparison.OrdinalIgnoreCase));
        var symbolIdentifier = alphaMatch.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(symbolIdentifier));

        var findReferencesResult = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindReferencesTool.ExecuteAsync(
                        lease,
                        new AssemblyFindReferencesRequest(symbolIdentifier, 50, 1, true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.True(
            findReferencesResult.IsError != true,
            string.Join(
                "\n",
                findReferencesResult.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>()
                    .Select(block => block.Text)));
        var referencePaths = findReferencesResult.StructuredContent!.Value
            .GetProperty("callSites").EnumerateArray()
            .Select(item => item.GetProperty("filePath").GetString()!)
            .ToArray();
        Assert.Contains(referencePaths, path => path.EndsWith("AlphaCaller.cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencePaths, path => path.EndsWith("BetaCaller.cs", StringComparison.OrdinalIgnoreCase));

        var defaultFollowUpResult = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindReferencesTool.ExecuteAsync(
                        lease,
                        new AssemblyFindReferencesRequest(symbolIdentifier, 50, 1, false),
                        CancellationToken.None)),
                CancellationToken.None));

        Assert.True(
            defaultFollowUpResult.IsError != true,
            string.Join(
                "\n",
                defaultFollowUpResult.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>()
                    .Select(block => block.Text)));
        var defaultFollowUpPaths = defaultFollowUpResult.StructuredContent!.Value
            .GetProperty("callSites").EnumerateArray()
            .Select(item => item.GetProperty("filePath").GetString()!)
            .ToArray();
        Assert.Contains(defaultFollowUpPaths, path => path.EndsWith("AlphaCaller.cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(defaultFollowUpPaths, path => path.EndsWith("BetaCaller.cs", StringComparison.OrdinalIgnoreCase));

        var getImpactResult = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => GetImpactTool.ExecuteAsync(
                        lease,
                        new GetImpactInput(null, symbolIdentifier, 50, 1),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.True(
            getImpactResult.IsError != true,
            string.Join(
                "\n",
                getImpactResult.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>()
                    .Select(block => block.Text)));
        var impactPaths = getImpactResult.StructuredContent!.Value
            .GetProperty("callSites").EnumerateArray()
            .Select(item => item.GetProperty("filePath").GetString()!)
            .ToArray();
        Assert.Contains(impactPaths, path => path.EndsWith("AlphaCaller.cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(impactPaths, path => path.EndsWith("BetaCaller.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AssemblyRoute_GetImpactWithoutSymbolReturnsRecoverableInvalidArgument()
    {
        using var temp = TestTempDirectory.Create("assembly-route-impact-invalid-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ImpactInvalidProbe",
            "namespace Probe; public sealed class Root { public int Read() => 1; }");
        await using var registry = new AssemblyAnalysisRegistry();

        var result = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(assemblyPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => GetImpactTool.ExecuteAsync(
                        lease,
                        new GetImpactInput(null, null, 50, 1),
                        CancellationToken.None)),
                CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Contains("symbolIdentifier", Text(result), StringComparison.Ordinal);
        Assert.DoesNotContain("ASSEMBLY_TARGET_UNSUPPORTED", Text(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyRoute_GetCallTreeWithReferencesProjectsNavigationDiagnosticsIntoTextAndStructuredContent()
    {
        using var temp = TestTempDirectory.Create("assembly-route-call-tree-diagnostics-");
        var dependencyPaths = Enumerable.Range(1, 6)
            .Select(index => AssemblyTestHelper.EmitAssembly(
                temp,
                $"CallTreeMissingDependency{index}",
                $"namespace Probe; public sealed class DependencyType{index} {{ public int Value => {index}; }}"))
            .ToArray();
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "CallTreeNavigationRoot",
            "namespace Probe; public sealed class Root { public int Read() => " +
            string.Join(" + ", Enumerable.Range(1, 6).Select(index => $"new DependencyType{index}().Value")) + "; }",
            dependencyPaths);
        foreach (var dependencyPath in dependencyPaths)
        {
            File.Delete(dependencyPath);
        }

        await using var registry = new AssemblyAnalysisRegistry();
        var result = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyGetCallTreeTool.ExecuteAsync(
                        lease,
                        new AssemblyGetCallTreeRequest(
                            new GetCallTreeInput("Probe.Root.Read", 1, null, 10, null),
                            true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.True(
            result.IsError != true,
            string.Join("\n", result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(block => block.Text)));
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content)).Text;
        var payload = result.StructuredContent!.Value;
        var navigation = payload.GetProperty("navigation");

        Assert.Equal("partial", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.False(payload.GetProperty("truncated").GetBoolean());
        AssemblyNavigationResponseAssertions.AssertDiagnosticProjection(
            navigation,
            null,
            text,
            "CallTreeMissingDependency",
            "Abhängigkeit nicht auflösbar: CallTreeMissingDependency6");
    }

    [Fact]
    public async Task AssemblyRoute_FindSymbolBatchPreservesEarlierPatternTruncation()
    {
        using var temp = TestTempDirectory.Create("assembly-route-symbol-batch-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "BatchProbe",
            "namespace Probe; " +
            "public sealed class MatchOne { } " +
            "public sealed class MatchTwo { } " +
            "public sealed class MatchThree { } " +
            "public sealed class Unique { }");
        await using var registry = new AssemblyAnalysisRegistry();

        var result = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(assemblyPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(
                        lease,
                        new AssemblyFindSymbolRequest(["Match", "Unique"], null, 1, true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        var navigation = payload.GetProperty("navigation");
        Assert.Equal("truncated", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.False(navigation.GetProperty("assembliesTruncated").GetBoolean());
        Assert.True(navigation.GetProperty("resultsTruncated").GetBoolean());
        Assert.False(payload.GetProperty("wireTruncated").GetBoolean());
        Assert.False(payload.GetProperty("wireBudget").GetProperty("truncated").GetBoolean());
        Assert.Equal(["maxResults"], payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.True(
            navigation.GetProperty("diagnostics").EnumerateArray()
                .Select(item => item.GetString()!)
                .Any(diagnostic => diagnostic.Contains("Treffer", StringComparison.Ordinal)),
            navigation.GetRawText());
    }

    [Fact]
    public async Task AssemblyRoute_WireBudgetMarksOnlyActualWireTruncation()
    {
        using var temp = TestTempDirectory.Create("assembly-route-symbol-wire-budget-");
        var declarations = Enumerable.Range(0, 120)
            .Select(index => $"public sealed class Type{index:D3} {{ public int Value{index:D3} => {index}; }}");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "WireBudgetProbe",
            $"namespace Probe.Budget; {string.Join(Environment.NewLine, declarations)}");
        await using var registry = new AssemblyAnalysisRegistry();

        var result = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(assemblyPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(
                        lease,
                        new AssemblyFindSymbolRequest(["Type"], null, 500, false),
                        CancellationToken.None),
                    MaxResponseBytes: 4096),
                CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        var wireBudget = payload.GetProperty("wireBudget");
        Assert.True(wireBudget.GetProperty("truncated").GetBoolean(), payload.GetRawText());
        Assert.True(payload.GetProperty("wireTruncated").GetBoolean(), payload.GetRawText());
        Assert.Equal("truncated", payload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
        Assert.Contains(
            "responseBudget",
            payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.True(
            wireBudget.GetProperty("totalBytes").GetInt32() <= wireBudget.GetProperty("limitBytes").GetInt32(),
            payload.GetRawText());
    }

    private static string Text(ModelContextProtocol.Protocol.CallToolResult result) =>
        Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content)).Text;
}
