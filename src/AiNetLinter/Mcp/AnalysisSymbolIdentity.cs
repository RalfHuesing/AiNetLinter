#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using AiNetLinter.Mcp.Handoffs;

namespace AiNetLinter.Mcp;

internal sealed record AnalysisSymbolIdentity(string ContentHash, long Generation)
{
    // The cache generation deliberately remains an internal lease detail. It is not part of the
    // serialized handoff ID, so the same path/content snapshot survives eviction and restart.
    internal string CanonicalPath { get; init; } = string.Empty;
    internal bool IsAssembly { get; init; } = true;

    internal string? Format(string? symbolId) =>
        symbolId is not null
        && SymbolHandoffIdentifier.TryCreate(
            new SymbolHandoffCreationRequest(
                IsAssembly ? SymbolHandoffOrigin.Assembly : SymbolHandoffOrigin.Source,
                CanonicalPath,
                ContentHash,
                symbolId),
            out var identifier)
            ? identifier.Format()
            : null;

    internal string? FormatHandoff(ISymbol symbol)
    {
        var declarationId = DocumentationCommentId.CreateDeclarationId(symbol);
        return IsCanonicalHandoffSymbol(symbol, declarationId)
            ? Format(declarationId)
            : null;
    }

    internal static bool IsCanonicalHandoffSymbol(ISymbol symbol, string? declarationId = null) =>
        symbol is not IMethodSymbol { MethodKind: MethodKind.LocalFunction }
        && !string.IsNullOrWhiteSpace(declarationId)
        && HasKnownDocumentationCommentIdPrefix(declarationId!);

    internal bool Matches(AnalysisSymbolIdentity other) =>
        IsAssembly == other.IsAssembly
        && (string.IsNullOrEmpty(CanonicalPath)
            || string.IsNullOrEmpty(other.CanonicalPath)
            || string.Equals(CanonicalPath, other.CanonicalPath, StringComparison.OrdinalIgnoreCase))
        && string.Equals(ContentHash, other.ContentHash, StringComparison.OrdinalIgnoreCase);

    internal static AnalysisSymbolIdentity ForAssembly(string canonicalPath, string contentHash, long generation) =>
        new(contentHash, generation)
        {
            CanonicalPath = canonicalPath,
            IsAssembly = true,
        };

    internal static AnalysisSymbolIdentity ForSource(string canonicalPath, string snapshotHash) =>
        new(snapshotHash, 0)
        {
            CanonicalPath = canonicalPath,
            IsAssembly = false,
        };

    internal static string CreateSourceSnapshotHash(
        string canonicalPath,
        IReadOnlyDictionary<string, McpFileState> fileState)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, canonicalPath);
        foreach (var file in fileState.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Append(hash, file.Key);
            Append(hash, file.Value.Hash);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    internal static bool HasKnownDocumentationCommentIdPrefix(string value) =>
        SymbolHandoffIdentifier.IsCanonicalDocumentationCommentId(value);

    private static void Append(IncrementalHash hash, string value) =>
        hash.AppendData(Encoding.UTF8.GetBytes(value));
}
