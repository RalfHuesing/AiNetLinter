#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
public sealed class ExternalSourceProviderContractTests
{
    [Fact]
    public async Task ResolveAsync_FakeProviderErhaeltNurMappingUndCancellation()
    {
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Shared.slnx",
            ["Foo"]);
        using var cancellation = new CancellationTokenSource();
        var provider = new RecordingProvider();

        var result = await provider.ResolveAsync(mapping, cancellation.Token);

        Assert.Same(mapping, provider.Mapping);
        Assert.Equal(cancellation.Token, provider.CancellationToken);
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.SourceSnapshot);
    }

    [Fact]
    public async Task ResolveAsync_FakeProviderTransportiertBereitsGeladenenSnapshot()
    {
        var mapping = new ExternalSourceMapping(
            "HTTPS://GITEA.EXAMPLE/shared.git",
            @".\src\..\src\Shared.slnx",
            ["Foo"]);
        var identity = SourceSnapshotIdentity.Create(mapping, "revision-1");
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        using var snapshot = new ExternalSourceSnapshot(identity, solution, workspace);
        var expectedDiagnostic = new ExternalSourceConfigurationDiagnostic(
            "source-warning",
            "Testdiagnose",
            "warning",
            "test");
        var provider = new RecordingProvider(new ExternalSourceProviderResult(
            isAvailable: true,
            diagnostics: [expectedDiagnostic],
            sourceSnapshot: snapshot));

        var result = await provider.ResolveAsync(mapping);

        Assert.True(result.IsAvailable);
        Assert.Same(snapshot, result.SourceSnapshot);
        Assert.Equal(identity, result.SourceSnapshot!.Identity);
        Assert.Same(solution, result.SourceSnapshot.Solution);
        Assert.Equal([expectedDiagnostic], result.Diagnostics);
    }

    [Fact]
    public void ProviderResult_RejectsSnapshotForUnavailableProvider()
    {
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Shared.slnx",
            ["Foo"]);
        var workspace = new AdhocWorkspace();
        using var snapshot = new ExternalSourceSnapshot(
            SourceSnapshotIdentity.Create(mapping, "revision-1"),
            workspace.CurrentSolution,
            workspace);

        Assert.Throws<ArgumentException>(() => new ExternalSourceProviderResult(
            isAvailable: false,
            diagnostics: System.Array.Empty<ExternalSourceConfigurationDiagnostic>(),
            sourceSnapshot: snapshot));
    }

    [Fact]
    public async Task ResolveAsync_UnavailableProvider_LiefertSichtbarenZustandOhneSourceSemantik()
    {
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Shared.slnx",
            ["Foo"]);

        var result = await new UnavailableExternalSourceProvider().ResolveAsync(mapping);

        Assert.False(result.IsAvailable);
        Assert.Null(result.SourceSnapshot);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ExternalSourceConfigurationDiagnosticCodes.ProviderUnavailable, diagnostic.Code);
        Assert.Equal("warning", diagnostic.Severity);
        Assert.Contains(mapping.Url, diagnostic.Location);
    }

    [Fact]
    public async Task ResolveAsync_UnavailableProvider_RespektiertCancellation()
    {
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Shared.slnx",
            ["Foo"]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new UnavailableExternalSourceProvider().ResolveAsync(mapping, cancellation.Token));
    }

    private sealed class RecordingProvider : IExternalSourceProvider
    {
        private readonly ExternalSourceProviderResult result;

        internal RecordingProvider(ExternalSourceProviderResult? result = null)
        {
            this.result = result ?? new ExternalSourceProviderResult(
                isAvailable: true,
                diagnostics: System.Array.Empty<ExternalSourceConfigurationDiagnostic>());
        }

        internal ExternalSourceMapping? Mapping { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        public ValueTask<ExternalSourceProviderResult> ResolveAsync(
            ExternalSourceMapping mapping,
            CancellationToken cancellationToken = default)
        {
            Mapping = mapping;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(result);
        }
    }
}
