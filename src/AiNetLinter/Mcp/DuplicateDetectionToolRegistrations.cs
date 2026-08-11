#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert das einzelne Duplicate-Detection-Tool (<c>find_duplicates</c>) an der von
/// <see cref="McpServerOptionsFactory"/> aufgebauten Tool-Collection. Eigene Registrierungsdatei
/// statt Anhaengen an <see cref="AnalysisToolRegistrations"/> oder
/// <see cref="SymbolGraphToolRegistrations"/>, weil <c>find_duplicates</c> weder den
/// <c>LinterEngine</c>-Pull-in der Analysis-Tools noch die Symbolgraph-Traversierungslogik der
/// Symbolgraph-Tools teilt — es nutzt ausschliesslich die eigenstaendige
/// <see cref="Core.DuplicateDetection.DuplicateDetectionEngine"/> (Core/DuplicateDetection/, auch
/// vom Linter-Checker <c>DuplicateCodeChecker</c> genutzt).
/// </summary>
internal static class DuplicateDetectionToolRegistrations
{
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog = null)
    {
        tools.Add(McpServerTool.Create(
            async (int? minTokens = null, string? similarityThreshold = null, bool? normalizeIdentifiers = null,
                string? scopeDir = null, int? maxResults = null, string? mode = null, string? helperSymbol = null,
                CancellationToken ct = default) =>
            {
                var input = new DuplicateDetectionInput(
                    minTokens, similarityThreshold, normalizeIdentifiers, scopeDir, maxResults, mode, helperSymbol);
                if (callLog is null)
                {
                    return await DuplicateDetectionTool.ExecuteAsync(mcpState, input, ct);
                }
                return await callLog.ExecuteCallAsync(
                    "find_duplicates",
                    $"{minTokens}|{similarityThreshold}|{normalizeIdentifiers}|{scopeDir}|{maxResults}|{mode}|{helperSymbol}",
                    () => DuplicateDetectionTool.ExecuteAsync(mcpState, input, ct));
            },
            new McpServerToolCreateOptions
            {
                Name = "find_duplicates",
                Description = FindDuplicatesDescription,
            }));
    }

    private const string FindDuplicatesDescription =
        "Wann nutzen: Solution-weite DRY-Audit-Suche nach Code-Duplikaten (Token-basiertes " +
        "Clone-Detection, Jaccard-N-Gram, Method-Granularitaet). mode='clone' (Default): findet " +
        "z. B. mehrfach separat instanziierte, eigentlich identische Objekt-Initialisierungen, die " +
        "zentralisiert werden sollten. Ergebnis als Cluster (transitiv aehnliche Methoden), nicht " +
        "als isolierte Paare. minTokens (Default aus rules.json, 30) filtert triviale Methoden. " +
        "similarityThreshold: 'exact' (>=0.95, fast identisch), 'near' (>=0.80) oder 'fuzzy' " +
        "(>=0.65, Default — niedrigste noch angezeigte Stufe). normalizeIdentifiers (Default " +
        "false) schaltet Erkennung umbenannter Klone an (Identifier/Literale werden vor dem " +
        "Vergleich normalisiert). scopeDir grenzt auf einen Teilbereich ein (Default " +
        "Solution-Root). maxResults begrenzt die gezeigten Cluster/Kandidaten (Default 20). " +
        "mode='refactoring-drift': findet Methoden, die einen bereits existierenden Helper " +
        "(helperSymbol, Pflicht bei diesem mode — Format wie find_references: " +
        "Datei:Zeile:Spalte, stabile DocumentationCommentId oder qualifizierter Name) strukturell " +
        "nachbauen statt ihn aufzurufen ('absence-of-calls'-Heuristik). Ergebnis als Kandidaten, " +
        "nicht als Verstoesse (hoeheres False-Positive-Budget als mode='clone' — strukturelle " +
        "Aehnlichkeit ist nicht zwingend Drift). similarityThreshold wird in diesem Modus ignoriert " +
        "(fester near-Schwellwert aus rules.json).";
}
