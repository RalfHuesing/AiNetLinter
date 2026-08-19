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
        McpCodeGraphServer mcpState)
    {
        AddGetSymbolBody(tools, mcpState);
    }

    private static void AddGetSymbolBody(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? symbolIdentifier = null, int maxBodyLines = 80, CancellationToken ct = default) =>
                GetSymbolBodyTool.ExecuteAsync(mcpState, symbolIdentifier, maxBodyLines, ct),
            new McpServerToolCreateOptions
            {
                Name = "get_symbol_body",
                Description = GetSymbolBodyDescription,
            }));
    }

    private const string GetSymbolBodyDescription =
        "Wann nutzen: Source-Body eines C#-Symbols lesen, wenn Fundstelle/Signatur schon " +
        "bekannt ist. symbolIdentifier: \"M:Namespace.Klasse.Methode\" oder \"Datei.cs:42:10\" oder " +
        "\"Datei.cs:42\" (Zeile ohne Spalte — bei mehreren Symbolen auf der Zeile liefert das " +
        "Ergebnis eine Kandidatenliste statt eines Treffers) oder \"Klasse.Methode\". " +
        "Hart gekappt bei maxBodyLines (Default 80).";
}
