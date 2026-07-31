#nullable enable

using System.Reflection;
using System.Threading;
using AiNetLinter.Mcp.Tools;
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

        tools.Add(McpServerTool.Create(
            (string namePattern, string? kind = null, CancellationToken ct = default) =>
                FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, ct),
            new McpServerToolCreateOptions
            {
                Name = "find_symbol",
                Description = "Sucht C#-Symbole (Klassen, Methoden, Properties, Interfaces) per " +
                    "Substring im Namen. Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien.",
            }));

        tools.Add(McpServerTool.Create(
            (string symbolIdentifier, CancellationToken ct = default) =>
                FindReferencesTool.ExecuteAsync(mcpState, symbolIdentifier, ct),
            new McpServerToolCreateOptions
            {
                Name = "find_references",
                Description = "Findet alle Aufrufstellen eines C#-Symbols (Datei:Zeile:Spalte " +
                    "oder qualifizierter/teil-qualifizierter Name). Deckt nur .cs-Dateien ab, " +
                    "keine .js/.razor/.xaml/.html/.css-Dateien.",
            }));

        return tools;
    }

    private static string GetServerVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }
}
