#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed partial class AssemblyAnalysisSession
{
    private async Task<AssemblySessionRefreshResult> BuildFreshGenerationAsync(
        AssemblyFingerprint fingerprint,
        AssemblyDecompilationCacheKey key,
        AssemblyReferenceResolution references,
        IReadOnlyList<AssemblySessionDiagnostic> cacheDiagnostics,
        CancellationToken cancellationToken)
    {
        var stagingDirectory = cache.CreateStagingDirectory(key);
        try
        {
            var decompilation = await decompilationAdapter.DecompileAsync(
                new DecompilationRequest(fingerprint.CanonicalPath, fingerprint, key, decompilationOptions, cancellationToken, stagingDirectory),
                references).ConfigureAwait(false);
            var diagnostics = CombineDiagnostics(cacheDiagnostics, references.Diagnostics, decompilation.Diagnostics);
            if (!decompilation.IsComplete || decompilation.Documents.Count == 0)
            {
                return FailureResult(EnsureDiagnostic(diagnostics, AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions)), "Die Decompilation hat keine vollständige, analysierbare Generation erzeugt."));
            }

            var status = DetermineStatus(references.Diagnostics, decompilation);
            return await CreateAndInstallGenerationAsync(
                new AssemblyGenerationBuildRequest(
                    fingerprint,
                    key,
                    references,
                    decompilation.Documents,
                    status,
                    diagnostics,
                    new AssemblyCachePublishRequest(fingerprint, key, decompilationOptions, references, decompilation, status, stagingDirectory),
                    decompilation.ProjectFilePath),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            cache.DiscardStagingDirectory(stagingDirectory);
        }
    }

    private async Task<AssemblySessionRefreshResult> CreateAndInstallGenerationAsync(
        AssemblyGenerationBuildRequest request,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await CreateSnapshotAsync(request.Fingerprint, request.References, request.Documents, request.Status, request.ProjectFilePath, cancellationToken).ConfigureAwait(false);
        if (snapshotResult.Snapshot is null) return FailureResult(CombineDiagnostics(request.Diagnostics, snapshotResult.Diagnostics));
        var finalStatus = snapshotResult.Diagnostics.Count == 0 ? request.Status : AssemblySessionStatus.Partial;

        if (request.PublishRequest is not null)
        {
            var publish = await cache.PublishAsync(request.PublishRequest with { Status = finalStatus }, cancellationToken).ConfigureAwait(false);
            if (!publish.Succeeded)
            {
                snapshotResult.Snapshot.Dispose();
                return FailureResult(CombineDiagnostics(request.Diagnostics, OptionalDiagnostic(publish.Diagnostic)));
            }

            if (!cache.TryRead(
                    new AssemblyCacheReadRequest(request.Key, request.Fingerprint, request.References),
                    out var published,
                    out var cacheDiagnostic)
                || published is null)
            {
                snapshotResult.Snapshot.Dispose();
                return FailureResult(CombineDiagnostics(request.Diagnostics, OptionalDiagnostic(cacheDiagnostic)));
            }

            snapshotResult.Snapshot.Dispose();
            request = request with
            {
                Documents = published.Documents,
                ProjectFilePath = published.ProjectFilePath,
                Status = ResolveManifestStatus(published.Manifest.Status.Status, request.References.Diagnostics, ManifestDiagnostics(published.Manifest)),
                PublishRequest = null,
            };
            snapshotResult = await CreateSnapshotAsync(request.Fingerprint, request.References, request.Documents, request.Status, request.ProjectFilePath, cancellationToken).ConfigureAwait(false);
            if (snapshotResult.Snapshot is null) return FailureResult(CombineDiagnostics(request.Diagnostics, snapshotResult.Diagnostics));
            finalStatus = snapshotResult.Diagnostics.Count == 0 ? request.Status : AssemblySessionStatus.Partial;
        }

        var generation = new AssemblySessionGeneration(
            Interlocked.Increment(ref nextGeneration),
            request.Fingerprint,
            request.Key,
            request.References.Identity!,
            finalStatus,
            snapshotResult.Snapshot,
            request.References.References,
            CombineDiagnostics(request.Diagnostics, snapshotResult.Diagnostics),
            CreateGenerationOrigin(request.Fingerprint, request.Documents, finalStatus),
            DecompiledProjectPaths.Create(request.ProjectFilePath, request.Documents));

        return InstallGeneration(generation);
    }

    private async Task<WorkspaceCreationResult> CreateSnapshotAsync(
        AssemblyFingerprint fingerprint,
        AssemblyReferenceResolution references,
        IReadOnlyList<DecompiledDocument> documents,
        AssemblySessionStatus status,
        string? projectFilePath,
        CancellationToken cancellationToken)
    {
        AssemblyRoslynSnapshot? snapshot = null;
        try
        {
            snapshot = await workspaceFactory.CreateAsync(
                new AssemblyWorkspaceRequest(fingerprint.CanonicalPath, fingerprint, documents, references.MetadataReferences, status, projectFilePath),
                references.Identity!.Name,
                fingerprint.Sha256,
                cancellationToken).ConfigureAwait(false);
            var diagnostics = ValidateCompilation(snapshot.Compilation, cancellationToken);
            if (diagnostics.Any(diagnostic => diagnostic.Severity == AssemblyDiagnosticSeverity.Error))
            {
                snapshot.Dispose();
                return new WorkspaceCreationResult(null, diagnostics);
            }

            return new WorkspaceCreationResult(snapshot, diagnostics);
        }
        catch (OperationCanceledException)
        {
            snapshot?.Dispose();
            throw;
        }
        catch (InvalidOperationException ex)
        {
            snapshot?.Dispose();
            return new WorkspaceCreationResult(null, [new(AssemblyDiagnosticCodes.For(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblySessionStatus.Failed)), $"Roslyn-Snapshot konnte nicht erzeugt werden: {ex.Message}", AssemblyDiagnosticSeverity.Error)]);
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
            return [new(
                AssemblyDiagnosticCodes.For(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblyRoslynSnapshot.Compilation)),
                $"Die dekompilierte Compilation enthält nicht parsbaren Quelltext: {string.Join("; ", syntaxErrors.Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage()))}.",
                AssemblyDiagnosticSeverity.Warning)];
        }

        return [new(
            AssemblyDiagnosticCodes.For(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblyRoslynSnapshot.Solution)),
            $"Die dekompilierte Compilation enthält {errors.Count} semantische Decompiler-/Referenzdiagnosen: {string.Join("; ", errors.Take(5).Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage()))}.",
            AssemblyDiagnosticSeverity.Warning)];
    }
}
