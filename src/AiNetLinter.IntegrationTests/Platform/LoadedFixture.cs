#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.IntegrationTests.Platform;

/// <summary>
/// Besitzt eine isolierte Fixture-Kopie und ihren einmalig per MSBuild geladenen Katalog.
/// </summary>
public sealed class LoadedFixture : IAsyncDisposable
{
    internal const int MaxConcurrentLoads = 2;

    private static readonly LoadBudgetGate LoadBudget = new(MaxConcurrentLoads);
    private readonly IsolatedFixtureLease lease;
    private readonly SourceFileCatalog catalog;

    private LoadedFixture(IsolatedFixtureLease lease, SourceFileCatalog catalog)
    {
        this.lease = lease;
        this.catalog = catalog;
    }

    public string RootPath => lease.RootPath;

    public SourceFileCatalog Catalog => catalog;

    public Solution Solution => catalog.Solution;

    public static async Task<LoadedFixture> CreateAsync(string fixtureName, CancellationToken cancellationToken = default)
    {
        var lease = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), fixtureName);
        try
        {
            var catalog = await LoadCatalogAsync(lease.RootPath, cancellationToken);
            return new LoadedFixture(lease, catalog);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static async Task<SourceFileCatalog> LoadCatalogAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        return await LoadBudget.ExecuteAsync(
            token => SourceFileCatalog.LoadAsync(rootPath, token),
            cancellationToken);
    }

    internal static int LoadBudgetCapacity => LoadBudget.Capacity;

    public ValueTask DisposeAsync()
    {
        catalog.Dispose();
        lease.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class LoadBudgetGate
{
    private readonly SemaphoreSlim semaphore;

    internal LoadBudgetGate(int capacity)
    {
        semaphore = new SemaphoreSlim(capacity, capacity);
        Capacity = capacity;
    }

    internal int Capacity { get; }

    internal async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
