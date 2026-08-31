#nullable enable

using System;
using System.IO;
using System.Linq;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal static class AssemblyCacheContract
{
    internal const string ManifestFileName = "manifest.json";
    internal const string CurrentPointerFileName = "current.json";
    internal const string SourceDirectoryName = "source";
    internal const string GenerationDirectoryPrefix = "generation-";
    internal const string Utf8EncodingName = "utf-8";
    internal const string CacheSchemaVersion = "assembly-cache-v2";
    internal const string SyntheticProjectName = "decompiled-assembly";
    internal const int FileBufferSize = 4096;
    internal const string DefaultCacheDirectoryName = "cache";
    internal const string DefaultAssemblyCacheDirectoryName = "assembly";
    internal const int MaxRetainedGenerations = 2;

    internal static bool IsSafeGenerationName(string? value) =>
        value is not null
        && value.Length == GenerationDirectoryPrefix.Length + 32
        && value.StartsWith(GenerationDirectoryPrefix, StringComparison.Ordinal)
        && value[GenerationDirectoryPrefix.Length..].All(IsLowerHexDigit);

    private static bool IsLowerHexDigit(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f';

    internal static string ResolveRootPath(string? configuredRoot) =>
        Path.GetFullPath(configuredRoot ?? Path.Combine(
            AppContext.BaseDirectory,
            DefaultCacheDirectoryName,
            DefaultAssemblyCacheDirectoryName));
}
