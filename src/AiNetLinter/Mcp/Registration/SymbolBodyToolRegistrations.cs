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
            async (string targetType, string targetPath, string[]? symbolIdentifiers = null, int maxBodyLines = 80, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetSymbolBodyTool.ExecuteAsync(lease.Server, symbolIdentifiers, maxBodyLines, ct),
                            AssemblySessionCall: lease => GetSymbolBodyTool.ExecuteAsync(lease.Server, symbolIdentifiers, maxBodyLines, ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_symbol_body", GetSymbolBodyDescription)));
    }

    private const string GetSymbolBodyDescription =
        "Wann nutzen: Source-Body eines oder mehrerer C#-Symbole lesen (Batch-Support fuer 1 Turn). " +
        "symbolIdentifiers: Array von Symbol-IDs (auch fuer genau ein Symbol): \"M:Namespace.Klasse.Methode\" " +
        "oder \"Datei.cs:42:10\" oder \"Datei.cs:42\" oder \"Klasse.Methode\". " +
        "Hart gekappt bei maxBodyLines je Symbol (Default 80).";
}
