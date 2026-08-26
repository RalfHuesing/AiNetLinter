#nullable enable

using System.Text.Json;

namespace AiNetLinter.Mcp.Daemon;

internal sealed record MruStateEntry(string RootPath, DateTime LastUsedUtc);

internal sealed record MruStateStoreOptions(
    string FilePath,
    TimeProvider Clock,
    Action<string>? Report = null,
    TimeSpan? Debounce = null,
    int MaxProjects = 4);

internal sealed class MruStateStore : IAsyncDisposable
{
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(30);
    private readonly object gate = new();
    private readonly Dictionary<string, MruStateEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider clock;
    private readonly Action<string> report;
    private readonly TimeSpan debounce;
    private readonly string filePath;
    private readonly int maxProjects;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly Timer timer;
    private DateTimeOffset? lastTouchUtc;
    private bool dirty;
    private bool disposed;

    internal MruStateStore(MruStateStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.FilePath))
        {
            throw new ArgumentException("Der MRU-Dateipfad darf nicht leer sein.", nameof(options));
        }

        filePath = Path.GetFullPath(options.FilePath);
        clock = options.Clock ?? throw new ArgumentNullException(nameof(options.Clock));
        report = options.Report ?? (_ => { });
        debounce = options.Debounce is { } value && value > TimeSpan.Zero ? value : DefaultDebounce;
        maxProjects = options.MaxProjects > 0 ? options.MaxProjects : throw new ArgumentOutOfRangeException(nameof(options.MaxProjects));
        timer = new Timer(static state => ((MruStateStore)state!).ScheduleWrite(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    internal static string DefaultFilePath => GetFilePath(null);

    internal static string GetFilePath(string? daemonInstance)
    {
        var normalizedInstance = DaemonInstanceId.Normalize(daemonInstance);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (normalizedInstance is null)
        {
            return Path.Combine(localAppData, "RalfHuesing", "AiNetLinter", "daemon-state.json");
        }

        return Path.Combine(localAppData, "RalfHuesing", "AiNetLinter", $"daemon-state.{normalizedInstance}.json");
    }

    internal IReadOnlyList<MruStateEntry> Read(int maxProjects)
    {
        if (maxProjects <= 0) return [];

        try
        {
            if (!File.Exists(filePath))
            {
                ReplaceEntries([], normalizationRequired: false);
                return [];
            }

            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                ReplaceEntries([], normalizationRequired: true);
                return [];
            }

            var loaded = JsonSerializer.Deserialize<List<MruStateEntry>>(json);
            if (loaded is null)
            {
                ReplaceEntries([], normalizationRequired: true);
                return [];
            }

            var valid = loaded
                .Select(TryNormalizeEntry)
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .GroupBy(entry => entry.RootPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(entry => entry.LastUsedUtc).First())
                .OrderByDescending(entry => entry.LastUsedUtc)
                .Take(maxProjects)
                .ToList();

            ReplaceEntries(valid, normalizationRequired: valid.Count != loaded.Count);
            return valid;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            ReplaceEntries([], normalizationRequired: true);
            report($"[WARN]: MRU-State konnte nicht gelesen werden und wird ignoriert: {exception.Message}");
            return [];
        }
    }

    internal void Touch(string rootPath, DateTime? lastUsedUtc = null)
    {
        var canonicalRoot = CanonicalizeRoot(rootPath);
        if (canonicalRoot is null) return;

        var timestamp = lastUsedUtc ?? clock.GetUtcNow().UtcDateTime;
        lock (gate)
        {
            if (disposed) return;
            if (entries.TryGetValue(canonicalRoot, out var existing)
                && existing.LastUsedUtc == timestamp)
            {
                return;
            }

            entries[canonicalRoot] = new MruStateEntry(canonicalRoot, timestamp);
            lastTouchUtc = clock.GetUtcNow();
            dirty = true;
            timer.Change(debounce, Timeout.InfiniteTimeSpan);
        }
    }

    internal void Remove(string rootPath)
    {
        var canonicalRoot = CanonicalizeRoot(rootPath);
        if (canonicalRoot is null) return;

        lock (gate)
        {
            if (disposed) return;
            if (!entries.Remove(canonicalRoot)) return;
            lastTouchUtc = clock.GetUtcNow();
            dirty = true;
            timer.Change(debounce, Timeout.InfiniteTimeSpan);
        }
    }

    internal async Task WriteIfDueAsync(bool force = false, int? maxProjectsOverride = null)
    {
        IReadOnlyList<MruStateEntry> snapshot;
        lock (gate)
        {
            if (!dirty || (!force && !IsDue())) return;
            snapshot = Snapshot(maxProjectsOverride ?? maxProjects);
        }

        await WriteSnapshotAsync(snapshot).ConfigureAwait(false);
    }

    internal async Task WriteSnapshotAsync(IReadOnlyCollection<MruStateEntry> snapshot)
    {
        await writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Der MRU-Dateipfad hat kein Verzeichnis.");
            }

            Directory.CreateDirectory(directory);
            var temporaryPath = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(temporaryPath, json).ConfigureAwait(false);
                File.Move(temporaryPath, filePath, overwrite: true);
                lock (gate)
                {
                    dirty = false;
                }
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            report($"[WARN]: MRU-State konnte nicht geschrieben werden und wird ignoriert: {exception.Message}");
        }
        finally
        {
            writeGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        bool shouldFlush;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            shouldFlush = true;
        }

        if (shouldFlush)
        {
            IReadOnlyList<MruStateEntry> snapshot;
            lock (gate)
            {
                snapshot = Snapshot(maxProjects);
            }
            await WriteSnapshotAsync(snapshot).ConfigureAwait(false);
        }

        timer.Dispose();
        writeGate.Dispose();
    }

    private void ScheduleWrite()
    {
        _ = ScheduleWriteAsync();
    }

    private async Task ScheduleWriteAsync()
    {
        try
        {
            await WriteIfDueAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ObjectDisposedException or IOException)
        {
            report($"[WARN]: Debouncedes MRU-Schreiben wurde beendet: {exception.Message}");
        }
    }

    private bool IsDue()
    {
        return lastTouchUtc is { } touched && clock.GetUtcNow() - touched >= debounce;
    }

    private IReadOnlyList<MruStateEntry> Snapshot(int maxProjects)
    {
        return entries.Values
            .OrderByDescending(entry => entry.LastUsedUtc)
            .Take(maxProjects)
            .ToList();
    }

    private void ReplaceEntries(
        IReadOnlyCollection<MruStateEntry> loaded,
        bool normalizationRequired)
    {
        lock (gate)
        {
            entries.Clear();
            foreach (var entry in loaded)
            {
                entries[entry.RootPath] = entry;
            }

            dirty = normalizationRequired;
            lastTouchUtc = normalizationRequired ? clock.GetUtcNow() : null;
        }
    }

    private static MruStateEntry? TryNormalizeEntry(MruStateEntry entry)
    {
        var canonicalRoot = CanonicalizeRoot(entry.RootPath);
        return canonicalRoot is null || entry.LastUsedUtc == default
            ? null
            : entry with { RootPath = canonicalRoot };
    }

    private static string? CanonicalizeRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Path.IsPathRooted(rootPath)) return null;

        var fullPath = Path.GetFullPath(rootPath);
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? root
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException ignored)
        {
            _ = ignored;
        }
        catch (UnauthorizedAccessException ignored)
        {
            _ = ignored;
        }
    }
}
