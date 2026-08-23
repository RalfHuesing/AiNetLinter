#nullable enable

namespace AiNetLinter.Mcp.Projects;

internal static class ProjectRegistryDefaults
{
    internal const int MaxProjects = 4;
    internal static readonly TimeSpan IdleTtl = TimeSpan.FromMinutes(45);
    internal static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);
}

// InstanceFactory muss nicht-blockierend sein: Sie konstruiert die Instanz und
// startet nur den Hintergrund-Load, wartet ihn aber nicht ab. Nur dadurch darf
// die Registry den Factory-Aufruf unter ihrem kurzen Lock ausfuehren.
internal sealed record ProjectRegistryOptions(
    Func<ProjectDefinition, McpCodeGraphServer> InstanceFactory,
    TimeProvider Clock,
    int MaxProjects = ProjectRegistryDefaults.MaxProjects,
    TimeSpan IdleTtl = default,
    TimeSpan TickInterval = default);

internal sealed class ProjectRegistry : IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, ProjectEntry> projects = new(StringComparer.OrdinalIgnoreCase);
    private readonly ProjectRegistryOptions options;
    private readonly TimeSpan idleTtl;
    private readonly CancellationTokenSource tickSource = new();
    private readonly Task tickTask;
    private int disposed;

    public ProjectRegistry(ProjectRegistryOptions options)
    {
        this.options = options;
        idleTtl = ResolvePositive(options.IdleTtl, ProjectRegistryDefaults.IdleTtl);
        var tickInterval = ResolvePositive(options.TickInterval, ProjectRegistryDefaults.TickInterval);
        tickTask = Task.Run(() => MonitorLoopAsync(tickInterval));
    }

    internal ProjectLeaseResult Lease(string projectRoot)
    {
        var key = Canonicalize(projectRoot);
        var retired = new List<McpCodeGraphServer>();
        var result = TryAdoptOrCreate(key, retired);
        foreach (var server in retired)
        {
            server.Dispose();
        }

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        tickSource.Cancel();
        try
        {
            await tickTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (tickSource.IsCancellationRequested)
        {
        }

        List<McpCodeGraphServer> remaining;
        lock (gate)
        {
            remaining = projects.Values.Select(entry => entry.Server).ToList();
            projects.Clear();
        }

        foreach (var server in remaining)
        {
            await server.DisposeAsync().ConfigureAwait(false);
        }

        tickSource.Dispose();
    }

    internal async Task RunEvictionTickAsync()
    {
        List<ProjectEntry> expired;
        lock (gate)
        {
            expired = CollectExpired(UtcNow());
            foreach (var entry in expired)
            {
                projects.Remove(entry.RootPath);
            }
        }

        foreach (var entry in expired)
        {
            await entry.Server.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task MonitorLoopAsync(TimeSpan tickInterval)
    {
        try
        {
            while (!tickSource.IsCancellationRequested)
            {
                await Task.Delay(tickInterval, tickSource.Token).ConfigureAwait(false);
                await RunEvictionTickAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (tickSource.IsCancellationRequested)
        {
        }
    }

    private ProjectLeaseResult TryAdoptOrCreate(string key, List<McpCodeGraphServer> retired)
    {
        var resident = FindAdoptable(key, retired);
        if (resident is not null)
        {
            return ProjectLeaseResult.Success(resident);
        }

        var definition = ProjectDefinitionLoader.Load(key);
        if (!definition.Succeeded)
        {
            return ProjectLeaseResult.Failure(definition.ErrorCode!, definition.Message!);
        }

        return InsertResident(key, definition.Definition!, retired);
    }

    private ProjectLease? FindAdoptable(string key, List<McpCodeGraphServer> retired)
    {
        lock (gate)
        {
            if (!projects.TryGetValue(key, out var entry))
            {
                return null;
            }

            if (entry.Server.LoadState == ServerLoadState.LoadFailed)
            {
                projects.Remove(key);
                retired.Add(entry.Server);
                return null;
            }

            return Adopt(entry);
        }
    }

    private ProjectLeaseResult InsertResident(string key, ProjectDefinition definition, List<McpCodeGraphServer> retired)
    {
        ProjectLease lease;
        lock (gate)
        {
            if (projects.TryGetValue(key, out var raced))
            {
                if (raced.Server.LoadState != ServerLoadState.LoadFailed)
                {
                    return ProjectLeaseResult.Success(Adopt(raced));
                }

                projects.Remove(key);
                retired.Add(raced.Server);
            }

            EvictLeastRecentlyUsed(retired);
            var entry = new ProjectEntry(key, definition, options.InstanceFactory(definition), UtcNow());
            projects.Add(key, entry);
            lease = entry.OpenLease();
        }

        return ProjectLeaseResult.Success(lease);
    }

    private ProjectLease Adopt(ProjectEntry entry)
    {
        entry.PendingEviction = false;
        entry.LastUsedUtc = UtcNow();
        return entry.OpenLease();
    }

    private void EvictLeastRecentlyUsed(List<McpCodeGraphServer> retired)
    {
        while (projects.Count >= options.MaxProjects)
        {
            ProjectEntry? victim = null;
            foreach (var candidate in projects.Values)
            {
                if (candidate.InFlightCount == 0 && (victim is null || candidate.LastUsedUtc < victim.LastUsedUtc))
                {
                    victim = candidate;
                }
            }

            if (victim is null)
            {
                break;
            }

            projects.Remove(victim.RootPath);
            retired.Add(victim.Server);
        }
    }

    private List<ProjectEntry> CollectExpired(DateTime now)
    {
        var expired = new List<ProjectEntry>();
        foreach (var entry in projects.Values)
        {
            if (IsExpired(entry, now))
            {
                expired.Add(entry);
            }
        }

        return expired;
    }

    private bool IsExpired(ProjectEntry entry, DateTime now)
    {
        if (entry.Server.LoadState == ServerLoadState.LoadFailed)
        {
            return true;
        }

        var idleBeyondTtl = now - entry.LastUsedUtc > idleTtl;
        if (entry.InFlightCount > 0)
        {
            if (idleBeyondTtl)
            {
                entry.PendingEviction = true;
            }

            return false;
        }

        return idleBeyondTtl || entry.PendingEviction;
    }

    private DateTime UtcNow() => options.Clock.GetUtcNow().UtcDateTime;

    private static TimeSpan ResolvePositive(TimeSpan value, TimeSpan fallback) =>
        value > TimeSpan.Zero ? value : fallback;

    private static string Canonicalize(string projectRoot)
    {
        var fullPath = Path.GetFullPath(projectRoot);
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
