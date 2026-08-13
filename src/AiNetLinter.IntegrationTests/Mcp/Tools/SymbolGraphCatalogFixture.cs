#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

public sealed class SymbolGraphCatalogFixture : IAsyncLifetime
{
    private LoadedFixture? fixture;

    private LoadedFixture Fixture => fixture ?? throw new InvalidOperationException("Fixture wurde noch nicht initialisiert.");

    public string RootPath => Fixture.RootPath;

    public Solution Snapshot => Fixture.Solution;

    public async ValueTask InitializeAsync() => fixture = await LoadedFixture.CreateAsync("SymbolGraphMini");

    internal McpCodeGraphServer CreateReadOnlyServer(bool usedDefaultConfig = false) =>
        new(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            Catalog: null,
            UsedDefaultConfig: usedDefaultConfig,
            ReadOnlySolutionSnapshot: Snapshot)));

    public async ValueTask DisposeAsync()
    {
        if (fixture is not null) await fixture.DisposeAsync();
    }
}
