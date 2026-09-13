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
    private const string InternalPrefix = "i:";
    private const char SourceOriginCode = '0';
    private const char AssemblyOriginCode = '1';

    internal string Format() =>
        $"{InternalPrefix}{(Origin == SymbolHandoffOrigin.Source ? SourceOriginCode : AssemblyOriginCode)}:{TargetToken}:{ContentToken}:{DocumentationCommentId}";

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
        if (!HasValidEnvelope(value) || !TryGetOrigin(value[2], out var origin)) return false;

        return TryParseComponents(value, origin, out identifier);
    }

    private static bool TryParseComponents(
        string value,
        SymbolHandoffOrigin origin,
        out SymbolHandoffIdentifier identifier)
    {
        identifier = default;
        const int targetStart = 4;
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

        identifier = new(origin, targetToken, contentToken, documentationCommentId);
        return true;
    }

    internal static bool IsInternalIdentifier(string value) =>
        value.StartsWith(InternalPrefix, StringComparison.Ordinal);

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

    private static bool HasValidEnvelope(string value) =>
        !string.IsNullOrEmpty(value)
        && !ContainsWhitespace(value)
        && value.StartsWith(InternalPrefix, StringComparison.Ordinal)
        && value.Length >= 5
        && value[3] == ':';

    private static bool TryGetOrigin(char value, out SymbolHandoffOrigin origin)
    {
        origin = value switch
        {
            SourceOriginCode => SymbolHandoffOrigin.Source,
            AssemblyOriginCode => SymbolHandoffOrigin.Assembly,
            _ => default,
        };
        return value is SourceOriginCode or AssemblyOriginCode;
    }

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
