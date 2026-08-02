#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using Xunit;

namespace AiNetLinter.Tests.Fixtures;

public sealed class BaselineCatalogFixture : IAsyncLifetime
{
    public BaselineMiniFixtureWorkspace Workspace { get; private set; } = null!;
    public SourceFileCatalog Catalog { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Workspace = new BaselineMiniFixtureWorkspace();
        Catalog = await SourceFileCatalog.LoadAsync(Workspace.RootPath);
    }

    public ValueTask DisposeAsync()
    {
        Workspace?.Dispose();
        return ValueTask.CompletedTask;
    }
}
