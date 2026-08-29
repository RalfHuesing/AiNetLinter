#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryCacheContract
{
    internal const string CacheSchemaVersion = "external-source-repository-cache-v1";
    internal const string ContentDirectoryName = "content";
    internal const string ManifestFileName = "manifest.json";
    internal const string InventoryFileName = "inventory.json";
    internal const string CurrentPointerFileName = "current";
    internal const string GenerationDirectoryPrefix = "generation-";
    internal const string PointerGenerationPropertyName = "generation";
    internal const string PublishFailedDiagnosticCode = "external-source-repository-cache-publish-failed";
    internal const string PublishCancelledDiagnosticCode = "external-source-repository-cache-publish-cancelled";
    internal const int FileBufferSize = 81920;
    internal const int MaxRepositoryUrlLength = 2048;
    internal const int MaxSchemaVersionLength = 128;
    internal const int MaxPointerJsonBytes = 4096;
    internal const int MaxManifestJsonBytes = 16 * 1024 * 1024;
    internal const int MaxInventoryJsonBytes = 16 * 1024 * 1024;
    internal const int MaxPointerJsonDepth = 8;
    internal const int MaxManifestJsonDepth = 16;
    internal const int MaxInventoryJsonDepth = 16;
    internal const int MaxRelativePathLength = 1024;
    internal const int MaxRevisionLength = 128;
    internal const int MaxInventoryEntries = 10000;
    internal const long MaxFileLength = 256L * 1024 * 1024;
    internal const long MaxInventoryBytes = 1024L * 1024 * 1024;
    internal const int GenerationIdentifierLength = 32;

    internal static string CreateStableValue(
        string schemaVersion,
        string canonicalRepositoryUrl,
        string solutionPath)
    {
        var identity = string.Join(
            "\n",
            schemaVersion,
            canonicalRepositoryUrl,
            solutionPath);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
    }

    internal static bool TryNormalizeRelativeFilePath(
        string value,
        out string? normalizedPath)
    {
        normalizedPath = null;
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaxRelativePathLength)
        {
            return false;
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = value.Replace('\\', '/');
        if (candidate.StartsWith("/", StringComparison.Ordinal)
            || Path.IsPathFullyQualified(candidate)
            || candidate.IndexOf(':') >= 0)
        {
            return false;
        }

        var segments = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment is "." or ".."
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }
        }

        normalizedPath = string.Join('/', segments);
        return true;
    }

    internal static string? TryCanonicalizeAbsoluteRoot(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Path.IsPathFullyQualified(value.Trim()))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(value.Trim());
            var pathRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(pathRoot))
            {
                return null;
            }

            return string.Equals(fullPath, pathRoot, StringComparison.OrdinalIgnoreCase)
                ? pathRoot
                : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            return null;
        }
    }

    internal static bool IsSafeGenerationName(string value)
    {
        if (value.Length != GenerationDirectoryPrefix.Length + GenerationIdentifierLength
            || !value.StartsWith(GenerationDirectoryPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = GenerationDirectoryPrefix.Length; index < value.Length; index++)
        {
            if (!IsLowerHexDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsLowerHexDigit(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f';
}
