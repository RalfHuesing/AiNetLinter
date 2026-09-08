#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp;

internal sealed record AnalysisSymbolIdentity(string ContentHash, long Generation)
{
    internal const string AssemblyPrefix = "assembly:";
    internal const string SourcePrefix = "source:";
    internal const string Prefix = AssemblyPrefix;

    // The cache generation deliberately remains an internal lease detail. It is not part of the
    // serialized handoff ID, so the same path/content snapshot survives eviction and restart.
    internal string CanonicalPath { get; init; } = string.Empty;
    internal bool IsAssembly { get; init; } = true;

    internal string? Format(string? symbolId) =>
        symbolId is null
            ? null
            : $"{(IsAssembly ? AssemblyPrefix : SourcePrefix)}{EncodePath(CanonicalPath)}:{ContentHash}:{symbolId}";

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

    // ainetlinter-disable MaxCyclomaticComplexity — der kanonische Parser validiert bewusst jedes Wire-Format-Feld explizit.
    internal static bool TryParse(
        string value,
        out AnalysisSymbolIdentity? identity,
        out string symbolId)
    {
        identity = null;
        symbolId = string.Empty;
        var prefix = value.StartsWith(AssemblyPrefix, StringComparison.Ordinal)
            ? AssemblyPrefix
            : value.StartsWith(SourcePrefix, StringComparison.Ordinal)
                ? SourcePrefix
                : null;
        if (prefix is null) return false;

        var pathStart = prefix.Length;
        var pathEnd = value.IndexOf(':', pathStart);
        if (pathEnd <= pathStart) return false;
        var hashStart = pathEnd + 1;
        var hashEnd = value.IndexOf(':', hashStart);
        if (hashEnd <= hashStart || hashEnd == value.Length - 1) return false;
        if (!TryDecodePath(value[pathStart..pathEnd], out var canonicalPath)) return false;
        if (string.IsNullOrWhiteSpace(canonicalPath)) return false;

        var hash = value[hashStart..hashEnd];
        symbolId = value[(hashEnd + 1)..];
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character))) return false;
        if (!HasKnownDocumentationCommentIdPrefix(symbolId)
            || symbolId.Contains("#lf:", StringComparison.Ordinal)
            || symbolId.Contains('?', StringComparison.Ordinal)) return false;
        identity = new AnalysisSymbolIdentity(hash, 0)
        {
            CanonicalPath = canonicalPath,
            IsAssembly = prefix == AssemblyPrefix,
        };
        return true;
    }

    private static bool HasKnownDocumentationCommentIdPrefix(string value) =>
        value.StartsWith("M:", StringComparison.Ordinal)
        || value.StartsWith("T:", StringComparison.Ordinal)
        || value.StartsWith("P:", StringComparison.Ordinal)
        || value.StartsWith("F:", StringComparison.Ordinal)
        || value.StartsWith("E:", StringComparison.Ordinal)
        || value.StartsWith("!:", StringComparison.Ordinal);

    private static string EncodePath(string path) =>
        string.IsNullOrEmpty(path)
            ? "-"
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(path))
                .TrimEnd('=')
                .Replace('+', '-').Replace('/', '_');

    private static bool TryDecodePath(string value, out string path)
    {
        path = string.Empty;
        if (value == "-") return true;
        try
        {
            var padded = value.Replace('-', '+').Replace('_', '/') +
                         new string('=', (4 - value.Length % 4) % 4);
            path = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return !string.IsNullOrEmpty(path);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void Append(IncrementalHash hash, string value) =>
        hash.AppendData(Encoding.UTF8.GetBytes(value));
}
