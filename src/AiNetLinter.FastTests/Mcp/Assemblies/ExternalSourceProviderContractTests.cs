#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
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
        internal ExternalSourceMapping? Mapping { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        public ValueTask<ExternalSourceProviderResult> ResolveAsync(
            ExternalSourceMapping mapping,
            CancellationToken cancellationToken = default)
        {
            Mapping = mapping;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(new ExternalSourceProviderResult(
                isAvailable: true,
                diagnostics: System.Array.Empty<ExternalSourceConfigurationDiagnostic>()));
        }
    }
}
