#nullable enable

using System.Reflection;

namespace AiNetLinter.Mcp.Composition;

/// <summary>
/// Ermittelt die bereinigte Server-Version fuer MCP-Handshake und Health-Payloads.
/// Bewusst als eigenstaendige schlanke Hilfsklasse ausgelagert, um den AIContextFootprint
/// von abhaengigen Klassen klein zu halten.
/// </summary>
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
}
