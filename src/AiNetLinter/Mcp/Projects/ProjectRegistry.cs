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
// die Registry den Factory-Aufruf ausserhalb ihres kurzen Locks ausfuehren. Ein
// Factory-Fehler (z. B. ungueltige Regeldatei) erzeugt keinen Entry.
internal sealed record ProjectRegistryOptions(
    Func<ProjectDefinition, ProjectInstanceCreation> InstanceFactory,
    TimeProvider Clock,
    int MaxProjects = ProjectRegistryDefaults.MaxProjects,
    TimeSpan IdleTtl = default,
    TimeSpan TickInterval = default)
{
    internal Action? BeforeLeaseRelease { get; init; }

    // Test-only barrier for the former lookup-to-reservation interleaving; production leaves it null.
    internal Action? BeforeCreationReservation { get; init; }

    internal Func<string, ProjectCreationAttempt, ProjectCreationAttempt?>? BeforePublishCreation { get; init; }
}

/// <summary>
/// Read-Only-Blick auf einen residenten Key fuer Statusabfragen ohne Lease-Seiteneffekt:
/// LastUsedUtc wird nicht angefasst und kein Load angestossen.
/// </summary>
internal sealed record ProjectSnapshot(
    string RootPath,
    ProjectDefinition Definition,
    DateTime LastUsedUtc,
    McpCodeGraphServer Server);

internal sealed class ProjectRegistry : IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, ProjectEntry> projects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProjectCreationReservation> reservations = new(StringComparer.OrdinalIgnoreCase);
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

    internal int ActiveLoadCount
    {
        get
        {
            lock (gate)
            {
                return projects.Values.Count(entry => entry.Server.LoadState == ServerLoadState.Loading);
            }
        }
    }

    internal int PendingCreationWaiters(string projectRoot)
    {
        var key = Canonicalize(projectRoot);
        lock (gate)
        {
            return reservations.TryGetValue(key, out var reservation) ? reservation.WaiterCount : 0;
        }
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
        if (options.BeforeCreationReservation is not null)
        {
            var observed = FindResidentBeforeCreationBarrier(key, retired);
            if (observed is not null)
            {
                return ProjectLeaseResult.Success(observed);
            }

            options.BeforeCreationReservation();
        }

        ProjectCreationReservation reservation;
        lock (gate)
        {
            var resident = FindAdoptable(key, retired);
            if (resident is not null)
            {
                return ProjectLeaseResult.Success(resident);
            }

            reservation = ReserveCreationUnderLock(key);
        }

        ProjectCreationAttempt attempt;
        try
        {
            attempt = reservation.GetValue();
        }
        catch
        {
            RemoveReservation(key, reservation);
            throw;
        }

        if (options.BeforePublishCreation is { } beforePublishCreation)
        {
            var winnerAttempt = beforePublishCreation(key, attempt);
            if (winnerAttempt is not null)
            {
                var winner = PublishCreation(key, reservation, winnerAttempt, retired);
                winner.Lease?.Dispose();
            }
        }

        return PublishCreation(key, reservation, attempt, retired);
    }

    private ProjectLease? FindResidentBeforeCreationBarrier(string key, List<McpCodeGraphServer> retired)
    {
        lock (gate)
        {
            return FindAdoptable(key, retired);
        }
    }

    private ProjectCreationReservation ReserveCreationUnderLock(string key)
    {
        if (reservations.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var reservation = new ProjectCreationReservation(() => CreateInstance(key));
        reservations.Add(key, reservation);
        return reservation;
    }

    private ProjectCreationAttempt CreateInstance(string key)
    {
        var definition = ProjectDefinitionLoader.Load(key);
        if (!definition.Succeeded)
        {
            return new(null, ProjectInstanceCreation.Failed(definition.ErrorCode!, definition.Message!));
        }

        return new(definition.Definition, options.InstanceFactory(definition.Definition!));
    }

    private ProjectLeaseResult PublishCreation(
        string key,
        ProjectCreationReservation reservation,
        ProjectCreationAttempt attempt,
        List<McpCodeGraphServer> retired)
    {
        var created = attempt.Creation;
        if (!created.Succeeded)
        {
            RemoveReservation(key, reservation);
            return ProjectLeaseResult.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        lock (gate)
        {
            if (projects.TryGetValue(key, out var raced))
            {
                RemoveReservationUnderLock(key, reservation);
                if (created.Server is not null && !ReferenceEquals(created.Server, raced.Server))
                {
                    retired.Add(created.Server);
                }

                return ProjectLeaseResult.Success(Adopt(raced));
            }

            EvictLeastRecentlyUsed(retired);
            var entry = new ProjectEntry(key, attempt.Definition!, created.Server!, UtcNow());
            projects.Add(key, entry);
            RemoveReservationUnderLock(key, reservation);
            return ProjectLeaseResult.Success(Adopt(entry));
        }
    }

    private void RemoveReservation(string key, ProjectCreationReservation reservation)
    {
        lock (gate)
        {
            RemoveReservationUnderLock(key, reservation);
        }
    }

    private void RemoveReservationUnderLock(string key, ProjectCreationReservation reservation)
    {
        if (reservations.TryGetValue(key, out var current) && ReferenceEquals(current, reservation))
        {
            reservations.Remove(key);
        }
    }

    private ProjectLease? FindAdoptable(string key, List<McpCodeGraphServer> retired)
    {
        if (!projects.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.Server.LoadState == ServerLoadState.LoadFailed
            && entry.FailureLeaseReleased
            && entry.InFlightCount == 0)
        {
            projects.Remove(key);
            retired.Add(entry.Server);
            return null;
        }

        return Adopt(entry);
    }

    internal IReadOnlyList<ProjectSnapshot> Snapshots()
    {
        lock (gate)
        {
            return projects.Values.Select(entry => SnapshotOf(entry)).ToList();
        }
    }

    internal ProjectSnapshot? FindSnapshot(string projectRoot)
    {
        var key = Canonicalize(projectRoot);
        lock (gate)
        {
            return projects.TryGetValue(key, out var entry) ? SnapshotOf(entry) : null;
        }
    }

    internal ProjectSnapshot SnapshotFor(ProjectLease lease)
    {
        lock (gate)
        {
            if (!projects.TryGetValue(lease.RootPath, out var entry)
                || !ReferenceEquals(entry.Server, lease.Server))
            {
                throw new InvalidOperationException("Der Projekt-Lease ist nicht mehr resident.");
            }

            return SnapshotOf(entry);
        }
    }

    private static ProjectSnapshot SnapshotOf(ProjectEntry entry) =>
        new(entry.RootPath, entry.Definition, entry.LastUsedUtc, entry.Server);

    private ProjectLease Adopt(ProjectEntry entry)
    {
        entry.PendingEviction = false;
        entry.LastUsedUtc = UtcNow();
        return entry.OpenLease(lease =>
        {
            options.BeforeLeaseRelease?.Invoke();
            ReleaseEntry(entry, lease.LoadFailedResponseEmitted);
        });
    }

    private void ReleaseEntry(ProjectEntry entry, bool loadFailedResponseEmitted)
    {
        lock (gate)
        {
            if (loadFailedResponseEmitted
                && entry.Server.LoadState == ServerLoadState.LoadFailed
                && projects.TryGetValue(entry.RootPath, out var current)
                && ReferenceEquals(current, entry))
            {
                entry.FailureLeaseReleased = true;
            }
        }
    }

    /// <summary>
    /// Versucht vor einem Insert, das Register unter <see cref="ProjectRegistryOptions.MaxProjects"/>
    /// zu verkleinern. Sind alle Entries busy (oder nicht verdraengbar), bricht die Raeumung
    /// ergebnislos ab und der neue Entry wird trotzdem registriert — der Bestand darf dann
    /// ueber MaxProjects wachsen. Das ist bewusste Kapazitaetsentscheidung (Nutzerentscheid
    /// vom 2026-08-24: Ueberlauf erlaubt), kein Defekt: Der Sync-Lease darf weder blockieren
    /// noch ablehnen. Der TTL-Tick reklamiert den Ueberschuss nicht aktiv; er raeumt erst,
    /// wenn Slots frei werden (Idle-TTL bzw. LRU-Druck bei kuenftigen Inserts).
    /// </summary>
    private void EvictLeastRecentlyUsed(List<McpCodeGraphServer> retired)
    {
        while (projects.Count >= options.MaxProjects)
        {
            ProjectEntry? victim = null;
            foreach (var candidate in projects.Values)
            {
                if (candidate.InFlightCount == 0
                    && (candidate.Server.LoadState != ServerLoadState.LoadFailed || candidate.FailureLeaseReleased)
                    && (victim is null || candidate.LastUsedUtc < victim.LastUsedUtc))
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
            return entry.InFlightCount == 0 && entry.FailureLeaseReleased;
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
