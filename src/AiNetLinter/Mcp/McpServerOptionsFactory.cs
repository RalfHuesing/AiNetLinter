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
    /// Baut die vollstaendigen Server-Optionen inkl. aller registrierten Tools. Tools erreichen
    /// den resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure — kein
    /// DI-Container (Architektur-Verbot, siehe <c>AiNetLinterRichtlinien.mdc</c> §2). Die
    /// <c>initialize</c>-Handshake-Instructions kommen aus <see cref="ServerInstructions.Text"/>
    /// (Single-Source-of-Truth, siehe dort).
    /// </summary>
    internal static McpServerOptions Create(McpCodeGraphServer mcpState, IServiceProvider? serviceProvider = null)
    {
        return new McpServerOptionsBuilder()
            .WithServerVersion(GetServerVersion())
            .WithServerInstructions(ServerInstructions.Text)
            .WithToolCollection(BuildToolCollection(mcpState, serviceProvider))
            .WithResourceCollection(BuildResourceCollection(mcpState))
            .Build();
    }

    internal static McpServerResourceCollection BuildResourceCollection(McpCodeGraphServer mcpState)
    {
        var resources = new McpServerResourceCollection();
        OverviewResourceRegistration.Register(resources, mcpState);
        return resources;
    }

    internal static McpServerPrimitiveCollection<McpServerTool> BuildToolCollection(
        McpCodeGraphServer mcpState,
        IServiceProvider? serviceProvider = null)
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>();

        SymbolGraphToolRegistrations.Register(tools, mcpState);
        FileStructureToolRegistrations.Register(tools, mcpState);
        AnalysisToolRegistrations.Register(tools, mcpState);
        SymbolBodyToolRegistrations.Register(tools, mcpState);
        ServerMaintenanceToolRegistrations.Register(tools, mcpState, serviceProvider);
        DuplicateDetectionToolRegistrations.Register(tools, mcpState);

        return tools;
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
