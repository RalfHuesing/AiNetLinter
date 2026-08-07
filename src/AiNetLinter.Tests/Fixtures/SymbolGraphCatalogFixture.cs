#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using Xunit;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Laedt einmalig pro Collection ein <see cref="SymbolGraphMiniFixtureWorkspace"/>
/// und dessen Roslyn <see cref="SourceFileCatalog"/>.
/// Wird in Tool-Unit-Tests via <c>[Collection("SymbolGraphCatalog")]</c> geteilt verwendet.
/// </summary>
public sealed class SymbolGraphCatalogFixture : IAsyncLifetime
{
    public SymbolGraphMiniFixtureWorkspace Workspace { get; private set; } = null!;
    public SourceFileCatalog Catalog { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Workspace = new SymbolGraphMiniFixtureWorkspace();
        Catalog = await SourceFileCatalog.LoadAsync(Workspace.RootPath);
    }

    public ValueTask DisposeAsync()
    {
        Workspace?.Dispose();
        return ValueTask.CompletedTask;
    }
}
