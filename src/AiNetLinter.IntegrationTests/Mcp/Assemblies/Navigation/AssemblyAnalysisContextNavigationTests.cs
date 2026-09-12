#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies.Navigation;

[Trait("Category", "Integration")]
public sealed class AssemblyAnalysisContextNavigationTests
{
    [Fact]
    public async Task Context_CanonicalReferenceHandoffUsesOnlyVerifiedOwnerWhenReferencesAreExcluded()
    {
        using var temp = TestTempDirectory.Create("assembly-context-owner-only-");
        var ownerPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextOwner",
            "namespace Probe; public sealed class Target { public int Read() => 1; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextRoot",
            "namespace Probe; public sealed class Root { public int Read() => new Target().Read(); }",
            ownerPath);
        await using var registry = new AssemblyAnalysisRegistry();

        var owner = await registry.LeaseAsync(ownerPath);
        Assert.Null(owner.Error);
        string handoffId;
        using (var ownerLease = owner.Lease!)
        {
            handoffId = AnalysisSymbolIdentity.ForAssembly(
                    ownerLease.CanonicalPath,
                    ownerLease.Context.Origin.ContentHash,
                    ownerLease.Context.Generation)
                .FormatHandoff(FindMember(ownerLease, "Probe.Target", "Read"))!;
        }

        var root = await registry.LeaseAsync(rootPath);
        Assert.Null(root.Error);
        using var rootLease = root.Lease!;
        var result = await AssemblyAnalysisContextTool.ExecuteAsync(
            rootLease,
            CreateArguments(handoffId, includeReferences: false),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var resolvedOwnerLease = Assert.Single(rootLease.ReferenceLeasesSnapshot());
        Assert.Equal(Path.GetFullPath(ownerPath), resolvedOwnerLease.CanonicalPath, ignoreCase: true);
        Assert.Empty(resolvedOwnerLease.ReferenceLeasesSnapshot());
    }

    [Fact]
    public async Task Context_PlainSymbolKeepsRootOnlyScopeWhenReferencesAreExcluded()
    {
        using var temp = TestTempDirectory.Create("assembly-context-root-only-");
        var ownerPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextPlainOwner",
            "namespace Probe; public sealed class Target { public int Read() => 1; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextPlainRoot",
            "namespace Probe; public sealed class Root { public int Read() => new Target().Read(); }",
            ownerPath);
        await using var registry = new AssemblyAnalysisRegistry();
        var root = await registry.LeaseAsync(rootPath);
        Assert.Null(root.Error);
        using var rootLease = root.Lease!;

        var result = await AssemblyAnalysisContextTool.ExecuteAsync(
            rootLease,
            CreateArguments("Probe.Root.Read", includeReferences: false),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Empty(rootLease.ReferenceLeasesSnapshot());
    }

    private static AssemblyAnalysisContextArguments CreateArguments(string symbolIdentifier, bool includeReferences) =>
        new(
            symbolIdentifier,
            IncludeMetrics: false,
            IncludeReferences: includeReferences,
            IncludeCallers: false,
            IncludeImpact: false,
            IncludeBody: true,
            IncludeClassStructure: false,
            MaxResults: 10,
            MaxBodyLines: 10,
            MaxCallers: 10,
            Depth: 1,
            TopN: 10,
            MaxResponseBytes: 0,
            DetailLevel: null,
            Cursor: null);

    private static ISymbol FindMember(AssemblyAnalysisLease lease, string typeName, string memberName) =>
        Assert.Single(lease.Context.Compilation.GetTypeByMetadataName(typeName)!.GetMembers(memberName));
}
