#nullable enable

using System.Threading;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die dateistruktur-orientierten Tools (aktuell <c>get_file_skeleton</c>,
/// <c>get_index_scope</c>) an der von <see cref="McpServerOptionsFactory"/> aufgebauten
/// Tool-Collection. Aus <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen eigener
/// <c>AIContextFootprint</c> (siehe <c>AiNetLinter.mdc</c>) nicht mit jedem neu registrierten Tool
/// waechst (siehe step-007 JIT-Kontext). Vorbereitet fuer die weiteren EPIC-04-Tools
/// (<c>get_hotspots</c>, <c>get_violations</c>, <c>search_pattern</c>).
/// </summary>
internal static class FileStructureToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die dateistruktur-orientierten Tools hinzu. Tools erreichen den
    /// resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure — kein DI-Container
    /// (siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(McpServerPrimitiveCollection<McpServerTool> tools, McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string filePath, CancellationToken ct = default) =>
                GetFileSkeletonTool.ExecuteAsync(mcpState, filePath, ct),
            new McpServerToolCreateOptions
            {
                Name = "get_file_skeleton",
                Description = "Liefert das Struktur-Skelett (Typen, Signaturen ohne " +
                    "Bodies) einer einzelnen C#-Datei per relativem Dateipfad. " +
                    "Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien.",
            }));

        tools.Add(McpServerTool.Create(
            (CancellationToken ct = default) =>
                GetIndexScopeTool.ExecuteAsync(mcpState, ct),
            new McpServerToolCreateOptions
            {
                Name = "get_index_scope",
                Description = "Liefert eine Dateityp-Aufschluesselung der geladenen Solution: " +
                    ".cs (voll vom Symbolgraph abgedeckt) sowie .css/.html/.js/.razor/.xaml " +
                    "(jeweils nicht vom Symbolgraph abgedeckt, mit Anzahl) — Orientierung, bevor " +
                    "andere Tools wie find_symbol/search_pattern aufgerufen werden.",
            }));
    }
}
