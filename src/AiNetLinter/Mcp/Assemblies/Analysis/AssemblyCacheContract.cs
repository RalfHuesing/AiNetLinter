#nullable enable

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
}
