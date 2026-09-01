#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;

internal sealed record SourceSnapshotIdentity
{
    private const string StableValueSeparator = "|";
    private const string StableValueLengthSeparator = ":";

    private SourceSnapshotIdentity(
        string repositoryUrl,
        string loadedRevision,
        string solutionPath)
    {
        RepositoryUrl = repositoryUrl;
        LoadedRevision = loadedRevision;
        SolutionPath = solutionPath;
        StableValue = BuildStableValue(repositoryUrl, loadedRevision, solutionPath);
    }

    internal string RepositoryUrl { get; }

    internal string LoadedRevision { get; }

    internal string SolutionPath { get; }

    internal string StableValue { get; }

    internal static SourceSnapshotIdentity Create(
        ExternalSourceMapping mapping,
        string loadedRevision)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (string.IsNullOrWhiteSpace(loadedRevision))
        {
            throw new ArgumentException(
                "Die tatsächlich geladene Revision darf nicht leer sein.",
                nameof(loadedRevision));
        }

        return new(
            CanonicalizeRepositoryUrl(mapping.Url),
            loadedRevision.Trim(),
            CanonicalizeSolutionPath(mapping.SolutionPath));
    }

    private static string CanonicalizeRepositoryUrl(string value)
    {
        if (!ExternalSourceUrlPolicy.TryNormalize(value, out var normalizedUrl))
        {
            throw new ArgumentException(
                "Die Repository-URL muss eine absolute HTTP(S)-URL sein.",
                nameof(value));
        }

        return normalizedUrl!;
    }

    private static string CanonicalizeSolutionPath(string value)
    {
        var path = value.Trim().Replace('\\', '/');
        EnsureRelativeSolutionPath(path, value);
        var segments = NormalizeSolutionSegments(path, value);

        var normalized = string.Join('/', segments);
        EnsureSolutionExtension(normalized, value);

        return normalized;
    }

    private static void EnsureRelativeSolutionPath(string path, string originalValue)
    {
        if (path.Length == 0
            || Path.IsPathRooted(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || ExternalSourcePathRules.IsDriveQualified(path))
        {
            throw new ArgumentException(
                "Der Solution-Pfad muss repository-relativ sein.",
                nameof(originalValue));
        }
    }

    private static List<string> NormalizeSolutionSegments(string path, string originalValue)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is ".")
            {
                continue;
            }

            if (segment is "..")
            {
                if (segments.Count == 0)
                {
                    throw new ArgumentException(
                        "Der Solution-Pfad darf nicht aus dem Repository ausbrechen.",
                        nameof(originalValue));
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    "Der Solution-Pfad enthält ein ungültiges Segment.",
                    nameof(originalValue));
            }

            segments.Add(segment);
        }

        return segments;
    }

    private static void EnsureSolutionExtension(string path, string originalValue)
    {
        if (path.Length == 0
            || !(path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Der Solution-Pfad muss auf .sln oder .slnx enden.",
                nameof(originalValue));
        }
    }

    private static string BuildStableValue(params string[] components)
    {
        var parts = new string[components.Length];
        for (var index = 0; index < components.Length; index++)
        {
            parts[index] = string.Concat(
                components[index].Length.ToString(CultureInfo.InvariantCulture),
                StableValueLengthSeparator,
                components[index]);
        }

        return string.Join(StableValueSeparator, parts);
    }

}

internal static class DisposeFailureAggregator
{
    internal static void ThrowIfAny(List<Exception> failures)
    {
        if (failures.Count == 0) return;
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new AggregateException(failures);
    }
}

internal sealed record ExternalSourceSnapshotOwnership(
    IExternalSourceCheckoutOwner? CheckoutOwner = null,
    ExternalSourceCheckoutMaterializationUse? MaterializationUse = null,
    bool IsAttested = false,
    ExternalSourceSnapshotResourceUsage? ResourceUsage = null,
    ExternalResourceReservation? ResourceReservation = null);

internal sealed record ExternalSourceSnapshotResourceUsage(long DiskBytes, long MemoryBytes)
{
    internal static ExternalSourceSnapshotResourceUsage Estimate(Solution solution)
    {
        ArgumentNullException.ThrowIfNull(solution);
        var projectCount = Math.Max(1, solution.ProjectIds.Count);
        return new(projectCount, projectCount);
    }

    internal static async ValueTask<ExternalSourceSnapshotResourceUsage> EstimateCheckoutAsync(
        string checkoutPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutPath);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            long totalBytes = 0;
            foreach (var path in Directory.EnumerateFiles(checkoutPath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalBytes = checked(totalBytes + new FileInfo(path).Length);
            }

            var boundedBytes = Math.Max(1, totalBytes);
            return new(boundedBytes, boundedBytes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException)
        {
            return new(long.MaxValue, long.MaxValue);
        }
    }
}

internal sealed class ExternalSourceSnapshot : IDisposable
{
    private readonly Workspace workspace;
    private readonly IExternalSourceCheckoutOwner? checkoutOwner;
    private readonly ExternalSourceCheckoutMaterializationUse? materializationUse;
    private ExternalResourceReservation? resourceReservation;
    private int disposed;

    internal ExternalSourceSnapshot(
        SourceSnapshotIdentity identity,
        Solution solution,
        Workspace workspace,
        ExternalSourceSnapshotOwnership? ownership = null,
        IEnumerable<ExternalSourceConfigurationDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(workspace);

        Identity = identity;
        Solution = solution;
        this.workspace = workspace;
        checkoutOwner = ownership?.CheckoutOwner;
        materializationUse = ownership?.MaterializationUse;
        resourceReservation = ownership?.ResourceReservation;
        IsAttested = ownership?.IsAttested == true;
        ResourceUsage = ownership?.ResourceUsage ?? ExternalSourceSnapshotResourceUsage.Estimate(solution);
        Diagnostics = (diagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>())
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.Code))
            .Distinct()
            .Take(20)
            .ToImmutableArray();
    }

    internal SourceSnapshotIdentity Identity { get; }

    internal Solution Solution { get; }

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    internal bool IsAttested { get; }

    internal ExternalSourceSnapshotResourceUsage ResourceUsage { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }

    internal ExternalResourceReservation? TakeResourceReservation() =>
        Interlocked.Exchange(ref resourceReservation, null);

    internal bool OwnsCheckout(IExternalSourceCheckoutOwner checkout) =>
        ReferenceEquals(checkoutOwner, checkout);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        var failures = new List<Exception>();
        try
        {
            workspace.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            checkoutOwner?.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            materializationUse?.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            Interlocked.Exchange(ref resourceReservation, null)?.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        DisposeFailureAggregator.ThrowIfAny(failures);
    }
}
