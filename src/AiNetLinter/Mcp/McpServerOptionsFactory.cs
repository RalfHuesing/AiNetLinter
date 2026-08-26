#nullable enable

using System.Reflection;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Registration;
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
    /// Baut die vollstaendigen Server-Optionen inkl. aller registrierten Tools. Die Lambdas
    /// erreichen die residenten Instanzen ausschliesslich ueber die <paramref name="registry"/>
    /// (Lease je projectRoot) — kein DI-Container (Architektur-Verbot, siehe
    /// <c>AiNetLinterRichtlinien.mdc</c> §2). Die <c>initialize</c>-Handshake-Instructions
    /// kommen aus <see cref="ServerInstructions.Text"/> (Single-Source-of-Truth, siehe dort).
    /// </summary>
    internal static McpServerOptions Create(
        ProjectRegistry registry,
        Daemon.DaemonRuntimeContext? runtimeContext = null)
    {
        return new McpServerOptionsBuilder()
            .WithServerVersion(GetServerVersion())
            .WithServerInstructions(ServerInstructions.Text)
            .WithToolCollection(BuildToolCollection(registry, runtimeContext))
            .WithResourceCollection(BuildResourceCollection(registry))
            .Build();
    }

    internal static McpServerResourceCollection BuildResourceCollection(ProjectRegistry registry)
    {
        var resources = new McpServerResourceCollection();
        McpAgentGuideRegistration.Register(resources);
        OverviewResourceRegistration.Register(resources, registry);
        return resources;
    }

    internal static McpServerPrimitiveCollection<McpServerTool> BuildToolCollection(
        ProjectRegistry registry,
        Daemon.DaemonRuntimeContext? runtimeContext = null)
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>();

        SymbolGraphToolRegistrations.Register(tools, registry);
        FileStructureToolRegistrations.Register(tools, registry);
        AnalysisToolRegistrations.Register(tools, registry);
        SymbolBodyToolRegistrations.Register(tools, registry);
        ServerMaintenanceToolRegistrations.Register(tools, registry, runtimeContext);
        DuplicateDetectionToolRegistrations.Register(tools, registry);

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
