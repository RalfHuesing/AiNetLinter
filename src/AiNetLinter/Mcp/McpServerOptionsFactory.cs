#nullable enable

using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Baut die <see cref="McpServerOptions"/> inkl. der registrierten Tool-Collection. Bewusst aus
/// <see cref="AiNetLinter.Commands.McpServerCommand"/> ausgelagert: haette
/// <see cref="McpCodeGraphServer"/> als Parametertyp eines eigenen Members, waechst dessen
/// AIContextFootprint (siehe <c>AiNetLinter.mdc</c>) durch die Tool-Registrierungs-Abhaengigkeiten
/// (<see cref="McpCodeGraphServer"/>, <see cref="FindSymbolTool"/>, ...) ueber das Limit.
/// </summary>
internal static class McpServerOptionsFactory
{
    private const string ServerName = "ainetlinter";

    /// <summary>
    /// Baut die vollstaendigen Server-Optionen inkl. aller registrierten Tools. Tools erreichen den
    /// resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure — kein DI-Container
    /// (siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static McpServerOptions Create(McpCodeGraphServer mcpState)
    {
        return new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = ServerName,
                Version = GetServerVersion(),
            },
            ToolCollection = BuildToolCollection(mcpState),
        };
    }

    private static McpServerPrimitiveCollection<McpServerTool> BuildToolCollection(McpCodeGraphServer mcpState)
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>();

        SymbolGraphToolRegistrations.Register(tools, mcpState);
        FileStructureToolRegistrations.Register(tools, mcpState);
        AnalysisToolRegistrations.Register(tools, mcpState);

        return tools;
    }

    private static string GetServerVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }
}
