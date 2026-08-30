#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

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
        lock (gate) return current is null ? null : new AssemblyAnalysisSnapshotLease(current);
    }

    internal async Task<AssemblySessionRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return FailureResultSingle(new(AssemblyDiagnosticCodes.For(nameof(AssemblyAnalysisSession), nameof(AssemblyAnalysisSession.RefreshAsync)), "Der Assembly-Refresh wurde vor dem Aufbau einer neuen Generation abgebrochen.", "error"));
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
        if (IsDisposed()) return FailureResultSingle(new(AssemblyDiagnosticCodes.For(nameof(AssemblyAnalysisSession), nameof(AssemblyAnalysisSession.Dispose)), "Die Assembly-Session wurde bereits beendet.", "error"));
        if (!ValidateOptions(out var optionsDiagnostic)) return FailureResultSingle(optionsDiagnostic!);
        if (!AssemblyFingerprintCalculator.TryCreate(sessionOptions.AssemblyPath, out var fingerprint, out var fingerprintDiagnostic))
        {
            return FailureResultSingle(fingerprintDiagnostic!);
        }

        if (fingerprint!.Length > decompilationOptions.MaxAssemblyBytes)
        {
            return FailureResultSingle(new(AssemblyDiagnosticCodes.For(nameof(AssemblyAnalysisSession), nameof(AssemblyFingerprint.Length)), $"Die Assembly überschreitet die Dateigrößenbegrenzung ({fingerprint.Length} von {decompilationOptions.MaxAssemblyBytes} Bytes).", "error"));
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
            return FailureResult(references.Diagnostics);
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

    private async Task<AssemblySessionRefreshResult> BuildFreshGenerationAsync(
        AssemblyFingerprint fingerprint,
        AssemblyDecompilationCacheKey key,
        AssemblyReferenceResolution references,
        IReadOnlyList<AssemblySessionDiagnostic> cacheDiagnostics,
        CancellationToken cancellationToken)
    {
        var decompilation = await decompilationAdapter.DecompileAsync(
            new DecompilationRequest(fingerprint.CanonicalPath, fingerprint, key, decompilationOptions, cancellationToken),
            references).ConfigureAwait(false);
        var diagnostics = CombineDiagnostics(cacheDiagnostics, references.Diagnostics, decompilation.Diagnostics);
        if (decompilation.Documents.Count == 0)
        {
            return FailureResult(EnsureDiagnostic(diagnostics, AssemblyDiagnosticCodes.For(nameof(AssemblyAnalysisSession), nameof(DecompilationResult.Documents)), "Die Decompilation hat keine analysierbaren Dokumente erzeugt."));
        }

        var status = DetermineStatus(references.Diagnostics, decompilation);
        var result = await CreateAndInstallGenerationAsync(
            new AssemblyGenerationBuildRequest(
                fingerprint,
                key,
                references,
                decompilation.Documents,
                status,
                diagnostics,
                new AssemblyCachePublishRequest(fingerprint, key, decompilationOptions, references, decompilation, status)),
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<AssemblySessionRefreshResult> CreateAndInstallGenerationAsync(
        AssemblyGenerationBuildRequest request,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await CreateSnapshotAsync(request.Fingerprint, request.References, request.Documents, request.Status, cancellationToken).ConfigureAwait(false);
        if (snapshotResult.Snapshot is null) return FailureResult(CombineDiagnostics(request.Diagnostics, snapshotResult.Diagnostics));
        var finalStatus = snapshotResult.Diagnostics.Count == 0 ? request.Status : AssemblySessionStatus.Partial;
        var generation = new AssemblySessionGeneration(
            Interlocked.Increment(ref nextGeneration),
            request.Fingerprint,
            request.Key,
            request.References.Identity!,
            finalStatus,
            snapshotResult.Snapshot,
            request.References.References,
            CombineDiagnostics(request.Diagnostics, snapshotResult.Diagnostics),
            CreateGenerationOrigin(request.Fingerprint, request.Documents, finalStatus));

        if (request.PublishRequest is not null)
        {
            var publish = cache.Publish(request.PublishRequest with { Status = finalStatus });
            if (!publish.Succeeded)
            {
                generation.Snapshot.Dispose();
                return FailureResult(CombineDiagnostics(generation.Diagnostics, OptionalDiagnostic(publish.Diagnostic)));
            }
        }

        return InstallGeneration(generation);
    }

    private async Task<WorkspaceCreationResult> CreateSnapshotAsync(
        AssemblyFingerprint fingerprint,
        AssemblyReferenceResolution references,
        IReadOnlyList<DecompiledDocument> documents,
        AssemblySessionStatus status,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await workspaceFactory.CreateAsync(
                new AssemblyWorkspaceRequest(fingerprint.CanonicalPath, fingerprint, documents, references.MetadataReferences, status),
                references.Identity!.Name,
                fingerprint.Sha256,
                cancellationToken).ConfigureAwait(false);
            var diagnostics = ValidateCompilation(snapshot.Compilation, cancellationToken);
            if (diagnostics.Any(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase)))
            {
                snapshot.Dispose();
                return new WorkspaceCreationResult(null, diagnostics);
            }

            return new WorkspaceCreationResult(snapshot, diagnostics);
        }
        catch (OperationCanceledException)
        {
            return new WorkspaceCreationResult(null, [new(AssemblyDiagnosticCodes.For(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblySessionStatus.Loading)), "Der Roslyn-Snapshot wurde wegen Cancellation abgebrochen.", "error")]);
        }
        catch (InvalidOperationException ex)
        {
            return new WorkspaceCreationResult(null, [new(AssemblyDiagnosticCodes.For(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblySessionStatus.Failed)), $"Roslyn-Snapshot konnte nicht erzeugt werden: {ex.Message}", "error")]);
        }
    }

    private static IReadOnlyList<AssemblySessionDiagnostic> ValidateCompilation(Compilation compilation, CancellationToken cancellationToken)
    {
        var errors = compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => !AssemblyDiagnosticCodes.IsExpectedDeclarationOnlyDiagnostic(diagnostic.Id))
            .ToList();
        if (errors.Count == 0)
        {
            return [];
        }

        var syntaxErrors = compilation.SyntaxTrees
            .SelectMany(tree => tree.GetDiagnostics(cancellationToken))
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Take(5)
            .ToList();
        if (syntaxErrors.Count > 0)
        {
            return [new(AssemblyDiagnosticCodes.For(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblyRoslynSnapshot.Compilation)), $"Die synthetische Compilation enthält nicht parsbaren Quelltext: {string.Join("; ", syntaxErrors.Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage()))}", "error")];
        }

        return [new(
            AssemblyDiagnosticCodes.For(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblyRoslynSnapshot.Solution)),
            $"Die synthetische Compilation enthält {errors.Count} semantische Decompiler-/Referenzdiagnosen: {string.Join("; ", errors.Take(5).Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage()))}.")];
    }

    private AssemblySessionRefreshResult InstallGeneration(AssemblySessionGeneration generation)
    {
        lock (gate)
        {
            if (disposed)
            {
                generation.Snapshot.Dispose();
                var diagnostic = new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyAnalysisSession), nameof(AssemblyAnalysisSession.Dispose)), "Die Assembly-Session wurde während des Aufbaus beendet.", "error");
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
            snapshots.Add(generation.Snapshot);
            state = new AssemblySessionState(
                generation.Status,
                generation.Number,
                generation.Number,
                generation.Fingerprint,
                generation.Diagnostics,
                DateTime.UtcNow);
            return new AssemblySessionRefreshResult(generation.Status, generation.Number, false, generation.Diagnostics);
        }
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

    private AssemblySessionRefreshResult FailureResult(IReadOnlyList<AssemblySessionDiagnostic> diagnostics)
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
            return new AssemblySessionRefreshResult(status, current?.Number, false, visible);
        }
    }

    private bool IsDisposed()
    {
        lock (gate) return disposed;
    }

    private bool ValidateOptions(out AssemblySessionDiagnostic? diagnostic)
    {
        if (decompilationOptions.MaxAssemblyBytes > 0 && decompilationOptions.MaxTypes > 0 && decompilationOptions.MaxMembers > 0 && decompilationOptions.MaxDocumentCharacters > 0 && decompilationOptions.MaxComplexity > 0)
        {
            diagnostic = null;
            return true;
        }

        diagnostic = new(AssemblyDiagnosticCodes.For(nameof(AssemblyAnalysisSessionOptions), nameof(AssemblyAnalysisSessionOptions.CacheRoot)), "Die Assembly-Decompilation-Optionen enthalten ungültige Größen- oder Komplexitätsgrenzen.", "error");
        return false;
    }

    private static AssemblySessionStatus DetermineStatus(IReadOnlyList<AssemblySessionDiagnostic> references, DecompilationResult decompilation) =>
        references.Count == 0 && decompilation.IsComplete ? AssemblySessionStatus.Complete : AssemblySessionStatus.Partial;

    private static AssemblySessionStatus ResolveManifestStatus(
        string status,
        IReadOnlyList<AssemblySessionDiagnostic> referenceDiagnostics,
        IReadOnlyList<AssemblySessionDiagnostic> manifestDiagnostics) =>
        referenceDiagnostics.Count > 0 || manifestDiagnostics.Any(diagnostic => diagnostic.Severity == "error")
            ? AssemblySessionStatus.Partial
            : AssemblySessionStatusExtensions.TryParsePersisted(status, out var parsed) ? parsed : AssemblySessionStatus.Partial;

    private static IReadOnlyList<AssemblySessionDiagnostic> ManifestDiagnostics(AssemblyDecompilationManifest manifest) =>
        manifest.Diagnostics.Warnings.Select(message => new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationManifest), nameof(AssemblyDecompilationManifest.Diagnostics)), message))
            .Concat(manifest.Diagnostics.Errors.Select(message => new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationManifest), nameof(AssemblyDecompilationManifest.Status)), message, "error")))
            .ToList();

    private static IReadOnlyList<AssemblySessionDiagnostic> CombineDiagnostics(params IEnumerable<AssemblySessionDiagnostic>[] groups) =>
        DistinctDiagnostics(groups.SelectMany(group => group));

    private static IReadOnlyList<AssemblySessionDiagnostic> OptionalDiagnostic(AssemblySessionDiagnostic? diagnostic) => diagnostic is null ? [] : [diagnostic];

    private static IReadOnlyList<AssemblySessionDiagnostic> EnsureDiagnostic(IReadOnlyList<AssemblySessionDiagnostic> diagnostics, string code, string message) =>
        diagnostics.Count == 0 ? [new AssemblySessionDiagnostic(code, message, "error")] : diagnostics;

    private static IReadOnlyList<AssemblySessionDiagnostic> DistinctDiagnostics(IEnumerable<AssemblySessionDiagnostic> diagnostics) =>
        diagnostics.Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.Message))
            .GroupBy(diagnostic => diagnostic.Code + "|" + diagnostic.Message, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
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
    private int disposed;

    internal AssemblyAnalysisSnapshotLease(AssemblySessionGeneration generation)
    {
        Generation = generation;
    }

    internal AssemblySessionGeneration Generation { get; }

    internal AssemblyRoslynSnapshot Snapshot => Generation.Snapshot;

    public void Dispose() => Interlocked.Exchange(ref disposed, 1);
}
