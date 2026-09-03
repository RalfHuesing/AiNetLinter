#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using Microsoft.CodeAnalysis;
namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisContextFactory
{
    internal static Task<(AssemblyContext? Context, string? Error)> CreateAsync(
        string assemblyPath,
        Solution? consumerSolution,
        string? receiverType,
        CancellationToken cancellationToken) =>
        CreateAsync(new AssemblyAnalysisContextRequest(
            assemblyPath,
            consumerSolution,
            receiverType,
            null,
            cancellationToken,
            null));

    internal static async Task<(AssemblyContext? Context, string? Error)> CreateAsync(
        AssemblyAnalysisContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceAttempt = await TryCreateSourceBackedContextAsync(request).ConfigureAwait(false);
        var context = sourceAttempt.Context;
        if (context is null)
        {
            await using var session = new AssemblyAnalysisSession(request.AssemblyPath);
            var refresh = await session.RefreshAsync(request.CancellationToken).ConfigureAwait(false);
            var generation = session.CurrentGeneration;
            if (generation is null)
            {
                return (null, FormatFailure(refresh.Diagnostics));
            }

            context = ApplyFallback(FromGeneration(generation), sourceAttempt.Fallback ?? request.Fallback);
        }

        var contextDiagnostics = context.Diagnostics.ToList();
        var consumer = request.ConsumerSolution is null
            ? new ConsumerSelection(null, null)
            : await FindConsumerReceiverAsync(request.ConsumerSolution, request.ReceiverType, contextDiagnostics, request.CancellationToken).ConfigureAwait(false);
        return (context with
        {
            Diagnostics = DistinctDiagnostics(contextDiagnostics),
            Receiver = consumer.Receiver,
            ConsumerProject = consumer.ProjectName,
        }, null);
    }

    internal static AssemblyContext FromGeneration(AssemblySessionGeneration generation) =>
        new(
            generation.Snapshot.Compilation.Assembly,
            generation.Identity,
            generation.References,
            DistinctDiagnostics(generation.Diagnostics.Select(diagnostic => diagnostic.Message)),
            generation.Snapshot.Compilation,
            null,
            null,
            generation.Origin with
            {
                BodyAvailability = "available",
                ContentMode = "decompiledProject",
            },
            generation.Number,
            generation.Status,
            generation.DecompiledProjectPaths);

    internal static async Task<(AssemblyContext? Context, string? Error)> CreateSourceProjectContextAsync(
        string targetPath,
        Project project,
        AssemblySourceSelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(selection);

        if (!IsSourceSelectionUsable(selection))
        {
            return (null, "Die Source-Project-Selection ist nicht mehr verfügbar.");
        }

        var compilationResult = await TryGetProjectCompilationAsync(project, cancellationToken).ConfigureAwait(false);
        if (compilationResult.Error is not null)
        {
            return (null, compilationResult.Error);
        }

        if (compilationResult.Compilation?.Assembly is null)
        {
            return (null, $"Source-Project-Compilation '{project.Name}' ist nicht verfügbar.");
        }

        var sourceReferences = new AssemblyReferenceResolver().ResolveSourceProjectReferences(
            project,
            selection.SourceLease.Snapshot.Solution,
            Array.Empty<AssemblyReferenceDto>());
        var sourceDiagnostics = selection.SourceLease.Snapshot.Diagnostics
            .Concat(compilationResult.Diagnostics)
            .Distinct()
            .Take(20)
            .ToArray();
        var diagnostics = sourceDiagnostics
            .Select(diagnostic => diagnostic.Message)
            .Concat(sourceReferences.Diagnostics
            .Select(diagnostic => diagnostic.Message)
            )
            .ToList();
        return (BuildSourceProjectContext(new(
            targetPath,
            project,
            selection,
            compilationResult.Compilation,
            sourceReferences,
            diagnostics,
            sourceDiagnostics)), null);
    }

    private static async Task<SourceContextAttempt> TryCreateSourceBackedContextAsync(
        AssemblyAnalysisContextRequest request)
    {
        var preparation = PrepareSourceContext(request);
        if (preparation is null)
        {
            return new(null, request.Fallback);
        }

        if (preparation.Project is null)
            return new(null, CreateWorkspaceFallback(request, preparation.Snapshot));

        var resolver = new AssemblyReferenceResolver();
        var references = resolver.Resolve(preparation.Fingerprint.CanonicalPath);
        if (references.Identity is null) return new(null, request.Fallback);
        var sourceReferences = resolver.ResolveSourceProjectReferences(
            preparation.Project,
            preparation.Snapshot.Solution,
            references.References);
        var effectiveReferences = MergeReferences(references.References, sourceReferences);

        var compilationResult = await TryGetProjectCompilationAsync(
            preparation.Project,
            request.CancellationToken).ConfigureAwait(false);

        if (compilationResult.Compilation?.Assembly is null)
        {
            return new(
                null,
                CreateWorkspaceFallback(
                    request,
                    preparation.Snapshot,
                    compilationResult.Diagnostics,
                    compilationResult.Error));
        }

        var diagnostics = MergeDiagnostics(references.Diagnostics, sourceReferences).ToList();
        diagnostics.AddRange(preparation.Snapshot.Diagnostics.Select(diagnostic => diagnostic.Message));
        diagnostics.AddRange(compilationResult.Diagnostics.Select(diagnostic => diagnostic.Message));
        var sourceDiagnostics = preparation.Snapshot.Diagnostics
            .Concat(compilationResult.Diagnostics)
            .Distinct()
            .Take(20)
            .ToArray();
        return new SourceContextAttempt(BuildSourceBackedContext(new(
            request,
            preparation,
            references,
            effectiveReferences,
            compilationResult.Compilation,
            diagnostics,
            sourceDiagnostics)), null);
    }

    private static SourceContextPreparation? PrepareSourceContext(AssemblyAnalysisContextRequest request)
    {
        var selection = request.SourceSelection;
        if (!IsSourceSelectionUsable(selection)
            || request.CancellationToken.IsCancellationRequested
            || !AssemblyFingerprintCalculator.TryCreate(request.AssemblyPath, out var fingerprint, out _))
        {
            return null;
        }

        var snapshot = selection!.SourceLease.Snapshot;
        var candidate = selection.MatchResult.MatchedCandidate!;
        return new(snapshot, snapshot.Solution.GetProject(candidate.ProjectId), fingerprint!);
    }

    private static async Task<ProjectCompilationResult> TryGetProjectCompilationAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        try
        {
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            return new(compilation, CreateCompilationDiagnostics(project, compilation, cancellationToken), null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException or NotSupportedException)
        {
            var error = $"Source-Project-Compilation '{project.Name}' konnte nicht geladen werden: {ex.Message}";
            return new(
                null,
                [new(
                    ExternalSourceConfigurationDiagnosticCodes.CompilationFailed,
                    error,
                    "error",
                    project.FilePath ?? project.Name)],
                error);
        }
    }

    private static IReadOnlyList<ExternalSourceConfigurationDiagnostic> CreateCompilationDiagnostics(
        Project project,
        Compilation? compilation,
        CancellationToken cancellationToken)
    {
        if (compilation is null) return [];
        return compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity is not DiagnosticSeverity.Hidden)
            .Take(20)
            .Select(diagnostic => new ExternalSourceConfigurationDiagnostic(
                diagnostic.Id,
                diagnostic.GetMessage(),
                diagnostic.Severity.ToString().ToLowerInvariant(),
                GetCompilationDiagnosticLocation(project, diagnostic)))
            .ToArray();
    }

    private static string GetCompilationDiagnosticLocation(Project project, Diagnostic diagnostic)
    {
        var path = diagnostic.Location == Location.None
            ? null
            : diagnostic.Location.GetLineSpan().Path;
        return string.IsNullOrWhiteSpace(path) ? project.FilePath ?? project.Name : path;
    }

    private static AssemblyContext BuildSourceProjectContext(SourceProjectContextBuildRequest request)
    {
        var status = request.Diagnostics.Count == 0
            ? AssemblySessionStatus.Complete
            : AssemblySessionStatus.Partial;
        var assemblyName = request.Project.AssemblyName ?? request.Project.Name;
        var snapshot = request.Selection.SourceLease.Snapshot;
        var origin = new AssemblyOrigin(
            AssemblyAnalysisOriginValues.SourceBackedKind,
            request.TargetPath,
            $"source:{snapshot.Identity.StableValue}:{request.Project.Id}",
            string.Empty,
            "high",
            snapshot.Identity,
            request.Project.FilePath,
            AssemblyAnalysisOriginValues.VerifiedCleanTrust,
            "source",
            "source",
            null,
            request.SourceDiagnostics);
        return new(
            request.Compilation.Assembly!,
            new AssemblyIdentityDto(assemblyName, "0.0.0.0", "neutral", string.Empty),
            request.SourceReferences.References,
            DistinctDiagnostics(request.Diagnostics),
            request.Compilation,
            null,
            null,
            origin,
            0,
            status);
    }

    private static AssemblyContext BuildSourceBackedContext(SourceContextBuildRequest request)
    {
        var status = request.Diagnostics.Count == 0
            ? AssemblySessionStatus.Complete
            : AssemblySessionStatus.Partial;
        var origin = new AssemblyOrigin(
            AssemblyAnalysisOriginValues.SourceBackedKind,
            request.Preparation.Fingerprint.CanonicalPath,
            request.Preparation.Fingerprint.Sha256,
            string.Empty,
            "high",
            request.Preparation.Snapshot.Identity,
            request.Preparation.Project!.FilePath,
            AssemblyAnalysisOriginValues.VerifiedCleanTrust,
            "source",
            "source",
            request.ContextRequest.Fallback?.Reason,
            request.SourceDiagnostics);
        return new(
            request.Compilation.Assembly!,
            request.References.Identity!,
            request.EffectiveReferences,
            DistinctDiagnostics(request.Diagnostics),
            request.Compilation,
            null,
            null,
            origin,
            0,
            status);
    }

    private static AssemblyContext ApplyFallback(
        AssemblyContext context,
        AssemblySourceFallbackMetadata? fallback)
    {
        if (fallback is null) return context;
        var diagnostics = context.Diagnostics
            .Concat(AssemblyAnalysisDiagnostics.FormatExternalDiagnostics(fallback.Diagnostics))
            .Distinct(StringComparer.Ordinal)
            .Take(100)
            .ToList();
        return context with
        {
            Diagnostics = diagnostics,
            Origin = context.Origin with
            {
                FallbackReason = fallback.Reason,
                SourceDiagnostics = fallback.Diagnostics,
            },
        };
    }

    private static AssemblySourceFallbackMetadata CreateWorkspaceFallback(
        AssemblyAnalysisContextRequest request,
        ExternalSourceSnapshot snapshot,
        IReadOnlyList<ExternalSourceConfigurationDiagnostic>? compilationDiagnostics = null,
        string? compilationError = null)
    {
        var diagnostics = (request.Fallback?.Diagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>())
            .Concat(snapshot.Diagnostics)
            .Concat(compilationDiagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>())
            .Concat(CreateCompilationFailureDiagnostic(request, compilationDiagnostics, compilationError))
            .Append(new ExternalSourceConfigurationDiagnostic(
                ExternalSourceConfigurationDiagnosticCodes.WorkspaceDiagnostic,
                "Die Source-Compilation konnte nicht als analysefähiger Workspace verwendet werden; die Assembly wird decompiliert.",
                "error",
                "$workspace"))
            .Distinct()
            .Take(20)
            .ToArray();
        return new(AssemblySourceFallbackReasons.WorkspaceFailure, diagnostics);
    }

    private static IEnumerable<ExternalSourceConfigurationDiagnostic> CreateCompilationFailureDiagnostic(
        AssemblyAnalysisContextRequest request,
        IReadOnlyList<ExternalSourceConfigurationDiagnostic>? compilationDiagnostics,
        string? compilationError)
    {
        if (string.IsNullOrWhiteSpace(compilationError)
            || compilationDiagnostics?.Any(diagnostic =>
                string.Equals(
                    diagnostic.Code,
                    ExternalSourceConfigurationDiagnosticCodes.CompilationFailed,
                    StringComparison.Ordinal)) == true)
        {
            return Array.Empty<ExternalSourceConfigurationDiagnostic>();
        }

        return [new(
            ExternalSourceConfigurationDiagnosticCodes.CompilationFailed,
            compilationError,
            "error",
            request.AssemblyPath)];
    }

    private static IReadOnlyList<AssemblyReferenceDto> MergeReferences(
        IReadOnlyList<AssemblyReferenceDto> assemblyReferences,
        SourceProjectReferenceResolution sourceReferences) =>
        assemblyReferences
            .Where(reference => !sourceReferences.AssemblyNames.Contains(reference.Name))
            .Concat(sourceReferences.References)
            .ToList();

    private static IReadOnlyList<string> MergeDiagnostics(
        IReadOnlyList<AssemblySessionDiagnostic> assemblyDiagnostics,
        SourceProjectReferenceResolution sourceReferences) =>
        assemblyDiagnostics
            .Where(diagnostic => !sourceReferences.AssemblyNames.Any(name =>
                diagnostic.Message.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .Select(diagnostic => diagnostic.Message)
            .Concat(sourceReferences.Diagnostics.Select(diagnostic => diagnostic.Message))
            .ToList();

    private static bool IsSourceSelectionUsable(AssemblySourceSelection? selection)
    {
        if (selection is null || selection.SourceLease.IsDisposed) return false;

        var snapshot = selection.SourceLease.Snapshot;
        var match = selection.MatchResult;
        return !snapshot.IsDisposed
            && selection.IsAttested
            && selection.ProviderHealth is ExternalSourceRepositoryHealth.Verified
            && selection.CheckoutTrust is ExternalSourceCheckoutTrust.Clean
            && match.State == ExternalSourceMatchState.Matched
            && match.MatchedCandidate is not null
            && match.SourceSnapshotIdentity is not null
            && string.Equals(
                match.SourceSnapshotIdentity.StableValue,
                snapshot.Identity.StableValue,
                StringComparison.Ordinal);
    }

    private static async Task<ConsumerSelection> FindConsumerReceiverAsync(
        Solution solution,
        string? receiverType,
        ICollection<string> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var project in solution.Projects.OrderBy(project => project.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Compilation? compilation;
            try
            {
                compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                diagnostics.Add($"Consumer-Compilation '{project.Name}' konnte nicht geladen werden: {ex.Message}");
                continue;
            }

            if (compilation is null) continue;
            var receiver = ResolveReceiver(compilation, receiverType);
            if (receiver is not null) return new ConsumerSelection(receiver, project.Name);
        }

        if (!string.IsNullOrWhiteSpace(receiverType))
        {
            diagnostics.Add($"Consumer-Typ '{receiverType}' konnte in keiner geladenen Compilation aufgelöst werden.");
        }

        return new ConsumerSelection(null, null);
    }

    private static ITypeSymbol? ResolveReceiver(Compilation compilation, string? receiverType)
    {
        if (string.IsNullOrWhiteSpace(receiverType)) return null;
        var normalized = receiverType.Trim().Replace("global::", string.Empty, StringComparison.Ordinal);
        return compilation.GetTypeByMetadataName(normalized)
            ?? AssemblyAnalysisSymbolTraversal.GetAllTypes(compilation.GlobalNamespace)
                .FirstOrDefault(type => string.Equals(
                    type.ToDisplayString(),
                    normalized,
                    StringComparison.Ordinal));
    }

    private static string FormatFailure(IReadOnlyList<AssemblySessionDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "Assembly konnte nicht analysiert werden."
            : string.Join(" ", diagnostics.Select(diagnostic => diagnostic.Message));

    private static IReadOnlyList<string> DistinctDiagnostics(IEnumerable<string> diagnostics) =>
        diagnostics.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Take(100).ToList();

    private sealed record ConsumerSelection(ITypeSymbol? Receiver, string? ProjectName);

    private sealed record SourceContextAttempt(
        AssemblyContext? Context,
        AssemblySourceFallbackMetadata? Fallback);

    private sealed record ProjectCompilationResult(
        Compilation? Compilation,
        IReadOnlyList<ExternalSourceConfigurationDiagnostic> Diagnostics,
        string? Error);

    private sealed record SourceContextPreparation(
        ExternalSourceSnapshot Snapshot,
        Project? Project,
        AssemblyFingerprint Fingerprint);

    private sealed record SourceProjectContextBuildRequest(
        string TargetPath,
        Project Project,
        AssemblySourceSelection Selection,
        Compilation Compilation,
        SourceProjectReferenceResolution SourceReferences,
        IReadOnlyList<string> Diagnostics,
        IReadOnlyList<ExternalSourceConfigurationDiagnostic> SourceDiagnostics);

    private sealed record SourceContextBuildRequest(
        AssemblyAnalysisContextRequest ContextRequest,
        SourceContextPreparation Preparation,
        AssemblyReferenceResolution References,
        IReadOnlyList<AssemblyReferenceDto> EffectiveReferences,
        Compilation Compilation,
        IReadOnlyList<string> Diagnostics,
        IReadOnlyList<ExternalSourceConfigurationDiagnostic> SourceDiagnostics);
}
