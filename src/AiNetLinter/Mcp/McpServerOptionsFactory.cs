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
    // Zentraler Scope-Hint fuer den initialize-Handshake.
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
    /// Baut die vollstaendigen Server-Optionen inkl. aller registrierten Tools. Tools erreichen
    /// den resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure — kein
    /// DI-Container (Architektur-Verbot, siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static McpServerOptions Create(McpCodeGraphServer mcpState)
    {
        return new McpServerOptionsBuilder()
            .WithServerVersion(GetServerVersion())
            .WithServerInstructions(ServerInstructions)
            .WithToolCollection(BuildToolCollection(mcpState))
            .Build();
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
