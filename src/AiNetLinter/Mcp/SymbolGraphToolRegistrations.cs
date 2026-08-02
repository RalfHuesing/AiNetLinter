#nullable enable

using System.Threading;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die vier reinen Symbolgraph-Tools (<c>find_symbol</c>, <c>find_references</c>,
/// <c>get_impact</c>, <c>get_type_hierarchy</c>) an der von <see cref="McpServerOptionsFactory"/>
/// aufgebauten Tool-Collection. Aus <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen
/// eigener <c>AIContextFootprint</c> (siehe <c> nicht mit jedem neu
/// registrierten Tool waechst.
/// </summary>
internal static class SymbolGraphToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die vier Symbolgraph-Tools hinzu. Tools erreichen den resident
    /// gehaltenen <paramref name="mcpState"/> per Delegate-Closure — kein DI-Container (siehe
    /// <c>.
    /// </summary>
    internal static void Register(McpServerPrimitiveCollection<McpServerTool> tools, McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string namePattern, string? kind = null, int maxResults = 50, CancellationToken ct = default) =>
                FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, maxResults, ct),
            new McpServerToolCreateOptions
            {
                Name = "find_symbol",
                Description = "Sucht C#-Symbole (Klassen, Methoden, Properties, Interfaces) per " +
                    "Substring im Namen. Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien. " +
                    "Bei 0 Treffern wird auf Textvorkommen in Nicht-C#-Dateien hingewiesen. " +
                    "Trunkiert standardmaessig auf 50 Treffer, ueberschreibbar via maxResults; " +
                    "Trunkierungs-Meta-Zeile meldet die Gesamt-Trefferzahl.",
            }));

        tools.Add(McpServerTool.Create(
            (string symbolIdentifier, int maxResults = 50, CancellationToken ct = default) =>
                FindReferencesTool.ExecuteAsync(mcpState, symbolIdentifier, maxResults, ct),
            new McpServerToolCreateOptions
            {
                Name = "find_references",
                Description = "Findet alle Aufrufstellen eines C#-Symbols (Datei:Zeile:Spalte " +
                    "oder qualifizierter/teil-qualifizierter Name). Deckt nur .cs-Dateien ab, " +
                    "keine .js/.razor/.xaml/.html/.css-Dateien. Trunkiert standardmaessig auf 50 " +
                    "Treffer, ueberschreibbar via maxResults; Trunkierungs-Meta-Zeile meldet die " +
                    "Gesamt-Trefferzahl.",
            }));

        tools.Add(McpServerTool.Create(
            (string? gitRef = null, string? symbolIdentifier = null, int maxResults = 50, CancellationToken ct = default) =>
                GetImpactTool.ExecuteAsync(mcpState, gitRef, symbolIdentifier, maxResults, ct),
            new McpServerToolCreateOptions
            {
                Name = "get_impact",
                Description = "Findet Aufrufstellen geaenderter C#-Signaturen. Entweder gitRef " +
                    "(Git-Commit-Ref, leer = uncommittete Aenderungen) ODER symbolIdentifier " +
                    "(Datei:Zeile:Spalte oder qualifizierter Name) angeben, nie beide. Deckt nur " +
                    ".cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien. Trunkiert " +
                    "standardmaessig auf 50 Treffer, ueberschreibbar via maxResults; " +
                    "Trunkierungs-Meta-Zeile meldet die Gesamt-Trefferzahl.",
            }));

        tools.Add(McpServerTool.Create(
            (string typeIdentifier, CancellationToken ct = default) =>
                GetTypeHierarchyTool.ExecuteAsync(mcpState, typeIdentifier, ct),
            new McpServerToolCreateOptions
            {
                Name = "get_type_hierarchy",
                Description = "Liefert Basisklassen, implementierte Interfaces und (abgeleitete " +
                    "Klassen bzw. implementierende Typen) eines C#-Typ-Identifikators (Datei:Zeile:" +
                    "Spalte oder qualifizierter/teil-qualifizierter Name). Deckt nur .cs-Dateien ab, " +
                    "keine .js/.razor/.xaml/.html/.css-Dateien.",
            }));
    }
}
