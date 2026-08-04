#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert das <c>get_symbol_body</c>-Tool an der von <see cref="McpServerOptionsFactory"/>
/// aufgebauten Tool-Collection. Eigene Registrar-Klasse (statt Erweiterung von
/// <see cref="SymbolGraphToolRegistrations"/>), weil die Symbolgraph-Registrar-Klasse bereits
/// an ihrem 2850-PathOverride haengt und ein zusaetzliches Tool in derselben Klasse das
/// verbleibende Sicherheits-Polster gegen weitere Erweiterungen aufgebraucht haette. Bewusst
/// duenner Dispatch auf <see cref="GetSymbolBodyTool.ExecuteAsync"/>. Kein DI-Container
/// (Architektur-Verbot, siehe <c>AiNetLinterRichtlinien.mdc</c> §2). Optionaler
/// <paramref name="callLog"/> zeichnet den Tool-Aufruf auf, wenn aktiv.
/// </summary>
internal static class SymbolBodyToolRegistrations
{
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog = null)
    {
        AddGetSymbolBody(tools, mcpState, callLog);
    }

    private static void AddGetSymbolBody(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string identifier, int maxBodyLines = 80, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetSymbolBodyTool.ExecuteAsync(mcpState, identifier, maxBodyLines, ct);
                }
                await using var scope = callLog.StartRecording("get_symbol_body", $"{identifier}|{maxBodyLines}");
                var result = await GetSymbolBodyTool.ExecuteAsync(mcpState, identifier, maxBodyLines, ct);
                scope.Complete(result);
                return result;
            },
            new McpServerToolCreateOptions
            {
                Name = "get_symbol_body",
                Description = GetSymbolBodyDescription,
            }));
    }

    private const string GetSymbolBodyDescription =
        "Liefert den Body eines C#-Symbols per stabiler ID (DocumentationCommentId, ueberlebt " +
        "Zeilenverschiebungen, disambiguiert Overloads) oder Datei:Zeile:Spalte bzw. qualifiziertem " +
        "Namen. Hart gekappt bei maxBodyLines (Default 80), mit Ellipse-Indikator und Voll-Laengen-Hinweis. " +
        "Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien.";
}
