#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies;

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
    string Confidence)
{
    internal string Kind => OriginKind;
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
    internal const string CurrentCacheSchemaVersion = "assembly-cache-v1";

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

internal sealed record AssemblyDecompilationManifest
{
    public required string CacheKey { get; init; }
    public required string CanonicalPath { get; init; }
    public required string OriginalPath { get; init; }
    public required long Length { get; init; }
    public required DateTime MtimeUtc { get; init; }
    public required string Sha256 { get; init; }
    public AssemblyIdentityDto? AssemblyIdentity { get; init; }
    public IReadOnlyList<AssemblyReferenceDto> References { get; init; } = [];
    public required string DecompilerVersion { get; init; }
    public required string OptionsIdentity { get; init; }
    public required string CacheSchemaVersion { get; init; }
    public IReadOnlyList<string> GeneratedFiles { get; init; } = [];
    public string Encoding { get; init; } = "utf-8";
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> UnresolvedReferences { get; init; } = [];
    public DateTime CreatedUtc { get; init; }
    public DateTime LastAccessUtc { get; init; }
    public required string Status { get; init; }
    public bool Complete { get; init; }
}
