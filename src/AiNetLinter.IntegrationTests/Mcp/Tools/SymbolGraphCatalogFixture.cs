#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.Mcp;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

public sealed class SymbolGraphCatalogFixture : IAsyncLifetime
{
    private LoadedFixture? fixture;

    public LoadedFixture Workspace => fixture ?? throw new InvalidOperationException("Fixture wurde noch nicht initialisiert.");

    public async ValueTask InitializeAsync() => fixture = await LoadedFixture.CreateAsync("SymbolGraphMini");

    internal McpCodeGraphServer CreateReadOnlyServer(bool usedDefaultConfig = false) =>
        new(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            Catalog: null,
            UsedDefaultConfig: usedDefaultConfig,
            ReadOnlySolutionSnapshot: Workspace.Solution)));

    public async ValueTask DisposeAsync()
    {
        if (fixture is not null) await fixture.DisposeAsync();
    }
}
