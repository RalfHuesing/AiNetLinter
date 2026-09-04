#nullable enable

using System.Reflection;

namespace AiNetLinter.Mcp.Composition;

internal static class McpServerVersion
{
    internal static string Get()
    {
        var assembly = typeof(McpServerVersion).Assembly;
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var plusIdx = infoVersion.IndexOf('+');
            return plusIdx > 0 ? infoVersion[..plusIdx] : infoVersion;
        }

        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
    }

    internal static string GetBuildFingerprint() =>
        $"build-{typeof(McpServerVersion).Assembly.ManifestModule.ModuleVersionId:N}";
}
