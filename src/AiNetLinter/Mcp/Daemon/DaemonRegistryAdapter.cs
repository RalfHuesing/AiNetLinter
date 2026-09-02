#nullable enable

using AiNetLinter.Mcp.Projects;

namespace AiNetLinter.Mcp.Daemon;

internal interface IDaemonRegistry : IAsyncDisposable
{
    int ActiveLoadCount { get; }

    IReadOnlyList<DaemonProjectSnapshot> Snapshots();

    ProjectSnapshot? FindSnapshot(string rootPath);

    DaemonRegistryLeaseResult Lease(string rootPath);
}

internal sealed record DaemonProjectSnapshot(string RootPath, DateTime LastUsedUtc);

internal sealed class DaemonRegistryLease : IDisposable
{
    private readonly Action release;
    private readonly Action adoptLoadedState;
    private readonly Task? loadTask;
    private int disposed;

    internal DaemonRegistryLease(
        string rootPath,
        Task? loadTask,
        Action adoptLoadedState,
        Action release)
    {
        RootPath = rootPath;
        this.loadTask = loadTask;
        this.adoptLoadedState = adoptLoadedState;
        this.release = release;
    }

    internal string RootPath { get; }

    internal Task? LoadTask => loadTask;

    internal void AdoptLoadedState()
    {
        adoptLoadedState();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            release();
        }
    }
}

internal sealed record DaemonRegistryLeaseResult(
    DaemonRegistryLease? Lease,
    string? ErrorMessage = null)
{
    internal bool Succeeded => Lease is not null;
}

internal sealed class DaemonRegistryAdapter : IDaemonRegistry
{
    private readonly ProjectRegistry registry;

    internal DaemonRegistryAdapter(ProjectRegistry registry)
    {
        this.registry = registry;
    }

    public int ActiveLoadCount => registry.ActiveLoadCount;

    public IReadOnlyList<DaemonProjectSnapshot> Snapshots() =>
        registry.Snapshots()
            .Select(snapshot => new DaemonProjectSnapshot(snapshot.RootPath, snapshot.LastUsedUtc))
            .ToList();

    public ProjectSnapshot? FindSnapshot(string rootPath) => registry.FindSnapshot(rootPath);

    public DaemonRegistryLeaseResult Lease(string rootPath)
    {
        var result = registry.Lease(rootPath);
        return result.Succeeded
            ? new DaemonRegistryLeaseResult(CreateLease(result.Lease!))
            : new DaemonRegistryLeaseResult(null, result.ErrorMessage);
    }

    private static DaemonRegistryLease CreateLease(ProjectLease lease) =>
        new(
            lease.RootPath,
            lease.Server.LoadTask,
            () => _ = lease.Server.GetCurrentSolution(),
            lease.Dispose);

    public ValueTask DisposeAsync() => registry.DisposeAsync();
}
