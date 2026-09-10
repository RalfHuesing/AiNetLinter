#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AiNetLinter.Mcp.Handoffs;

internal static class SymbolHandoffToken
{
    internal const int TokenBytes = 16;
    internal const int EncodedLength = 22;

    internal static bool TryCreateTarget(string canonicalPath, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(canonicalPath) || !Path.IsPathFullyQualified(canonicalPath)) return false;

        string normalizedPath;
        try
        {
            normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(canonicalPath));
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (string.IsNullOrEmpty(normalizedPath)) return false;

        token = Encode128(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)));
        return true;
    }

    internal static bool TryCreateContent(string contentHash, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(contentHash) || contentHash.Length != 64) return false;

        byte[] hash;
        try
        {
            hash = Convert.FromHexString(contentHash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (hash.Length != 32) return false;
        token = Encode128(hash);
        return true;
    }

    internal static bool IsValid(string token)
    {
        if (token.Length != EncodedLength) return false;
        foreach (var character in token)
        {
            if (character is not (>= 'A' and <= 'Z')
                and not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9')
                and not ('-' or '_'))
            {
                return false;
            }
        }

        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/') + "==";
            var decoded = Convert.FromBase64String(padded);
            return decoded.Length == TokenBytes && string.Equals(Encode128(decoded), token, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Encode128(ReadOnlySpan<byte> hash) =>
        Convert.ToBase64String(hash[..TokenBytes])
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
