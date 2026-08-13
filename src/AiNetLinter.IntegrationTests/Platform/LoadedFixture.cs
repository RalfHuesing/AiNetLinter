#nullable enable

using System;
using System.IO;
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

    private static readonly SemaphoreSlim LoadBudget = new(initialCount: MaxConcurrentLoads, maxCount: MaxConcurrentLoads);
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
        var lease = IsolatedFixtureLease.CopyFixture(FindSolutionRoot(), fixtureName);
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
        return await ExecuteWithinLoadBudgetAsync(
            token => SourceFileCatalog.LoadAsync(rootPath, token),
            cancellationToken);
    }

    internal static async Task<T> ExecuteWithinLoadBudgetAsync<T>(Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken = default)
    {
        await LoadBudget.WaitAsync(cancellationToken);
        try
        {
            return await load(cancellationToken);
        }
        finally
        {
            LoadBudget.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        catalog.Dispose();
        lease.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string FindSolutionRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "AiNetLinter.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }
}
