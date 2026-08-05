#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die vier reinen Symbolgraph-Tools (<c>find_symbol</c>, <c>find_references</c>,
/// <c>get_impact</c>, <c>get_type_hierarchy</c>) an der von <see cref="McpServerOptionsFactory"/>
/// aufgebauten Tool-Collection. Aus <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen
/// eigener <c>AIContextFootprint</c> (siehe <c>AiNetLinter.mdc</c>) nicht mit jedem neu
/// registrierten Tool waechst.
/// </summary>
internal static class SymbolGraphToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die vier Symbolgraph-Tools hinzu. Tools erreichen den resident
    /// gehaltenen <paramref name="mcpState"/> per Delegate-Closure - kein DI-Container (siehe
    /// <c>AiNetLinterRichtlinien.mdc</c> §2). Optionaler <paramref name="callLog"/> zeichnet jeden
    /// Tool-Aufruf auf, wenn aktiv (kein Overhead bei deaktiviertem Log).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog = null)
    {
        AddFindSymbol(tools, mcpState, callLog);
        AddFindReferences(tools, mcpState, callLog);
        AddGetImpact(tools, mcpState, callLog);
        AddGetTypeHierarchy(tools, mcpState, callLog);
    }

    private static void AddFindSymbol(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string namePattern, string? kind = null, int maxResults = 50, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, maxResults, ct);
                }
                await using var scope = callLog.StartRecording("find_symbol", $"{namePattern}|{kind}|{maxResults}");
                var result = await FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, maxResults, ct);
                scope.Complete(result);
                return result;
            },
            new McpServerToolCreateOptions
            {
                Name = "find_symbol",
                Description = FindSymbolDescription,
            }));
    }

    private const string FindSymbolDescription =
        "Sucht C#-Symbole (Klassen, Methoden, Properties, Interfaces) per " +
        "Substring im Namen. Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien. " +
        "Bei 0 Treffern wird auf Textvorkommen in Nicht-C#-Dateien hingewiesen. " +
        "Trunkiert standardmaessig auf 50 Treffer, ueberschreibbar via maxResults; " +
        "Trunkierungs-Meta-Zeile meldet die Gesamt-Trefferzahl.";

    private static void AddFindReferences(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string symbolIdentifier, int maxResults = 50, int depth = 1, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await FindReferencesTool.ExecuteAsync(mcpState, symbolIdentifier, maxResults, depth, ct);
                }
                await using var scope = callLog.StartRecording("find_references", $"{symbolIdentifier}|{maxResults}|{depth}");
                var result = await FindReferencesTool.ExecuteAsync(mcpState, symbolIdentifier, maxResults, depth, ct);
                scope.Complete(result);
                return result;
            },
            new McpServerToolCreateOptions
            {
                Name = "find_references",
                Description = FindReferencesDescription,
            }));
    }

    private const string FindReferencesDescription =
        "Findet alle Aufrufstellen eines C#-Symbols anhand stabiler ID " +
        "(DocumentationCommentId, ueberlebt Zeilenverschiebungen, disambiguiert Overloads) oder " +
        "Datei:Zeile:Spalte bzw. qualifiziertem/teil-qualifiziertem Namen. Optionaler " +
        "depth-Parameter (Default 1, hard cap 3) loest transitive Aufrufstellen und aggregiert " +
        "sie. Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien. Trunkiert " +
        "standardmaessig auf 50 Treffer, ueberschreibbar via maxResults; Trunkierungs-Meta-Zeile " +
        "meldet die Gesamt-Trefferzahl.";

    private static void AddGetImpact(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string? gitRef = null, string? symbolIdentifier = null, int maxResults = 50, int depth = 1, CancellationToken ct = default) =>
            {
                var input = new GetImpactInput(gitRef, symbolIdentifier, maxResults, depth);
                if (callLog is null)
                {
                    return await GetImpactTool.ExecuteAsync(mcpState, input, ct);
                }
                await using var scope = callLog.StartRecording("get_impact", $"{gitRef}|{symbolIdentifier}|{maxResults}|{depth}");
                var result = await GetImpactTool.ExecuteAsync(mcpState, input, ct);
                scope.Complete(result);
                return result;
            },
            new McpServerToolCreateOptions
            {
                Name = "get_impact",
                Description = GetImpactDescription,
            }));
    }

    private const string GetImpactDescription =
        "Findet Aufrufstellen geaenderter C#-Signaturen. Ohne jeden Parameter aufgerufen " +
        "prueft es uncommittete lokale Aenderungen (Standardfall). Alternativ: entweder " +
        "gitRef (Git-Commit-Ref) ODER symbolIdentifier angeben, nie beide — symbolIdentifier " +
        "akzeptiert stabile ID (DocumentationCommentId, ueberlebt Zeilenverschiebungen, " +
        "disambiguiert Overloads) oder Datei:Zeile:Spalte bzw. qualifiziertem/teil-" +
        "qualifiziertem Namen. Optionaler depth-Parameter (Default 1, hard cap 3) wirkt nur im " +
        "Symbol-Branch und loest transitive Aufrufstellen, aggregiert. Deckt nur .cs-Dateien ab, " +
        "keine .js/.razor/.xaml/.html/.css-Dateien. Trunkiert standardmaessig auf 50 Treffer, " +
        "ueberschreibbar via maxResults; Trunkierungs-Meta-Zeile meldet die Gesamt-Trefferzahl.";

    private static void AddGetTypeHierarchy(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string typeIdentifier, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetTypeHierarchyTool.ExecuteAsync(mcpState, typeIdentifier, ct);
                }
                await using var scope = callLog.StartRecording("get_type_hierarchy", typeIdentifier);
                var result = await GetTypeHierarchyTool.ExecuteAsync(mcpState, typeIdentifier, ct);
                scope.Complete(result);
                return result;
            },
            new McpServerToolCreateOptions
            {
                Name = "get_type_hierarchy",
                Description = GetTypeHierarchyDescription,
            }));
    }

    private const string GetTypeHierarchyDescription =
        "Liefert Basisklassen, implementierte Interfaces und (abgeleitete " +
        "Klassen bzw. implementierende Typen) eines C#-Typ-Identifikators anhand stabiler ID " +
        "(DocumentationCommentId, ueberlebt Zeilenverschiebungen, disambiguiert Overloads) oder " +
        "Datei:Zeile:Spalte bzw. qualifiziertem/teil-qualifiziertem Namen. Deckt nur .cs-Dateien " +
        "ab, keine .js/.razor/.xaml/.html/.css-Dateien.";
}
