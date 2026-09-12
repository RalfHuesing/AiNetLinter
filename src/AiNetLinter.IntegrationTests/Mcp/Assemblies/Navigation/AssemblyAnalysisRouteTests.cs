#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools;
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
public sealed partial class AssemblyAnalysisRouteTests
{
    [Fact]
    public async Task AssemblyRoute_CallTreeSkipsGeneralWireTrimAndRefreshesFinalBudget()
    {
        using var temp = TestTempDirectory.Create("assembly-route-call-tree-wire-budget-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "CallTreeBudgetDependency",
            "namespace Probe; public sealed class DependencyType { public int Read() => 1; }");
        var callers = Enumerable.Range(0, 48)
            .Select(index =>
                $"public sealed class Caller{index:D2} {{ public int Call() => new DependencyType().Read(); }}")
            .ToArray();
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "CallTreeBudgetRoot",
            $"namespace Probe; {string.Join(Environment.NewLine, callers)}",
            dependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();

        var result = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyGetCallTreeTool.ExecuteAsync(
                        lease,
                        new AssemblyGetCallTreeRequest(
                            new GetCallTreeInput(
                                "Probe.DependencyType.Read",
                                9,
                                "ascii",
                                50,
                                "incoming",
                                MaxResponseBytes: 8_192),
                            true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true,
                    MaxResponseBytes: 8_192,
                    PostNavigationResponseBudget: CallGraphResponseBudget.ApplyFinalResponseBudget,
                    ApplyAssemblyWireBudget: false),
                CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        var text = Text(result);
        Assert.Contains("[ASSEMBLY]", text, StringComparison.Ordinal);
        Assert.Contains("[depth auf 5 begrenzt", text, StringComparison.Ordinal);

        var payload = result.StructuredContent!.Value;
        var wireBudget = payload.GetProperty("wireBudget");
        var textBytes = System.Text.Encoding.UTF8.GetByteCount(text);
        var structuredBytes = System.Text.Encoding.UTF8.GetByteCount(payload.GetRawText());
        Assert.Equal(textBytes, wireBudget.GetProperty("textBytes").GetInt32());
        Assert.Equal(structuredBytes, wireBudget.GetProperty("structuredBytes").GetInt32());
        Assert.Equal(textBytes + structuredBytes, wireBudget.GetProperty("totalBytes").GetInt32());
        Assert.False(wireBudget.GetProperty("truncated").GetBoolean(), payload.GetRawText());
        Assert.True(wireBudget.GetProperty("totalBytes").GetInt32() <= 8_192, payload.GetRawText());
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
    public async Task AssemblyRoute_ReferenceHandoffWithoutExpansionSearchesOnlyItsOwner()
    {
        using var temp = TestTempDirectory.Create("assembly-route-owner-only-");
        var alphaPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "OwnerOnlyAlpha",
            "namespace Probe; public sealed class SharedTarget { public int Read() => 1; } public sealed class AlphaCaller { public int Invoke() => new SharedTarget().Read(); }");
        var betaPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "OwnerOnlyBeta",
            "namespace Probe; public sealed class SharedTarget { public int Read() => 2; } public sealed class BetaCaller { public int Invoke() => new SharedTarget().Read(); }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "OwnerOnlyRoot",
            "namespace Probe; public sealed class Root { public object Value => new AlphaCaller(); }",
            alphaPath,
            betaPath);
        await using var registry = new AssemblyAnalysisRegistry();
        var route = AssemblyAnalysisDispatcher.CreateRoute(registry);

        var symbols = await AnalysisToolCall.ExecuteRouted(
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
        var alphaId = Assert.Single(symbols.StructuredContent!.Value.GetProperty("results")[0]
            .GetProperty("matches").EnumerateArray(), match => string.Equals(
                match.GetProperty("origin").GetProperty("canonicalPath").GetString(),
                Path.GetFullPath(alphaPath),
                StringComparison.OrdinalIgnoreCase)).GetProperty("id").GetString();

        var result = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindReferencesTool.ExecuteAsync(
                        lease,
                        new AssemblyFindReferencesRequest(alphaId, 50, 1, false),
                        CancellationToken.None)),
                CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        var navigation = payload.GetProperty("navigation");
        Assert.False(navigation.GetProperty("requestedIncludeReferences").GetBoolean());
        Assert.Equal("symbol_owner_only", navigation.GetProperty("effectiveSearchMode").GetString());
        Assert.Equal(1, navigation.GetProperty("searchedAssemblyCount").GetInt32());
        Assert.NotEmpty(payload.GetProperty("callSites").EnumerateArray());
        Assert.All(payload.GetProperty("callSites").EnumerateArray(), callSite =>
            Assert.EndsWith("AlphaCaller.cs", callSite.GetProperty("filePath").GetString(), StringComparison.OrdinalIgnoreCase));

        var rootOnly = await AnalysisToolCall.ExecuteRouted(
            route,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindReferencesTool.ExecuteAsync(
                        lease,
                        new AssemblyFindReferencesRequest("Probe.Root.Value", 50, 1, false),
                        CancellationToken.None)),
                CancellationToken.None));
        var rootNavigation = rootOnly.StructuredContent!.Value.GetProperty("navigation");
        Assert.False(rootNavigation.GetProperty("requestedIncludeReferences").GetBoolean());
        Assert.Equal("root_only", rootNavigation.GetProperty("effectiveSearchMode").GetString());
        Assert.Equal(1, rootNavigation.GetProperty("searchedAssemblyCount").GetInt32());
    }

    [Fact]
    public async Task AssemblyRoute_ReferenceHandoffOpensOnlyItsOwnerForOwnerOnlyAndClosureScopes()
    {
        using var temp = TestTempDirectory.Create("assembly-route-owner-session-scope-");
        var alphaChildPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "OwnerSessionAlphaChild",
            "namespace Probe; public sealed class AlphaChild { public int Value => 1; }");
        var alphaPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "OwnerSessionAlpha",
            "namespace Probe; public sealed class SharedTarget { public int Read() => new AlphaChild().Value; }",
            alphaChildPath);
        var betaPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "OwnerSessionBeta",
            "namespace Probe; public sealed class BetaOnly { public int Read() => 2; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "OwnerSessionRoot",
            "namespace Probe; public sealed class Root { public object Value => new SharedTarget(); public object Other => new BetaOnly(); }",
            alphaPath,
            betaPath);
        await using var registry = new AssemblyAnalysisRegistry();

        var alphaLeaseResult = await registry.LeaseAsync(alphaPath);
        Assert.Null(alphaLeaseResult.Error);
        string alphaId;
        using (var alphaLease = alphaLeaseResult.Lease!)
        {
            var alphaSymbol = alphaLease.Context.Compilation
                .GetTypeByMetadataName("Probe.SharedTarget")!
                .GetMembers("Read")
                .Single();
            alphaId = AnalysisSymbolIdentity.ForAssembly(
                    alphaLease.CanonicalPath,
                    alphaLease.Context.Origin.ContentHash,
                    alphaLease.Context.Generation)
                .FormatHandoff(alphaSymbol)!;
        }

        var rootLeaseResult = await registry.LeaseAsync(rootPath);
        Assert.Null(rootLeaseResult.Error);
        using var rootLease = rootLeaseResult.Lease!;

        var ownerOnly = await AssemblyFindReferencesTool.ExecuteAsync(
            rootLease,
            new AssemblyFindReferencesRequest(alphaId, 50, 1, false),
            CancellationToken.None);

        Assert.False(ownerOnly.IsError == true, Text(ownerOnly));
        Assert.All(rootLease.ReferenceLeasesSnapshot(), opened =>
            Assert.Equal(Path.GetFullPath(alphaPath), opened.CanonicalPath, ignoreCase: true));

        var closure = await AssemblyFindReferencesTool.ExecuteAsync(
            rootLease,
            new AssemblyFindReferencesRequest(alphaId, 50, 1, true),
            CancellationToken.None);

        Assert.False(closure.IsError == true, Text(closure));
        Assert.All(rootLease.ReferenceLeasesSnapshot(), opened =>
            Assert.Equal(Path.GetFullPath(alphaPath), opened.CanonicalPath, ignoreCase: true));
        Assert.Contains(rootLease.ReferenceLeasesSnapshot(), ownerLease =>
            ownerLease.ReferenceLeasesSnapshot().Any(opened =>
                string.Equals(opened.CanonicalPath, Path.GetFullPath(alphaChildPath), StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task AssemblyRoute_GetSymbolBodyResolvesBatchHandoffsFromTheirVerifiedOwners()
    {
        using var temp = TestTempDirectory.Create("assembly-route-body-batch-owners-");
        var alphaPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "BodyBatchAlpha",
            "namespace Probe; public sealed class Alpha { public int Read() => 1; }");
        var betaPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "BodyBatchBeta",
            "namespace Probe; public sealed class Beta { public int Read() => 2; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "BodyBatchRoot",
            "namespace Probe; public sealed class Root { public int Alpha() => new Alpha().Read(); public int Beta() => new Beta().Read(); }",
            alphaPath,
            betaPath);
        await using var registry = new AssemblyAnalysisRegistry();

        var alphaId = await CreateMemberHandoffAsync(registry, alphaPath, "Probe.Alpha", "Read");
        var betaId = await CreateMemberHandoffAsync(registry, betaPath, "Probe.Beta", "Read");
        var rootLeaseResult = await registry.LeaseAsync(rootPath);
        Assert.Null(rootLeaseResult.Error);
        using var rootLease = rootLeaseResult.Lease!;

        var result = await GetSymbolBodyTool.ExecuteAsync(
            rootLease,
            new GetSymbolBodyRequest([alphaId, betaId]),
            CancellationToken.None);

        Assert.False(result.IsError == true, Text(result));
        var entries = result.StructuredContent!.Value.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(2, entries.Length);
        Assert.All(entries, entry => Assert.StartsWith("a:", entry.GetProperty("id").GetString(), StringComparison.Ordinal));
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

        Assert.True(result.IsError == true);
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

    private static string Text(ModelContextProtocol.Protocol.CallToolResult result) =>
        Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content)).Text;

    private static void AssertCompositeSectionErrors(JsonElement payload)
    {
        Assert.Equal("NOT_CONFIGURED", payload.GetProperty("metrics").GetProperty("code").GetString());
        Assert.Equal("SYMBOL_NOT_FOUND", payload.GetProperty("body").GetProperty("code").GetString());
        Assert.Equal("SYMBOL_NOT_FOUND", payload.GetProperty("classStructure").GetProperty("code").GetString());
    }

    private static async Task<string> CreateMemberHandoffAsync(
        AssemblyAnalysisRegistry registry,
        string assemblyPath,
        string typeName,
        string memberName)
    {
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        Assert.Null(leaseResult.Error);
        using var lease = leaseResult.Lease!;
        var member = lease.Context.Compilation.GetTypeByMetadataName(typeName)!.GetMembers(memberName).Single();
        return AnalysisSymbolIdentity.ForAssembly(
                lease.CanonicalPath,
                lease.Context.Origin.ContentHash,
                lease.Context.Generation)
            .FormatHandoff(member)!;
    }
}
