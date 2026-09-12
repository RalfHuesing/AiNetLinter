#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Protocol;
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
/// (Architektur-Verbot, siehe <c>AiNetLinter-Richtlinien.mdc</c> §2).
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
            async (RequestContext<CallToolRequestParams> context, string targetPath, string[]? symbolIdentifiers = null, int maxBodyLines = 80, int startLine = 1, int? endLine = null, int maxResponseBytes = GetSymbolBodyTool.DefaultMaxResponseBytes, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var request = new GetSymbolBodyRequest(symbolIdentifiers, maxBodyLines, startLine, endLine, maxResponseBytes);
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetSymbolBodyTool.ExecuteAsync(lease.Server, request, ct),
                            AssemblySessionCall: lease => GetSymbolBodyTool.ExecuteAsync(lease, request, ct),
                            MaxResponseBytes: maxResponseBytes,
                            PostNavigationResponseBudget: GetSymbolBodyTool.ApplyFinalResponseBudget,
                            ApplyAssemblyWireBudget: false),
                        ct));
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("get_symbol_body", GetSymbolBodyDescription)));
    }

    private const string GetSymbolBodyDescription =
        "Source-Body eines oder mehrerer C#-Symbole lesen (Batch in 1 Turn). " +
        "symbolIdentifiers: Array von Symbol-IDs ('M:Namespace.Klasse.Methode', 'Datei.cs:Zeile:Spalte' oder 'Klasse.Methode'). " +
        "maxBodyLines: Begrenzung je Body (Default 80). " +
        "startLine: 1-basierte Startzeile im Body fuer Windowing (Default 1). " +
        "endLine: optionale Endzeile im Body.";
}
