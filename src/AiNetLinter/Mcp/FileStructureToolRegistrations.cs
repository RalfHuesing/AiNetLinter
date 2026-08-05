#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die dateistruktur-orientierten Tools (aktuell <c>get_file_skeleton</c>,
/// <c>get_index_scope</c>, <c>get_hotspots</c>) an der von
/// <see cref="McpServerOptionsFactory"/> aufgebauten Tool-Collection. Aus
/// <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen eigener <c>AIContextFootprint</c>
/// (siehe <c> nicht mit jedem neu registrierten Tool waechst
/// JIT-Kontext). <c>get_violations</c> ist in eine eigene <see cref="AnalysisToolRegistrations"/>-
/// Klasse ausgelagert, weil sein <c>LinterEngine</c>-Pull-in den Footprint dieser Klasse ueber das
/// 2500-Limit getrieben hat.
/// </summary>
internal static class FileStructureToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die dateistruktur-orientierten Tools hinzu. Tools erreichen den
    /// resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure - kein DI-Container
    /// (siehe <c>. Optionaler <paramref name="callLog"/> zeichnet jeden Tool-Aufruf auf, wenn
    /// aktiv (kein Overhead bei deaktiviertem Log).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog = null)
    {
        AddGetFileSkeleton(tools, mcpState, callLog);
        AddGetIndexScope(tools, mcpState, callLog);
        AddGetHotspots(tools, mcpState, callLog);
    }

    private static void AddGetFileSkeleton(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string filePath, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetFileSkeletonTool.ExecuteAsync(mcpState, filePath, ct);
                }
                return await callLog.ExecuteCallAsync("get_file_skeleton", filePath,
                    () => GetFileSkeletonTool.ExecuteAsync(mcpState, filePath, ct));
            },
            new McpServerToolCreateOptions
            {
                Name = "get_file_skeleton",
                Description = GetFileSkeletonDescription,
            }));
    }

    private const string GetFileSkeletonDescription =
        "Liefert das Struktur-Skelett (Typen, Signaturen ohne " +
        "Bodies) einer einzelnen C#-Datei per relativem Dateipfad. " +
        "Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien.";

    private static void AddGetIndexScope(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetIndexScopeTool.ExecuteAsync(mcpState, ct);
                }
                return await callLog.ExecuteCallAsync("get_index_scope", "",
                    () => GetIndexScopeTool.ExecuteAsync(mcpState, ct));
            },
            new McpServerToolCreateOptions
            {
                Name = "get_index_scope",
                Description = GetIndexScopeDescription,
            }));
    }

    private const string GetIndexScopeDescription =
        "Liefert eine Dateityp-Aufschluesselung der geladenen Solution: " +
        ".cs (voll vom Symbolgraph abgedeckt) sowie .css/.html/.js/.razor/.xaml " +
        "(jeweils nicht vom Symbolgraph abgedeckt, mit Anzahl) - Orientierung, bevor " +
        "andere Tools wie find_symbol/search_pattern aufgerufen werden.";

    private static void AddGetHotspots(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string? scopeFilter = null, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetHotspotsTool.ExecuteAsync(mcpState, scopeFilter, ct);
                }
                return await callLog.ExecuteCallAsync("get_hotspots", scopeFilter ?? "",
                    () => GetHotspotsTool.ExecuteAsync(mcpState, scopeFilter, ct));
            },
            new McpServerToolCreateOptions
            {
                Name = "get_hotspots",
                Description = GetHotspotsDescription,
            }));
    }

    private const string GetHotspotsDescription =
        "Liefert .cs-Dateien der geladenen Solution, die sich ihrem " +
        "konfigurierten Zeilen-Limit (MaxLineCount aus rules.json/Default) naehern " +
        "oder es ueberschreiten - Drift-Signal vor einem geplanten Edit. Optionaler " +
        "scopeFilter matched gegen Projekt-Name oder solution-relativen Dateipfad.";
}
