#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed partial class AssemblyAnalysisSession : IDisposable, IAsyncDisposable
{
    private readonly object gate = new();
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly AssemblyAnalysisSessionOptions sessionOptions;
    private readonly AssemblyDecompilationOptions decompilationOptions;
    private readonly AssemblyDecompilationCache cache;
    private readonly AssemblyReferenceResolver referenceResolver = new();
    private readonly AssemblyDecompilationAdapter decompilationAdapter = new();
    private readonly AssemblyRoslynWorkspaceFactory workspaceFactory = new();
    private readonly List<AssemblySessionGeneration> generations = [];
    private readonly TaskCompletionSource<object?> leasesDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private AssemblySessionGeneration? current;
    private AssemblySessionState state;
    private long nextGeneration;
    private bool disposed;

    internal AssemblyAnalysisSession(string assemblyPath, AssemblyDecompilationOptions? options = null, string? cacheRoot = null)
        : this(CreateConfiguredOptions(assemblyPath, options, cacheRoot))
    {
    }

    private static AssemblyAnalysisSessionOptions CreateConfiguredOptions(
        string assemblyPath,
        AssemblyDecompilationOptions? options,
        string? cacheRoot)
    {
        var configured = AssemblyAnalysisConfigurationLoader.Load().Options;
        return new AssemblyAnalysisSessionOptions(
            assemblyPath,
            options ?? new AssemblyDecompilationOptions(Timeout: configured.DecompilationTimeout),
            cacheRoot ?? configured.CacheRoot);
    }

    internal AssemblyAnalysisSession(AssemblyAnalysisSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        sessionOptions = options;
        decompilationOptions = options.Decompilation ?? AssemblyDecompilationOptions.Default;
        cache = new AssemblyDecompilationCache(options.CacheRoot);
        nextGeneration = options.GenerationStart;
        state = new AssemblySessionState(AssemblySessionStatus.Loading, null, null, null, [], DateTime.UtcNow);
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
            if (disposed || current is null) return null;
            current.ActiveLeaseCount++;
            return new AssemblyAnalysisSnapshotLease(this, current);
        }
    }

    internal async Task<AssemblySessionRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);

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
        List<AssemblyRoslynSnapshot> snapshotsToDispose;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            current = null;
            state = state with { Status = AssemblySessionStatus.Failed, CurrentGeneration = null, UpdatedUtc = DateTime.UtcNow };
            snapshotsToDispose = generations
                .Where(generation => generation.ActiveLeaseCount == 0)
                .Select(generation => generation.Snapshot)
                .ToList();
            generations.RemoveAll(generation => generation.ActiveLeaseCount == 0);
            if (generations.Count == 0) leasesDrained.TrySetResult(null);
        }

        foreach (var snapshot in snapshotsToDispose) snapshot.Dispose();
        refreshGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await leasesDrained.Task.ConfigureAwait(false);
    }

    private async Task<AssemblySessionRefreshResult> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        if (IsDisposed()) return FailureResultSingle(new(AssemblyDiagnosticCodes.For(nameof(AssemblyAnalysisSession), nameof(AssemblyAnalysisSession.Dispose)), "Die Assembly-Session wurde bereits beendet.", AssemblyDiagnosticSeverity.Error));
        if (!ValidateOptions(out var optionsDiagnostic)) return FailureResultSingle(optionsDiagnostic!);
        if (!AssemblyFingerprintCalculator.TryCreate(sessionOptions.AssemblyPath, out var fingerprint, out var fingerprintDiagnostic))
        {
            return FailureResultSingle(fingerprintDiagnostic!);
        }

        if (fingerprint is null)
        {
            return FailureResultSingle(new(AssemblyDiagnosticCodes.For(nameof(AssemblyFingerprintCalculator), nameof(AssemblyFingerprintCalculator.TryCreate)), "Die Assembly-Fingerprint konnte nicht erzeugt werden.", AssemblyDiagnosticSeverity.Error));
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
        if (references.Identity is null)
        {
            var metadataDiagnostic = references.Diagnostics.FirstOrDefault(
                diagnostic => diagnostic.Code == AssemblyDiagnosticCodes.MetadataMissing);
            return FailureResult(
                references.Diagnostics,
                metadataDiagnostic is null
                    ? null
                    : new AssemblySessionFailure(
                        AssemblySessionFailureKind.MetadataUnavailable,
                        metadataDiagnostic));
        }

        if (TryReadCache(key, fingerprint, references, out var cached, out var cacheDiagnostics) && cached is not null)
        {
            var status = ResolveManifestStatus(cached.Manifest.Status.Status, references.Diagnostics, ManifestDiagnostics(cached.Manifest));
            return await CreateAndInstallGenerationAsync(
                new AssemblyGenerationBuildRequest(
                    fingerprint,
                    key,
                    references,
                    cached.Documents,
                    status,
                    CombineDiagnostics(references.Diagnostics, ManifestDiagnostics(cached.Manifest))),
                cancellationToken).ConfigureAwait(false);
        }

        return await BuildFreshGenerationAsync(fingerprint, key, references, cacheDiagnostics, cancellationToken).ConfigureAwait(false);
    }

    private AssemblySessionRefreshResult InstallGeneration(AssemblySessionGeneration generation)
    {
        AssemblyRoslynSnapshot? retiredSnapshot = null;
        lock (gate)
        {
            if (disposed)
            {
                generation.Snapshot.Dispose();
                var diagnostic = new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyAnalysisSession), nameof(AssemblyAnalysisSession.Dispose)), "Die Assembly-Session wurde während des Aufbaus beendet.", AssemblyDiagnosticSeverity.Error);
                state = state with
                {
                    Status = AssemblySessionStatus.Failed,
                    CurrentGeneration = null,
                    Diagnostics = [diagnostic],
                    UpdatedUtc = DateTime.UtcNow,
                };
                return new AssemblySessionRefreshResult(AssemblySessionStatus.Failed, null, false, [diagnostic]);
            }

            current = generation;
            generations.Add(generation);
            if (generations.Count > 1)
            {
                var previous = generations[^2];
                if (previous.ActiveLeaseCount == 0)
                {
                    generations.Remove(previous);
                    retiredSnapshot = previous.Snapshot;
                }
            }
            state = new AssemblySessionState(
                generation.Status,
                generation.Number,
                generation.Number,
                generation.Fingerprint,
                generation.Diagnostics,
                DateTime.UtcNow);
        }

        retiredSnapshot?.Dispose();
        return new AssemblySessionRefreshResult(generation.Status, generation.Number, false, generation.Diagnostics);
    }

    private bool TryReadCache(
        AssemblyDecompilationCacheKey key,
        AssemblyFingerprint fingerprint,
        AssemblyReferenceResolution references,
        out CachedDecompilationGeneration? generation,
        out IReadOnlyList<AssemblySessionDiagnostic> diagnostics)
    {
        if (cache.TryRead(new AssemblyCacheReadRequest(key, fingerprint, references), out generation, out var cacheDiagnostic) && generation is not null)
        {
            diagnostics = [];
            return true;
        }

        diagnostics = OptionalDiagnostic(cacheDiagnostic);
        return false;
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

    private AssemblySessionRefreshResult FailureResultSingle(AssemblySessionDiagnostic diagnostic) => FailureResult([diagnostic]);

    private AssemblySessionRefreshResult FailureResult(
        IReadOnlyList<AssemblySessionDiagnostic> diagnostics,
        AssemblySessionFailure? failure = null)
    {
        lock (gate)
        {
            var status = current is null ? AssemblySessionStatus.Failed : AssemblySessionStatus.Degraded;
            var visible = DistinctDiagnostics(EnsureDiagnostic(diagnostics, AssemblyDiagnosticCodes.For(nameof(AssemblyAnalysisSession), nameof(AssemblySessionRefreshResult.Diagnostics)), "Assembly-Refresh konnte keinen neuen analysierbaren Snapshot erzeugen."));
            state = state with
            {
                Status = status,
                CurrentGeneration = current?.Number,
                LastGoodGeneration = current?.Number,
                Diagnostics = visible,
                UpdatedUtc = DateTime.UtcNow,
            };
            return new AssemblySessionRefreshResult(status, current?.Number, false, visible, failure);
        }
    }

    private bool IsDisposed()
    {
        lock (gate) return disposed;
    }

    internal void ReleaseSnapshot(AssemblySessionGeneration generation)
    {
        AssemblyRoslynSnapshot? snapshotToDispose = null;
        lock (gate)
        {
            if (generation.ActiveLeaseCount == 0) return;
            generation.ActiveLeaseCount--;
            if (generation.ActiveLeaseCount == 0
                && !ReferenceEquals(current, generation)
                && generations.Remove(generation))
            {
                snapshotToDispose = generation.Snapshot;
            }

            if (disposed && generations.Count == 0)
            {
                leasesDrained.TrySetResult(null);
            }
        }

        snapshotToDispose?.Dispose();
    }

    private bool ValidateOptions(out AssemblySessionDiagnostic? diagnostic)
    {
        if (decompilationOptions.EffectiveTimeout > TimeSpan.Zero
            && !string.IsNullOrWhiteSpace(decompilationOptions.DecompilerVersion)
            && !string.IsNullOrWhiteSpace(decompilationOptions.CacheSchemaVersion))
        {
            diagnostic = null;
            return true;
        }

        diagnostic = new(AssemblyDiagnosticCodes.For(nameof(AssemblyAnalysisSessionOptions), nameof(AssemblyAnalysisSessionOptions.CacheRoot)), "Die Assembly-Decompilation-Optionen enthalten ungültige Werte.", AssemblyDiagnosticSeverity.Error);
        return false;
    }

    private static AssemblySessionStatus DetermineStatus(IReadOnlyList<AssemblySessionDiagnostic> references, DecompilationResult decompilation) =>
        references.Count == 0 && decompilation.IsComplete ? AssemblySessionStatus.Complete : AssemblySessionStatus.Partial;

    private static AssemblySessionStatus ResolveManifestStatus(
        string status,
        IReadOnlyList<AssemblySessionDiagnostic> referenceDiagnostics,
        IReadOnlyList<AssemblySessionDiagnostic> manifestDiagnostics) =>
        referenceDiagnostics.Count > 0 || manifestDiagnostics.Any(diagnostic => diagnostic.Severity == AssemblyDiagnosticSeverity.Error)
            ? AssemblySessionStatus.Partial
            : AssemblySessionStatusExtensions.TryParsePersisted(status, out var parsed) ? parsed : AssemblySessionStatus.Partial;

    private static IReadOnlyList<AssemblySessionDiagnostic> ManifestDiagnostics(AssemblyDecompilationManifest manifest) =>
        manifest.Diagnostics.Warnings.Select(message => new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationManifest), nameof(AssemblyDecompilationManifest.Diagnostics)), message))
            .Concat(manifest.Diagnostics.Errors.Select(message => new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationManifest), nameof(AssemblyDecompilationManifest.Status)), message, AssemblyDiagnosticSeverity.Error)))
            .ToList();

    private static IReadOnlyList<AssemblySessionDiagnostic> CombineDiagnostics(params IEnumerable<AssemblySessionDiagnostic>[] groups) =>
        DistinctDiagnostics(groups.SelectMany(group => group));

    private static IReadOnlyList<AssemblySessionDiagnostic> OptionalDiagnostic(AssemblySessionDiagnostic? diagnostic) => diagnostic is null ? [] : [diagnostic];

    private static IReadOnlyList<AssemblySessionDiagnostic> EnsureDiagnostic(IReadOnlyList<AssemblySessionDiagnostic> diagnostics, string code, string message) =>
        diagnostics.Count == 0 ? [new AssemblySessionDiagnostic(code, message, AssemblyDiagnosticSeverity.Error)] : diagnostics;

    private static IReadOnlyList<AssemblySessionDiagnostic> DistinctDiagnostics(IEnumerable<AssemblySessionDiagnostic> diagnostics) =>
        diagnostics.Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.Message))
            .GroupBy(diagnostic => diagnostic.Code + "|" + diagnostic.Message, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(diagnostic => diagnostic.Severity == AssemblyDiagnosticSeverity.Error ? 0 : 1)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .Take(100)
            .ToList();

    private static AssemblyOrigin CreateGenerationOrigin(AssemblyFingerprint fingerprint, IReadOnlyList<DecompiledDocument> documents, AssemblySessionStatus status) =>
        new("decompiled", fingerprint.CanonicalPath, fingerprint.Sha256, documents.FirstOrDefault()?.GeneratedPath ?? string.Empty, status == AssemblySessionStatus.Complete ? "high" : "medium");

    private sealed record WorkspaceCreationResult(AssemblyRoslynSnapshot? Snapshot, IReadOnlyList<AssemblySessionDiagnostic> Diagnostics);
}

internal sealed class AssemblyAnalysisSnapshotLease : IDisposable
{
    private readonly AssemblyAnalysisSession session;
    private readonly AssemblySessionGeneration generation;
    private int disposed;

    internal AssemblyAnalysisSnapshotLease(
        AssemblyAnalysisSession session,
        AssemblySessionGeneration generation)
    {
        this.session = session;
        this.generation = generation;
    }

    internal AssemblySessionGeneration Generation => generation;

    internal AssemblyRoslynSnapshot Snapshot => generation.Snapshot;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            session.ReleaseSnapshot(generation);
        }
    }
}
