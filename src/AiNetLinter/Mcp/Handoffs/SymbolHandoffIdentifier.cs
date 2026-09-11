#nullable enable

using System;

namespace AiNetLinter.Mcp.Handoffs;

internal enum SymbolHandoffOrigin
{
    Source,
    Assembly,
}

internal readonly record struct SymbolHandoffIdentifier(
    SymbolHandoffOrigin Origin,
    string TargetToken,
    string ContentToken,
    string DocumentationCommentId)
{
    internal const int TokenBytes = SymbolHandoffToken.TokenBytes;
    internal const int EncodedTokenLength = SymbolHandoffToken.EncodedLength;
    private const string SourcePrefix = "s:";
    private const string AssemblyPrefix = "a:";

    internal string Format() =>
        $"{(Origin == SymbolHandoffOrigin.Source ? SourcePrefix : AssemblyPrefix)}{TargetToken}:{ContentToken}:{DocumentationCommentId}";

    internal static bool TryCreate(
        SymbolHandoffCreationRequest request,
        out SymbolHandoffIdentifier identifier)
    {
        identifier = default;
        if (!IsCanonicalDocumentationCommentId(request.DocumentationCommentId)
            || !SymbolHandoffToken.TryCreateTarget(request.CanonicalPath, out var targetToken)
            || !SymbolHandoffToken.TryCreateContent(request.ContentHash, out var contentToken))
        {
            return false;
        }

        identifier = new(request.Origin, targetToken, contentToken, request.DocumentationCommentId);
        return true;
    }

    internal static bool TryParse(string value, out SymbolHandoffIdentifier identifier)
    {
        identifier = default;
        if (string.IsNullOrEmpty(value) || ContainsWhitespace(value)) return false;

        var origin = value.StartsWith(SourcePrefix, StringComparison.Ordinal)
            ? SymbolHandoffOrigin.Source
            : value.StartsWith(AssemblyPrefix, StringComparison.Ordinal)
                ? SymbolHandoffOrigin.Assembly
                : (SymbolHandoffOrigin?)null;
        if (origin is null) return false;

        var targetStart = 2;
        var targetEnd = value.IndexOf(':', targetStart);
        if (targetEnd <= targetStart) return false;
        var contentStart = targetEnd + 1;
        var contentEnd = value.IndexOf(':', contentStart);
        if (contentEnd <= contentStart || contentEnd == value.Length - 1) return false;

        var targetToken = value[targetStart..targetEnd];
        var contentToken = value[contentStart..contentEnd];
        var documentationCommentId = value[(contentEnd + 1)..];
        if (!SymbolHandoffToken.IsValid(targetToken)
            || !SymbolHandoffToken.IsValid(contentToken)
            || !IsCanonicalDocumentationCommentId(documentationCommentId))
        {
            return false;
        }

        identifier = new(origin.Value, targetToken, contentToken, documentationCommentId);
        return true;
    }

    internal static bool HasWirePrefix(string value) =>
        value.StartsWith(SourcePrefix, StringComparison.Ordinal)
        || value.StartsWith(AssemblyPrefix, StringComparison.Ordinal);

    internal static bool HasUnsupportedPrefix(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        var separator = value.IndexOf(':');
        if (separator <= 0) return false;

        var prefix = value[..separator];
        if (prefix.Equals("source", StringComparison.Ordinal)
            || prefix.Equals("assembly", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var character in prefix)
        {
            if (character is < 'a' or > 'z') return false;
        }

        // Ein einbuchstabiger Windows-Laufwerksbuchstabe ist ein Positionspfad, kein Handoff.
        return prefix.Length != 1
            || value.Length <= separator + 1
            || (value[separator + 1] is not '\\' and not '/');
    }

    internal static bool IsCanonicalDocumentationCommentId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 3 || value[1] != ':') return false;
        if (value.Contains("#lf:", StringComparison.Ordinal)
            || value.Contains('?', StringComparison.Ordinal)
            || ContainsWhitespace(value)) return false;
        if (value[2..].Length == 0) return false;

        return value[0] is 'M' or 'T' or 'P' or 'F' or 'E' or '!';
    }

    internal static string ForError(string value) =>
        value.Length <= 80 ? value : value[..80] + "…";

    private static bool ContainsWhitespace(string value)
    {
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character)) return true;
        }

        return false;
    }
}

internal sealed record SymbolHandoffCreationRequest(
    SymbolHandoffOrigin Origin,
    string CanonicalPath,
    string ContentHash,
    string DocumentationCommentId);
