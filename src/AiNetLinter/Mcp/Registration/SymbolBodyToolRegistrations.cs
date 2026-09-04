#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Registriert das <c>get_symbol_body</c>-Tool an der von <see cref="McpServerOptionsFactory"/>
/// aufgebauten Tool-Collection. Eigene Registrar-Klasse (statt Erweiterung von
/// <see cref="SymbolGraphToolRegistrations"/>), weil die Symbolgraph-Registrar-Klasse bereits
/// an ihrem 2850-PathOverride haengt und ein zusaetzliches Tool in derselben Klasse das
/// verbleibende Sicherheits-Polster gegen weitere Erweiterungen aufgebraucht haette. Bewusst
/// duenner Dispatch auf <see cref="GetSymbolBodyTool.ExecuteAsync"/> ueber den
/// zielgebundenen Dispatch-Weg (<see cref="AnalysisToolCall"/>). Kein DI-Container
/// (Architektur-Verbot, siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
/// </summary>
internal static class SymbolBodyToolRegistrations
{
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        AddGetSymbolBody(tools, targetRoute);
    }

    private static void AddGetSymbolBody(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string[]? symbolIdentifiers = null, string? symbolIdentifier = null, int maxBodyLines = 80, int startLine = 1, int? endLine = null, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetSymbolBodyTool.ExecuteAsync(lease.Server, new GetSymbolBodyRequest(symbolIdentifiers, symbolIdentifier, maxBodyLines, startLine, endLine), ct),
                            AssemblySessionCall: lease => GetSymbolBodyTool.ExecuteAsync(lease, new GetSymbolBodyRequest(symbolIdentifiers, symbolIdentifier, maxBodyLines, startLine, endLine), ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_symbol_body", GetSymbolBodyDescription)));
    }

    private const string GetSymbolBodyDescription =
        "Wann nutzen: Source-Body eines oder mehrerer C#-Symbole lesen (Batch-Support in 1 Turn). " +
        "symbolIdentifiers: Array von Symbol-IDs oder symbolIdentifier als String-Alias fuer genau ein Symbol: " +
        "\"M:Namespace.Klasse.Methode\", " +
        "\"Datei.cs:Zeile:Spalte\", \"Datei.cs:Zeile\" oder \"Klasse.Methode\". " +
        "maxBodyLines: Begrenzung der Zeilenanzahl je Symbol-Body (Default 80). " +
        "startLine: 1-basierte Startzeile innerhalb des Methoden-Bodys fuer gezieltes Windowing langer Methoden (Default 1). " +
        "endLine: optionale 1-basierte Endzeile innerhalb des Methoden-Bodys (berechnet maxBodyLines als endLine - startLine + 1).";
}
