#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert das <c>get_symbol_body</c>-Tool an der von <see cref="McpServerOptionsFactory"/>
/// aufgebauten Tool-Collection. Eigene Registrar-Klasse (statt Erweiterung von
/// <see cref="SymbolGraphToolRegistrations"/>), weil die Symbolgraph-Registrar-Klasse bereits
/// an ihrem 2850-PathOverride haengt und ein zusaetzliches Tool in derselben Klasse das
/// verbleibende Sicherheits-Polster gegen weitere Erweiterungen aufgebraucht haette. Bewusst
/// duenner Dispatch auf <see cref="GetSymbolBodyTool.ExecuteAsync"/> ueber den
/// projektgebundenen Lease-Weg (<see cref="ProjectToolCall"/>). Kein DI-Container
/// (Architektur-Verbot, siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
/// </summary>
internal static class SymbolBodyToolRegistrations
{
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        AddGetSymbolBody(tools, registry);
    }

    private static void AddGetSymbolBody(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string projectRoot, string? symbolIdentifier = null, string[]? symbolIdentifiers = null, int maxBodyLines = 80, CancellationToken ct = default) =>
                await ProjectToolCall.ExecuteAsync(
                    registry,
                    projectRoot,
                    lease => GetSymbolBodyTool.ExecuteAsync(lease.Server, symbolIdentifier, symbolIdentifiers, maxBodyLines, ct)),
            new McpServerToolCreateOptions
            {
                Name = "get_symbol_body",
                Description = GetSymbolBodyDescription,
            }));
    }

    private const string GetSymbolBodyDescription =
        "Wann nutzen: Source-Body eines oder mehrerer C#-Symbole lesen (Batch-Support fuer 1 Turn). " +
        "symbolIdentifier (einzeln) ODER symbolIdentifiers (Array fuer Batch): \"M:Namespace.Klasse.Methode\" " +
        "oder \"Datei.cs:42:10\" oder \"Datei.cs:42\" oder \"Klasse.Methode\". " +
        "Hart gekappt bei maxBodyLines je Symbol (Default 80).";
}
