#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal enum AssemblySessionStatus
{
    Loading,
    Complete,
    Partial,
    Degraded,
    Failed,
}

internal sealed record AssemblyOrigin(
    string OriginKind,
    string CanonicalPath,
    string ContentHash,
    string GeneratedDocumentPath,
    string Confidence = "high",
    string BodyAvailability = "available",
    string ContentMode = "decompiled")
{
    /// <summary>Interner Kompatibilitätsalias; im MCP-Payload ist <see cref="OriginKind"/> maßgeblich.</summary>
    internal string Kind => OriginKind;

    internal bool IsDecompiled => string.Equals(OriginKind, "decompiled", StringComparison.Ordinal);
}

internal sealed record AssemblyDecompilationOptions(
    TimeSpan Timeout = default,
    string DecompilerVersion = AssemblyDecompilationOptions.CurrentDecompilerVersion,
    string CacheSchemaVersion = AssemblyDecompilationOptions.CurrentCacheSchemaVersion)
{
    internal const string CurrentDecompilerVersion = "10.0.1.8346";
    internal const string CurrentCacheSchemaVersion = AssemblyCacheContract.CacheSchemaVersion;
    internal const int MaxCancelAfterMilliseconds = int.MaxValue;
    internal static readonly TimeSpan MaxCancelAfterTimeout = TimeSpan.FromMilliseconds(MaxCancelAfterMilliseconds);
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(180);

    internal static AssemblyDecompilationOptions Default => new();

    internal TimeSpan EffectiveTimeout => Timeout > TimeSpan.Zero
        ? Timeout
        : DefaultTimeout;

    internal static bool IsSupportedTimeout(TimeSpan timeout) =>
        timeout > TimeSpan.Zero && timeout <= MaxCancelAfterTimeout;

    internal string Identity => string.Join(
        "|",
        EffectiveTimeout.Ticks,
        DecompilerVersion,
        CacheSchemaVersion);
}

internal sealed record AssemblyDecompilationConfiguration(
    AssemblyDecompilationOptions Options,
    string CacheRoot,
    int ResponseBudgetBytes = AssemblyAnalysisResponseLimits.DefaultResponseBytes);

internal sealed record AssemblyFingerprint(
    string CanonicalPath,
    long Length,
    DateTime MtimeUtc,
    string Sha256);

internal sealed record AssemblyDecompilationCacheKey(
    string CanonicalPath,
    string ContentHash,
    string DecompilerVersion,
    string OptionsIdentity,
    string CacheSchemaVersion)
{
    internal string StableValue => string.Join(
        "|",
        CanonicalPath,
        ContentHash,
        DecompilerVersion,
        OptionsIdentity,
        CacheSchemaVersion);
}

internal sealed record AssemblySessionDiagnostic(
    string Code,
    string Message,
    AssemblyDiagnosticSeverity Severity = AssemblyDiagnosticSeverity.Warning);

internal sealed record DecompiledDocument(
    string GeneratedPath,
    string TypeMetadataName,
    string CSharpSource,
    string? MetadataToken = null);

/// <summary>
/// Absolute, physical paths of the materialized WholeProjectDecompiler output.
/// The source root is the deepest common directory of all generated C# documents,
/// so it is safe to pass directly to <c>rg</c> or <c>get_file_tree</c>.
/// </summary>
internal sealed record DecompiledProjectPaths(
    string DecompiledProjectDirectory,
    string DecompiledProjectPath,
    string DecompiledSourceRoot)
{
    internal static DecompiledProjectPaths? Create(
        string? projectFilePath,
        IReadOnlyList<DecompiledDocument> documents)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath) || documents.Count == 0)
        {
            return null;
        }

        try
        {
            var projectPath = Path.GetFullPath(projectFilePath);
            var projectDirectory = Path.GetDirectoryName(projectPath);
            var sourceDirectories = documents
                .Select(document => document.GeneratedPath)
                .Where(Path.IsPathFullyQualified)
                .Select(Path.GetFullPath)
                .Select(Path.GetDirectoryName)
                .Where(directory => !string.IsNullOrWhiteSpace(directory))
                .Select(directory => directory!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (string.IsNullOrWhiteSpace(projectDirectory) || sourceDirectories.Count == 0)
            {
                return null;
            }

            var sourceRoot = FindCommonDirectory(sourceDirectories);
            return sourceRoot is null
                ? null
                : new(projectDirectory, projectPath, sourceRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? FindCommonDirectory(IReadOnlyList<string> directories)
    {
        var common = directories[0];
        foreach (var directory in directories.Skip(1))
        {
            while (!IsSameOrDescendant(common, directory))
            {
                var parent = Directory.GetParent(common);
                if (parent is null) return null;
                common = parent.FullName;
            }
        }

        return common;
    }

    private static bool IsSameOrDescendant(string ancestor, string candidate)
    {
        var relative = Path.GetRelativePath(ancestor, candidate);
        return relative == "."
            || (!Path.IsPathRooted(relative)
                && relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
    }
}

internal sealed record DecompilationRequest(
    string AssemblyPath,
    AssemblyFingerprint Fingerprint,
    AssemblyDecompilationCacheKey CacheKey,
    AssemblyDecompilationOptions Options,
    System.Threading.CancellationToken CancellationToken,
    string? StagingDirectory = null);

internal sealed record DecompilationResult(
    IReadOnlyList<DecompiledDocument> Documents,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics,
    bool IsComplete,
    string? ProjectFilePath = null);

internal sealed record AssemblyCachePublishRequest(
    AssemblyFingerprint Fingerprint,
    AssemblyDecompilationCacheKey CacheKey,
    AssemblyDecompilationOptions Options,
    AssemblyReferenceResolution References,
    DecompilationResult Decompilation,
    AssemblySessionStatus Status,
    string? StagingDirectory = null);

internal sealed record AssemblyWorkspaceRequest(
    string AssemblyPath,
    AssemblyFingerprint Fingerprint,
    IReadOnlyList<DecompiledDocument> Documents,
    IReadOnlyList<MetadataReference> MetadataReferences,
    AssemblySessionStatus Status,
    string? ProjectFilePath = null);

internal sealed record AssemblyCachePublishResult(
    bool Succeeded,
    string? EntryDirectory,
    AssemblySessionDiagnostic? Diagnostic);

internal sealed record AssemblyReferenceResolution(
    AssemblyIdentityDto? Identity,
    IReadOnlyList<AssemblyReferenceDto> References,
    IReadOnlyList<MetadataReference> MetadataReferences,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics);

internal sealed record AssemblyBodyResolution(
    string? Body,
    string BodyAvailability,
    string ContentMode,
    string? Hint = null,
    int TotalBodyLines = 0,
    int DisplayedStartLine = 1,
    int DisplayedEndLine = 0,
    bool HasMoreLines = false);

internal sealed record AssemblyReferenceSession(
    AssemblyReferenceDto Reference,
    string AssemblyPath,
    AssemblyIdentityDto? Identity,
    AssemblyOrigin? Origin,
    string Completeness,
    string SessionStatus,
    IReadOnlyList<string> Diagnostics);

internal sealed record AssemblyReferenceExpansion(
    IReadOnlyList<AssemblyReferenceSession> Sessions,
    IReadOnlyList<string> Diagnostics);

internal sealed record AssemblyRoslynSnapshot(
    Solution Solution,
    ProjectId ProjectId,
    Compilation Compilation,
    IReadOnlyList<Document> Documents,
    IReadOnlyDictionary<DocumentId, AssemblyOrigin> Origins,
    AdhocWorkspace Workspace) : IDisposable
{
    public void Dispose() => Workspace.Dispose();
}

internal sealed record AssemblySessionGeneration(
    long Number,
    AssemblyFingerprint Fingerprint,
    AssemblyDecompilationCacheKey CacheKey,
    AssemblyIdentityDto Identity,
    AssemblySessionStatus Status,
    AssemblyRoslynSnapshot Snapshot,
    IReadOnlyList<AssemblyReferenceDto> References,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics,
    AssemblyOrigin Origin,
    DecompiledProjectPaths? DecompiledProjectPaths = null)
{
    internal int ActiveLeaseCount { get; set; }
}

internal sealed record AssemblySessionState(
    AssemblySessionStatus Status,
    long? CurrentGeneration,
    long? LastGoodGeneration,
    AssemblyFingerprint? Fingerprint,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics,
    DateTime UpdatedUtc);

internal sealed record AssemblyAnalysisHealthSnapshot(
    string TargetPath,
    string LoadState,
    string? OriginKind = null,
    string? ContentHash = null,
    string? GeneratedDocumentPath = null,
    string? Confidence = null,
    long? Generation = null,
    IReadOnlyList<string>? Diagnostics = null,
    string? AnalysisOrigin = null,
    string? DaemonProfile = null,
    string? LockStatus = null,
    string? LeaseStatus = null,
    string? CleanupStatus = null,
    string? ErrorCode = null,
    string? ErrorPhase = null,
    string? ErrorCause = null,
    string? NextAction = null);

internal sealed record AssemblySessionRefreshResult(
    AssemblySessionStatus Status,
    long? Generation,
    bool Reused,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics,
    AssemblySessionFailure? Failure = null);

internal enum AssemblySessionFailureKind
{
    MetadataUnavailable,
    SourceUnavailable,
}

internal sealed record AssemblySessionFailure(
    AssemblySessionFailureKind Kind,
    AssemblySessionDiagnostic Diagnostic);

internal sealed record AssemblyAnalysisSessionOptions(
    string AssemblyPath,
    AssemblyDecompilationOptions? Decompilation = null,
    string? CacheRoot = null,
    long GenerationStart = 0);

internal sealed record CachedDecompilationGeneration(
    AssemblyDecompilationManifest Manifest,
    IReadOnlyList<DecompiledDocument> Documents,
    string? ProjectFilePath = null);

internal sealed record AssemblyCacheReadRequest(
    AssemblyDecompilationCacheKey Key,
    AssemblyFingerprint Fingerprint,
    AssemblyReferenceResolution References);

internal sealed record AssemblyGenerationBuildRequest(
    AssemblyFingerprint Fingerprint,
    AssemblyDecompilationCacheKey Key,
    AssemblyReferenceResolution References,
    IReadOnlyList<DecompiledDocument> Documents,
    AssemblySessionStatus Status,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics,
    AssemblyCachePublishRequest? PublishRequest = null,
    string? ProjectFilePath = null);

internal sealed record AssemblyManifestInput
{
    internal required string CacheKey { get; init; }
    internal required string CanonicalPath { get; init; }
    internal required string OriginalPath { get; init; }
    internal required long Length { get; init; }
    internal required DateTime MtimeUtc { get; init; }
    internal required string Sha256 { get; init; }
}

internal sealed record AssemblyManifestReferences
{
    internal required AssemblyIdentityDto? AssemblyIdentity { get; init; }
    internal required IReadOnlyList<AssemblyReferenceDto> References { get; init; }
}

internal sealed record AssemblyManifestFormat
{
    internal required string DecompilerVersion { get; init; }
    internal required string OptionsIdentity { get; init; }
    internal required string CacheSchemaVersion { get; init; }
    internal required IReadOnlyList<string> GeneratedFiles { get; init; }
    internal required string Encoding { get; init; }
}

internal sealed record AssemblyManifestDiagnostics
{
    internal required IReadOnlyList<string> Warnings { get; init; }
    internal required IReadOnlyList<string> Errors { get; init; }
    internal required IReadOnlyList<string> UnresolvedReferences { get; init; }
}

internal sealed record AssemblyManifestStatus
{
    internal required DateTime CreatedUtc { get; init; }
    internal required DateTime LastAccessUtc { get; init; }
    internal required string Status { get; init; }
    internal required bool Complete { get; init; }
}

internal sealed record AssemblyDecompilationManifest
{
    internal required AssemblyManifestInput Input { get; init; }
    internal required AssemblyManifestReferences References { get; init; }
    internal required AssemblyManifestFormat Format { get; init; }
    internal required AssemblyManifestDiagnostics Diagnostics { get; init; }
    internal required AssemblyManifestStatus Status { get; init; }
}

internal enum AssemblyDiagnosticSeverity
{
    Warning,
    Error,
}

internal static class AssemblyDiagnosticSeverityExtensions
{
    internal const string WarningWireValue = "warning";
    internal const string ErrorWireValue = "error";

    internal static string ToWireValue(this AssemblyDiagnosticSeverity severity) => severity switch
    {
        AssemblyDiagnosticSeverity.Warning => WarningWireValue,
        AssemblyDiagnosticSeverity.Error => ErrorWireValue,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unbekannte Assembly-Diagnoseschwere.")
    };

    internal static bool TryParseWireValue(string? value, out AssemblyDiagnosticSeverity severity)
    {
        if (string.Equals(value, WarningWireValue, StringComparison.OrdinalIgnoreCase))
        {
            severity = AssemblyDiagnosticSeverity.Warning;
            return true;
        }

        if (string.Equals(value, ErrorWireValue, StringComparison.OrdinalIgnoreCase))
        {
            severity = AssemblyDiagnosticSeverity.Error;
            return true;
        }

        severity = default;
        return false;
    }
}
