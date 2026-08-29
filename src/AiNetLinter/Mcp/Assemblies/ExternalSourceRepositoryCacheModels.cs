#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed record ExternalSourceRepositoryCacheKey
{
    private ExternalSourceRepositoryCacheKey(
        string canonicalRepositoryUrl,
        string solutionPath)
    {
        CanonicalRepositoryUrl = canonicalRepositoryUrl;
        SolutionPath = solutionPath;
        SchemaVersion = ExternalSourceRepositoryCacheContract.CacheSchemaVersion;
        StableValue = ExternalSourceRepositoryCacheContract.CreateStableValue(
            SchemaVersion,
            CanonicalRepositoryUrl,
            SolutionPath);
    }

    internal string CanonicalRepositoryUrl { get; }

    internal string SolutionPath { get; }

    internal string SchemaVersion { get; }

    internal string StableValue { get; }

    internal static bool TryCreate(
        string repositoryUrl,
        string solutionPath,
        out ExternalSourceRepositoryCacheKey? key)
    {
        key = null;
        if (!ExternalSourceRepositoryUrlPolicy.TryNormalize(
                repositoryUrl,
                out var normalizedUrl)
            || normalizedUrl!.Length > ExternalSourceRepositoryCacheContract.MaxRepositoryUrlLength
            || !TryNormalizeSolutionPath(solutionPath, out var normalizedSolutionPath))
        {
            return false;
        }

        key = new ExternalSourceRepositoryCacheKey(
            normalizedUrl!,
            normalizedSolutionPath!);
        return true;
    }

    internal static bool TryNormalizeSolutionPath(
        string value,
        out string? normalizedPath)
    {
        normalizedPath = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim().Replace('\\', '/');
        if (candidate.Length > ExternalSourceRepositoryCacheContract.MaxRelativePathLength
            || candidate.StartsWith("/", StringComparison.Ordinal)
            || candidate.IndexOf(':') >= 0
            || System.IO.Path.IsPathFullyQualified(candidate))
        {
            return false;
        }

        var segments = new List<string>();
        foreach (var segment in candidate.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is ".")
            {
                continue;
            }

            if (segment is ".."
                || segment.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            return false;
        }

        normalizedPath = string.Join('/', segments);
        return normalizedPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsSafeRevision(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > ExternalSourceRepositoryCacheContract.MaxRevisionLength
            || value.Length is not (40 or 64))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')
                and not (>= 'A' and <= 'F'))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed record ExternalSourceRepositoryCacheFileEntry(
    string RelativePath,
    long Length,
    string ContentHash);

internal sealed record ExternalSourceRepositoryCacheManifest(
    string CacheSchemaVersion,
    string CacheKey,
    string CanonicalRepositoryUrl,
    string SolutionPath,
    string LoadedRevision,
    string GenerationName,
    DateTime CreatedUtc,
    IReadOnlyList<ExternalSourceRepositoryCacheFileEntry> Files);

internal sealed record ExternalSourceRepositoryCacheInventory(
    string CacheSchemaVersion,
    string CacheKey,
    string SolutionPath,
    string GenerationName,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<ExternalSourceRepositoryCacheFileEntry> Files);

internal sealed class ExternalSourceRepositoryCachePublishRequest
{
    internal ExternalSourceMapping Mapping { get; init; } = null!;

    internal ExternalSourceCheckoutHandle Checkout { get; init; } = null!;

    internal ExternalSourceCheckoutOwnership CheckoutOwnership { get; init; } = null!;

    internal ExternalSourceRepositoryCacheKey CacheKey { get; init; } = null!;

    internal string SolutionPath { get; init; } = string.Empty;

    internal string LoadedRevision { get; init; } = string.Empty;
}

internal enum ExternalSourceRepositoryCachePublishFailureKind
{
    None,
    InvalidRequest,
    UnsafeSource,
    ManifestInvalid,
    PointerPublishFailed,
    WriteFailed,
    Cancelled,
}

internal sealed record ExternalSourceRepositoryCachePublishResult
{
    internal bool Succeeded { get; init; }

    internal ExternalSourceRepositoryCachePublishFailureKind FailureKind { get; init; }

    internal ExternalSourceRepositoryCacheKey? CacheKey { get; init; }

    internal string? GenerationName { get; init; }

    internal string? GenerationPath { get; init; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; init; }

    internal static ExternalSourceRepositoryCachePublishResult Success(
        ExternalSourceRepositoryCacheKey cacheKey,
        string generationName,
        string generationPath) =>
        new()
        {
            Succeeded = true,
            FailureKind = ExternalSourceRepositoryCachePublishFailureKind.None,
            CacheKey = cacheKey,
            GenerationName = generationName,
            GenerationPath = generationPath,
            Diagnostics = ImmutableArray<ExternalSourceConfigurationDiagnostic>.Empty,
        };

    internal static ExternalSourceRepositoryCachePublishResult Failure(
        ExternalSourceRepositoryCachePublishFailureKind failureKind,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics) =>
        new()
        {
            Succeeded = false,
            FailureKind = failureKind,
            Diagnostics = ImmutableArray.CreateRange(diagnostics),
        };

    internal static ExternalSourceRepositoryCachePublishResult Failure(
        ExternalSourceRepositoryCachePublishFailureKind failureKind) =>
        Failure(
            failureKind,
            [new(
                failureKind is ExternalSourceRepositoryCachePublishFailureKind.Cancelled
                    ? ExternalSourceRepositoryCacheContract.PublishCancelledDiagnosticCode
                    : ExternalSourceRepositoryCacheContract.PublishFailedDiagnosticCode,
                failureKind is ExternalSourceRepositoryCachePublishFailureKind.Cancelled
                    ? "Die Veröffentlichung des Repository-Caches wurde abgebrochen."
                    : "Die Repository-Cachegeneration konnte nicht veröffentlicht werden.",
                "warning",
                "$repository-cache")]);
}

internal sealed class ExternalSourceRepositoryCacheReadRequest
{
    internal ExternalSourceRepositoryCacheKey Key { get; init; } = null!;

    internal string EntryDirectory { get; init; } = string.Empty;

    internal string? ExpectedRevision { get; init; }

    internal string? ExpectedSolutionPath { get; init; }
}

internal sealed record ExternalSourceRepositoryCacheReadResult(
    ExternalSourceRepositoryCacheManifest Manifest,
    string GenerationPath);
