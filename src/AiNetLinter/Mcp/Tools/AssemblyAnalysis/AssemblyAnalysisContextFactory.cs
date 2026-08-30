#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
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
            cancellationToken));

    internal static async Task<(AssemblyContext? Context, string? Error)> CreateAsync(
        AssemblyAnalysisContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await TryCreateSourceBackedContextAsync(request).ConfigureAwait(false);
        if (context is null)
        {
            await using var session = new AssemblyAnalysisSession(request.AssemblyPath);
            var refresh = await session.RefreshAsync(request.CancellationToken).ConfigureAwait(false);
            var generation = session.CurrentGeneration;
            if (generation is null)
            {
                return (null, FormatFailure(refresh.Diagnostics));
            }

            context = FromGeneration(generation);
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
            generation.Origin,
            generation.Number,
            generation.Status);

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

        Compilation? compilation;
        try
        {
            compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException or NotSupportedException)
        {
            return (null, $"Source-Project-Compilation '{project.Name}' konnte nicht geladen werden: {ex.Message}");
        }

        if (compilation?.Assembly is null)
        {
            return (null, $"Source-Project-Compilation '{project.Name}' ist nicht verfügbar.");
        }

        var sourceReferences = new AssemblyReferenceResolver().ResolveSourceProjectReferences(
            project,
            selection.SourceLease.Snapshot.Solution,
            Array.Empty<AssemblyReferenceDto>());
        var diagnostics = sourceReferences.Diagnostics
            .Select(diagnostic => diagnostic.Message)
            .ToList();
        var status = diagnostics.Count == 0
            ? AssemblySessionStatus.Complete
            : AssemblySessionStatus.Partial;
        var assemblyName = project.AssemblyName ?? project.Name;
        var origin = new AssemblyOrigin(
            "source-backed",
            targetPath,
            $"source:{selection.SourceLease.Snapshot.Identity.StableValue}:{project.Id}",
            string.Empty,
            "high",
            selection.SourceLease.Snapshot.Identity,
            project.FilePath,
            "verified-clean");
        return (new AssemblyContext(
            compilation.Assembly,
            new AssemblyIdentityDto(assemblyName, "0.0.0.0", "neutral", string.Empty),
            sourceReferences.References,
            DistinctDiagnostics(diagnostics),
            compilation,
            null,
            null,
            origin,
            0,
            status), null);
    }

    private static async Task<AssemblyContext?> TryCreateSourceBackedContextAsync(
        AssemblyAnalysisContextRequest request)
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
        var project = snapshot.Solution.GetProject(candidate.ProjectId);
        if (project is null) return null;

        var resolver = new AssemblyReferenceResolver();
        var references = resolver.Resolve(fingerprint!.CanonicalPath);
        if (references.Identity is null) return null;
        var sourceReferences = resolver.ResolveSourceProjectReferences(
            project,
            snapshot.Solution,
            references.References);
        var effectiveReferences = MergeReferences(references.References, sourceReferences);

        Compilation? compilation;
        try
        {
            compilation = await project.GetCompilationAsync(request.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException or NotSupportedException)
        {
            return null;
        }

        if (compilation is null || compilation.Assembly is null) return null;

        var diagnostics = MergeDiagnostics(references.Diagnostics, sourceReferences);
        var status = diagnostics.Count == 0
            ? AssemblySessionStatus.Complete
            : AssemblySessionStatus.Partial;
        var origin = new AssemblyOrigin(
            "source-backed",
            fingerprint.CanonicalPath,
            fingerprint.Sha256,
            string.Empty,
            "high",
            snapshot.Identity,
            project.FilePath,
            "verified-clean");
        return new AssemblyContext(
            compilation.Assembly,
            references.Identity,
            effectiveReferences,
            DistinctDiagnostics(diagnostics),
            compilation,
            null,
            null,
            origin,
            0,
            status);
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
}
