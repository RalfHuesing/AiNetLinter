#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class AssemblyAnalysisSession : IDisposable, IAsyncDisposable
{
    private readonly object gate = new();
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly AssemblyAnalysisSessionOptions sessionOptions;
    private readonly AssemblyDecompilationOptions decompilationOptions;
    private readonly AssemblyDecompilationCache cache;
    private readonly AssemblyReferenceResolver referenceResolver = new();
    private readonly AssemblyDecompilationAdapter decompilationAdapter = new();
    private readonly AssemblyRoslynWorkspaceFactory workspaceFactory = new();
    private readonly List<AssemblyRoslynSnapshot> snapshots = [];
    private AssemblySessionGeneration? current;
    private AssemblySessionState state;
    private long nextGeneration;
    private bool disposed;

    internal AssemblyAnalysisSession(string assemblyPath, AssemblyDecompilationOptions? options = null, string? cacheRoot = null)
        : this(new AssemblyAnalysisSessionOptions(assemblyPath, options, cacheRoot))
    {
    }

    internal AssemblyAnalysisSession(AssemblyAnalysisSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        sessionOptions = options;
        decompilationOptions = options.Decompilation ?? AssemblyDecompilationOptions.Default;
        cache = new AssemblyDecompilationCache(options.CacheRoot);
        state = new AssemblySessionState(
            AssemblySessionStatus.Loading,
            null,
            null,
            null,
            [],
            DateTime.UtcNow);
    }

    internal AssemblySessionState State
    {
        get
        {
            lock (gate) return state;
        }
    }

    internal AssemblySessionGeneration? CurrentGeneration
    {
        get
        {
            lock (gate) return current;
        }
    }

    internal AssemblyAnalysisSnapshotLease? AcquireSnapshot()
    {
        lock (gate)
        {
            return current is null ? null : new AssemblyAnalysisSnapshotLease(current);
        }
    }

    internal async Task<AssemblySessionRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return FailureResult(new AssemblySessionDiagnostic(
                "assembly-refresh-cancelled",
                "Der Assembly-Refresh wurde vor dem Aufbau einer neuen Generation abgebrochen.",
                "error"));
        }

        try
        {
            return await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public void Dispose()
    {
        List<AssemblyRoslynSnapshot> snapshots;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            snapshots = this.snapshots.ToList();
            current = null;
            state = state with { Status = AssemblySessionStatus.Failed, CurrentGeneration = null, UpdatedUtc = DateTime.UtcNow };
        }

        foreach (var snapshot in snapshots) snapshot.Dispose();
        refreshGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<AssemblySessionRefreshResult> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        if (IsDisposed())
        {
            return FailureResult((AssemblySessionDiagnostic)new("assembly-session-disposed", "Die Assembly-Session wurde bereits beendet.", "error"));
        }

        if (!ValidateOptions(out var optionsDiagnostic))
        {
            return FailureResult(optionsDiagnostic!);
        }

        if (!AssemblyFingerprintCalculator.TryCreate(sessionOptions.AssemblyPath, out var fingerprint, out var fingerprintDiagnostic))
        {
            return FailureResult(fingerprintDiagnostic!);
        }

        if (fingerprint!.Length > decompilationOptions.MaxAssemblyBytes)
        {
            return FailureResult(new AssemblySessionDiagnostic(
                "assembly-size-limit",
                $"Die Assembly überschreitet die Dateigrößenbegrenzung ({fingerprint.Length} von {decompilationOptions.MaxAssemblyBytes} Bytes).",
                "error"));
        }

        if (TryReuseCurrent(fingerprint, out var reused)) return reused;
        return await RefreshGenerationAsync(fingerprint, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AssemblySessionRefreshResult> RefreshGenerationAsync(
        AssemblyFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var key = AssemblyFingerprintCalculator.CreateCacheKey(fingerprint, decompilationOptions);
        var references = referenceResolver.Resolve(fingerprint.CanonicalPath);
        if (TryReadCache(key, out var cached, out var cacheDiagnostics) && cached is not null)
        {
            return await PublishWorkspaceAsync(
                fingerprint,
                key,
                references,
                cached.Documents,
                ResolveManifestStatus(cached.Manifest.Status, references.Diagnostics, cacheDiagnostics),
                cacheDiagnostics,
                cancellationToken).ConfigureAwait(false);
        }

        return await BuildFreshGenerationAsync(
            fingerprint,
            key,
            references,
            cacheDiagnostics,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<AssemblySessionRefreshResult> BuildFreshGenerationAsync(
        AssemblyFingerprint fingerprint,
        AssemblyDecompilationCacheKey key,
        AssemblyReferenceResolution references,
        IReadOnlyList<AssemblySessionDiagnostic> cacheDiagnostics,
        CancellationToken cancellationToken)
    {
        var decompilation = await decompilationAdapter.DecompileAsync(
            new DecompilationRequest(
                fingerprint.CanonicalPath,
                fingerprint,
                key,
                decompilationOptions,
                cancellationToken),
            references).ConfigureAwait(false);
        if (decompilation.Documents.Count == 0)
        {
            var diagnostics = CombineDiagnostics(references.Diagnostics, decompilation.Diagnostics, cacheDiagnostics);
            return FailureResult(diagnostics);
        }

        var status = DetermineStatus(references.Diagnostics, decompilation);
        var publishRequest = new AssemblyCachePublishRequest(
            fingerprint,
            key,
            decompilationOptions,
            references,
            decompilation,
            status);
        var publishResult = cache.Publish(publishRequest);
        if (!publishResult.Succeeded)
        {
            var diagnostics = CombineDiagnostics(
                references.Diagnostics,
                decompilation.Diagnostics,
                OptionalDiagnostic(publishResult.Diagnostic));
            return FailureResult(diagnostics);
        }

        if (!cache.TryRead(key, out var published, out var publishedDiagnostic) || published is null)
        {
            var diagnostics = CombineDiagnostics(
                references.Diagnostics,
                decompilation.Diagnostics,
                OptionalDiagnostic(publishedDiagnostic));
            return FailureResult(diagnostics);
        }

        return await PublishWorkspaceAsync(
            fingerprint,
            key,
            references,
            published.Documents,
            status,
            CombineDiagnostics(cacheDiagnostics, references.Diagnostics, decompilation.Diagnostics),
            cancellationToken).ConfigureAwait(false);
    }

    private bool TryReadCache(
        AssemblyDecompilationCacheKey key,
        out CachedDecompilationGeneration? generation,
        out IReadOnlyList<AssemblySessionDiagnostic> diagnostics)
    {
        var collected = new List<AssemblySessionDiagnostic>();
        if (cache.TryRead(key, out generation, out var cacheDiagnostic) && generation is not null)
        {
            collected.AddRange(ManifestDiagnostics(generation.Manifest));
            diagnostics = collected;
            return true;
        }

        if (cacheDiagnostic is not null) collected.Add(cacheDiagnostic);
        diagnostics = collected;
        return false;
    }

    private async Task<AssemblySessionRefreshResult> PublishWorkspaceAsync(
        AssemblyFingerprint fingerprint,
        AssemblyDecompilationCacheKey key,
        AssemblyReferenceResolution references,
        IReadOnlyList<DecompiledDocument> documents,
        AssemblySessionStatus status,
        IReadOnlyList<AssemblySessionDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await workspaceFactory.CreateAsync(
                new AssemblyWorkspaceRequest(
                    fingerprint.CanonicalPath,
                    fingerprint,
                    documents,
                    references.MetadataReferences,
                    status),
                references.Identity?.Name ?? Path.GetFileNameWithoutExtension(fingerprint.CanonicalPath),
                fingerprint.Sha256,
                cancellationToken).ConfigureAwait(false);
            var origin = CreateGenerationOrigin(fingerprint, documents, status);
            var generation = new AssemblySessionGeneration(
                Interlocked.Increment(ref nextGeneration),
                fingerprint,
                key,
                status,
                snapshot,
                references.References,
                diagnostics,
                origin);
            lock (gate)
            {
                if (disposed)
                {
                    snapshot.Dispose();
                    return FailureResult((AssemblySessionDiagnostic)new("assembly-session-disposed", "Die Assembly-Session wurde während des Aufbaus beendet.", "error"));
                }

                current = generation;
                snapshots.Add(snapshot);
                state = new AssemblySessionState(
                    status,
                    generation.Number,
                    generation.Number,
                    fingerprint,
                    diagnostics,
                    DateTime.UtcNow);
            }

            return new AssemblySessionRefreshResult(status, generation.Number, false, diagnostics);
        }
        catch (OperationCanceledException)
        {
            return FailureResult((AssemblySessionDiagnostic)new("assembly-workspace-cancelled", "Der Roslyn-Snapshot wurde wegen Cancellation abgebrochen.", "error"));
        }
        catch (InvalidOperationException ex)
        {
            return FailureResult((AssemblySessionDiagnostic)new("assembly-workspace-failed", $"Roslyn-Snapshot konnte nicht erzeugt werden: {ex.Message}", "error"));
        }
    }

    private bool TryReuseCurrent(AssemblyFingerprint fingerprint, out AssemblySessionRefreshResult result)
    {
        lock (gate)
        {
            if (current is null || !string.Equals(current.Fingerprint.Sha256, fingerprint.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                result = null!;
                return false;
            }

            state = state with { Fingerprint = fingerprint, UpdatedUtc = DateTime.UtcNow };
            result = new AssemblySessionRefreshResult(state.Status, current.Number, true, state.Diagnostics);
            return true;
        }
    }

    private AssemblySessionRefreshResult FailureResult(AssemblySessionDiagnostic diagnostic) =>
        FailureResult([diagnostic]);

    private AssemblySessionRefreshResult FailureResult(IReadOnlyList<AssemblySessionDiagnostic> diagnostics)
    {
        lock (gate)
        {
            var status = current is null ? AssemblySessionStatus.Failed : AssemblySessionStatus.Degraded;
            state = state with
            {
                Status = status,
                CurrentGeneration = current?.Number,
                LastGoodGeneration = current?.Number,
                Diagnostics = DistinctDiagnostics(diagnostics),
                UpdatedUtc = DateTime.UtcNow,
            };
            return new AssemblySessionRefreshResult(status, current?.Number, false, state.Diagnostics);
        }
    }

    private bool IsDisposed()
    {
        lock (gate) return disposed;
    }

    private bool ValidateOptions(out AssemblySessionDiagnostic? diagnostic)
    {
        if (decompilationOptions.MaxAssemblyBytes > 0
            && decompilationOptions.MaxTypes > 0
            && decompilationOptions.MaxMembers > 0
            && decompilationOptions.MaxDocumentCharacters > 0
            && decompilationOptions.MaxComplexity > 0)
        {
            diagnostic = null;
            return true;
        }

        diagnostic = new("assembly-options-invalid", "Die Assembly-Decompilation-Optionen enthalten ungültige Größen- oder Komplexitätsgrenzen.", "error");
        return false;
    }

    private static AssemblySessionStatus DetermineStatus(
        IReadOnlyList<AssemblySessionDiagnostic> referenceDiagnostics,
        DecompilationResult decompilation) =>
        referenceDiagnostics.Count == 0 && decompilation.IsComplete
            ? AssemblySessionStatus.Complete
            : AssemblySessionStatus.Partial;

    private static AssemblySessionStatus ResolveManifestStatus(
        string status,
        IReadOnlyList<AssemblySessionDiagnostic> referenceDiagnostics,
        IReadOnlyList<AssemblySessionDiagnostic> manifestDiagnostics)
    {
        if (referenceDiagnostics.Count > 0 || manifestDiagnostics.Any(diagnostic => diagnostic.Severity == "error"))
        {
            return AssemblySessionStatus.Partial;
        }

        return Enum.TryParse<AssemblySessionStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : AssemblySessionStatus.Partial;
    }

    private static IReadOnlyList<AssemblySessionDiagnostic> ManifestDiagnostics(AssemblyDecompilationManifest manifest) =>
        manifest.Warnings.Select(message => new AssemblySessionDiagnostic("assembly-cache-warning", message)).Concat(
            manifest.Errors.Select(message => new AssemblySessionDiagnostic("assembly-cache-error", message, "error"))).ToList();

    private static IReadOnlyList<AssemblySessionDiagnostic> CombineDiagnostics(params IReadOnlyList<AssemblySessionDiagnostic>[] groups) =>
        DistinctDiagnostics(groups.SelectMany(group => group));

    private static IReadOnlyList<AssemblySessionDiagnostic> OptionalDiagnostic(AssemblySessionDiagnostic? diagnostic) =>
        diagnostic is null ? [] : [diagnostic];

    private static IReadOnlyList<AssemblySessionDiagnostic> DistinctDiagnostics(IEnumerable<AssemblySessionDiagnostic> diagnostics) =>
        diagnostics.Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.Message))
            .GroupBy(diagnostic => diagnostic.Code + "|" + diagnostic.Message, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .Take(100)
            .ToList();

    private static AssemblyOrigin CreateGenerationOrigin(
        AssemblyFingerprint fingerprint,
        IReadOnlyList<DecompiledDocument> documents,
        AssemblySessionStatus status) =>
        new(
            "decompiled",
            fingerprint.CanonicalPath,
            fingerprint.Sha256,
            documents.FirstOrDefault()?.GeneratedPath ?? string.Empty,
            status == AssemblySessionStatus.Complete ? "high" : "medium");
}

internal sealed class AssemblyAnalysisSnapshotLease : IDisposable
{
    private int disposed;

    internal AssemblyAnalysisSnapshotLease(AssemblySessionGeneration generation)
    {
        Generation = generation;
    }

    internal AssemblySessionGeneration Generation { get; }

    internal AssemblyRoslynSnapshot Snapshot => Generation.Snapshot;

    public void Dispose() => Interlocked.Exchange(ref disposed, 1);
}
