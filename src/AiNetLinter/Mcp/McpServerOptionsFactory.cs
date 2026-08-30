#nullable enable

using System.Reflection;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Baut die <see cref="McpServerOptions"/> inkl. der registrierten Tool-Collection.
/// Bewusst aus <see cref="AiNetLinter.Commands.McpServerCommand"/> ausgelagert
/// und durch <see cref="McpServerOptionsBuilder"/> in eine schlanke Factory + Builder
/// aufgeteilt: ohne diese Auslagerung waechst der AIContextFootprint von
/// <see cref="McpCodeGraphServer"/> durch die Tool-Registrierungs-Abhaengigkeiten
/// ueber das projektweite Limit (siehe <c>AiNetLinter.mdc</c>).
/// </summary>
internal static class McpServerOptionsFactory
{
    internal const string ServerName = "ainetlinter";

    /// <summary>
    /// Baut die Server-Optionen aus bereits komponierten Collections. Die Tool- und
    /// Resource-Komposition liegt in den fachlich getrennten Collection-Factories;
    /// dadurch bleibt diese Factory auf das Optionsformat beschraenkt.
    /// </summary>
    internal static McpServerOptions Create(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpServerResourceCollection resources)
    {
        return new McpServerOptionsBuilder()
            .WithServerVersion(GetServerVersion())
            .WithServerInstructions(ServerInstructions.Text)
            .WithToolCollection(tools)
            .WithResourceCollection(resources)
            .Build();
    }

    internal static string GetServerVersion()
    {
        var assembly = typeof(McpServerOptionsFactory).Assembly;
        var infoVersion = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly)?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var plusIdx = infoVersion.IndexOf('+');
            return plusIdx > 0 ? infoVersion[..plusIdx] : infoVersion;
        }
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
    }
}
