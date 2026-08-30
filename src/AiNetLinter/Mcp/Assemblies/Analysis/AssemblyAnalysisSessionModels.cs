#nullable enable

using System;
using System.Collections.Generic;
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
    string Confidence,
    SourceSnapshotIdentity? SourceSnapshotIdentity = null,
    string? SourceProjectPath = null)
{
    internal string Kind => OriginKind;

    internal bool IsDecompiled => string.Equals(OriginKind, "decompiled", StringComparison.Ordinal);
}

internal sealed record AssemblyDecompilationOptions(
    int MaxAssemblyBytes = 64 * 1024 * 1024,
    int MaxTypes = 2_000,
    int MaxMembers = 20_000,
    int MaxDocumentCharacters = 2_000_000,
    int MaxComplexity = 50_000,
    TimeSpan Timeout = default,
    string DecompilerVersion = AssemblyDecompilationOptions.CurrentDecompilerVersion,
    string CacheSchemaVersion = AssemblyDecompilationOptions.CurrentCacheSchemaVersion)
{
    internal const string CurrentDecompilerVersion = "10.0.1.8346";
    internal const string CurrentCacheSchemaVersion = AssemblyCacheContract.CacheSchemaVersion;

    internal static AssemblyDecompilationOptions Default => new();

    internal TimeSpan EffectiveTimeout => Timeout > TimeSpan.Zero
        ? Timeout
        : TimeSpan.FromSeconds(30);

    internal string Identity => string.Join(
        "|",
        MaxAssemblyBytes,
        MaxTypes,
        MaxMembers,
        MaxDocumentCharacters,
        MaxComplexity,
        EffectiveTimeout.Ticks,
        DecompilerVersion,
        CacheSchemaVersion);
}

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
    string Severity = "warning");

internal sealed record DecompiledDocument(
    string GeneratedPath,
    string TypeMetadataName,
    string CSharpSource,
    string? MetadataToken = null);

internal sealed record DecompilationRequest(
    string AssemblyPath,
    AssemblyFingerprint Fingerprint,
    AssemblyDecompilationCacheKey CacheKey,
    AssemblyDecompilationOptions Options,
    System.Threading.CancellationToken CancellationToken);

internal sealed record DecompilationResult(
    IReadOnlyList<DecompiledDocument> Documents,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics,
    bool IsComplete);

internal sealed record AssemblyCachePublishRequest(
    AssemblyFingerprint Fingerprint,
    AssemblyDecompilationCacheKey CacheKey,
    AssemblyDecompilationOptions Options,
    AssemblyReferenceResolution References,
    DecompilationResult Decompilation,
    AssemblySessionStatus Status);

internal sealed record AssemblyWorkspaceRequest(
    string AssemblyPath,
    AssemblyFingerprint Fingerprint,
    IReadOnlyList<DecompiledDocument> Documents,
    IReadOnlyList<MetadataReference> MetadataReferences,
    AssemblySessionStatus Status);

internal sealed record AssemblyCachePublishResult(
    bool Succeeded,
    string? EntryDirectory,
    AssemblySessionDiagnostic? Diagnostic);

internal sealed record AssemblyReferenceResolution(
    AssemblyIdentityDto? Identity,
    IReadOnlyList<AssemblyReferenceDto> References,
    IReadOnlyList<MetadataReference> MetadataReferences,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics,
    ICSharpCode.Decompiler.Metadata.IAssemblyResolver DecompilerResolver);

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
    AssemblyOrigin Origin);

internal sealed record AssemblySessionState(
    AssemblySessionStatus Status,
    long? CurrentGeneration,
    long? LastGoodGeneration,
    AssemblyFingerprint? Fingerprint,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics,
    DateTime UpdatedUtc);

internal sealed record AssemblySessionRefreshResult(
    AssemblySessionStatus Status,
    long? Generation,
    bool Reused,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics);

internal sealed record AssemblyAnalysisSessionOptions(
    string AssemblyPath,
    AssemblyDecompilationOptions? Decompilation = null,
    string? CacheRoot = null);

internal sealed record CachedDecompilationGeneration(
    AssemblyDecompilationManifest Manifest,
    IReadOnlyList<DecompiledDocument> Documents);

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
    AssemblyCachePublishRequest? PublishRequest = null);

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
