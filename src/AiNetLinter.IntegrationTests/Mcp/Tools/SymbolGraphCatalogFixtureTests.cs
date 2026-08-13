#nullable enable

using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

[Trait("Category", "Integration")]
public sealed class SymbolGraphCatalogFixtureTests
{
    private readonly SymbolGraphCatalogFixture fixture;

    public SymbolGraphCatalogFixtureTests(SymbolGraphCatalogFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task ReadOnlyServers_DisposeWithoutAffectingParallelOrLaterFixtureReaders()
    {
        var snapshot = fixture.Snapshot;
        using var disposedServer = fixture.CreateReadOnlyServer();
        using var remainingServer = fixture.CreateReadOnlyServer();

        Assert.Null(typeof(SymbolGraphCatalogFixture).GetProperty("Catalog"));
        Assert.Null(typeof(SymbolGraphCatalogFixture).GetProperty("Workspace"));
        Assert.Same(snapshot, disposedServer.GetCurrentSolution());
        Assert.Same(snapshot, remainingServer.GetCurrentSolution());

        disposedServer.Dispose();

        await Task.WhenAll(
            Task.Run(() => Assert.Same(snapshot, remainingServer.GetCurrentSolution())),
            Task.Run(async () =>
            {
                var compilation = await snapshot.Projects.First().GetCompilationAsync();
                Assert.NotNull(compilation);
            }));

        Assert.Same(snapshot, remainingServer.GetCurrentSolution());
    }
}
