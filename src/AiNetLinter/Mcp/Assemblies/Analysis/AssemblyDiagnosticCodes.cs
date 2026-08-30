#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal static class AssemblyDiagnosticCodes
{
    internal const string EmptyEventAccessor = "CS0073";
    internal const string EmptyMemberBody = "CS0501";

    private static readonly ImmutableDictionary<string, string> Values = new Dictionary<string, string>
    {
        [Key(nameof(AssemblyAnalysisSession), nameof(AssemblyAnalysisSession.RefreshAsync))] = "assembly-refresh-cancelled",
        [Key(nameof(AssemblyAnalysisSession), nameof(AssemblyAnalysisSession.Dispose))] = "assembly-session-disposed",
        [Key(nameof(AssemblyAnalysisSession), nameof(AssemblyFingerprint.Length))] = "assembly-size-limit",
        [Key(nameof(AssemblyAnalysisSession), nameof(DecompilationResult.Documents))] = "assembly-decompilation-empty",
        [Key(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblySessionStatus.Loading))] = "assembly-workspace-cancelled",
        [Key(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblySessionStatus.Failed))] = "assembly-workspace-failed",
        [Key(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblyRoslynSnapshot.Compilation))] = "assembly-workspace-compilation-failed",
        [Key(nameof(AssemblyRoslynWorkspaceFactory), nameof(AssemblyRoslynSnapshot.Solution))] = "assembly-compilation-partial",
        [Key(nameof(AssemblyAnalysisSession), nameof(AssemblySessionRefreshResult.Diagnostics))] = "assembly-refresh-failed",
        [Key(nameof(AssemblyAnalysisSessionOptions), nameof(AssemblyAnalysisSessionOptions.CacheRoot))] = "assembly-options-invalid",
        [Key(nameof(AssemblyDecompilationManifest), nameof(AssemblyDecompilationManifest.Diagnostics))] = "assembly-cache-warning",
        [Key(nameof(AssemblyDecompilationManifest), nameof(AssemblyDecompilationManifest.Status))] = "assembly-cache-error",
        [Key(nameof(AssemblyDecompilationAdapter), nameof(OperationCanceledException))] = "assembly-decompilation-cancelled",
        [Key(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions))] = "assembly-decompilation-failed",
        [Key(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.CSharpSource))] = "assembly-type-decompilation-empty",
        [Key(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions.MaxDocumentCharacters))] = "assembly-document-size-limit",
        [Key(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.GeneratedPath))] = "assembly-type-decompilation-failed",
        [Key(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions.MaxTypes))] = "assembly-type-limit",
        [Key(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions.MaxMembers))] = "assembly-member-limit",
        [Key(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions.MaxComplexity))] = "assembly-complexity-limit",
        [Key(nameof(AssemblyDecompilationCache), nameof(AssemblyCacheReadRequest))] = "assembly-cache-invalid",
        [Key(nameof(AssemblyDecompilationCache), nameof(AssemblyCachePublishRequest))] = "assembly-cache-publish-failed",
        [Key(nameof(AssemblyDecompilationCache), nameof(AssemblyCacheContract.CurrentPointerFileName))] = "assembly-cache-pointer-race",
        [Key(nameof(AssemblyFingerprintCalculator), nameof(AssemblyFingerprintCalculator.Canonicalize))] = "assembly-path-missing",
        [Key(nameof(AssemblyFingerprintCalculator), nameof(AssemblyFingerprintCalculator.TryCreate))] = "assembly-fingerprint-failed",
        [Key(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceResolver.Resolve))] = "assembly-metadata-missing",
        [Key(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceResolution.MetadataReferences))] = "assembly-reference-metadata-failed",
        [Key(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceResolution.Identity))] = "assembly-metadata-read-failed",
        [Key(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceDto.Resolved))] = "assembly-reference-unresolved",
        [Key(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceDto.Version))] = "assembly-reference-identity-mismatch",
        [Key(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceDto.Name))] = "assembly-reference-enumeration-failed",
        [Key(nameof(AssemblyReferenceResolver), nameof(Microsoft.CodeAnalysis.MetadataReference))] = "assembly-reference-invalid",
        [Key(nameof(AssemblyReferenceResolver), nameof(AssemblyIdentityDto))] = "assembly-reference-candidate-invalid",
    }.ToImmutableDictionary(StringComparer.Ordinal);

    internal static string For(string owner, string member) => Values[Key(owner, member)];

    internal static bool IsExpectedDeclarationOnlyDiagnostic(string id) => id is EmptyEventAccessor or EmptyMemberBody;

    private static string Key(string owner, string member) => owner + "." + member;
}
