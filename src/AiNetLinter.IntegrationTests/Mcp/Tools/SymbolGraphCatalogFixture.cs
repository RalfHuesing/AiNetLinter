#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.IntegrationTests.Platform;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

public sealed class SymbolGraphCatalogFixture : IAsyncLifetime
{
    private LoadedFixture? fixture;

    public LoadedFixture Workspace => fixture ?? throw new InvalidOperationException("Fixture wurde noch nicht initialisiert.");
    public SourceFileCatalog Catalog => Workspace.Catalog;

    public async ValueTask InitializeAsync() => fixture = await LoadedFixture.CreateAsync("SymbolGraphMini");

    public async ValueTask DisposeAsync()
    {
        if (fixture is not null) await fixture.DisposeAsync();
    }
}
