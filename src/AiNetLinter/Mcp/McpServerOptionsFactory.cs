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

    // Zentraler Scope-Hint fuer den initialize-Handshake (EPIC-05 / 003).
    // Wird via ModelContextProtocol-SDK-Property McpServerOptions.ServerInstructions
    // an den Server-Info-Block der initialize-Antwort durchgereicht. Nennt die
    // C#-only-Grenze einmal server-weit, damit der Agent sie nicht pro Tool-
    // Description zusammensuchen muss. Verweist auf search_pattern als Fallback
    // fuer Namen in Nicht-C#-Dateien (.js, .razor, .xaml, .html, .css).
    private const string ServerInstructions =
        "Symbolgraph-Tools (find_symbol, find_references, get_impact, get_type_hierarchy, " +
        "get_file_skeleton, get_violations) arbeiten ausschliesslich auf C#/.cs-Quellcode. " +
        "Fuer Namen, die nur in .js, .razor, .cshtml, .xaml, .html oder .css vorkommen, " +
        "ist search_pattern der passende Fallback. Struktur-Tools ohne C#-Beschraenkung: " +
        "get_index_scope, get_hotspots.";

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
            ServerInstructions = ServerInstructions,
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
