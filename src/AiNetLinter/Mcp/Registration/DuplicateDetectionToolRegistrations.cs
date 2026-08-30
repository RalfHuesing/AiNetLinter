#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Registriert das einzelne Duplicate-Detection-Tool (<c>find_duplicates</c>) an der von
/// <see cref="McpServerOptionsFactory"/> aufgebauten Tool-Collection. Eigene Registrierungsdatei
/// statt Anhaengen an <see cref="AnalysisToolRegistrations"/> oder
/// <see cref="SymbolGraphToolRegistrations"/>, weil <c>find_duplicates</c> weder den
/// <c>LinterEngine</c>-Pull-in der Analysis-Tools noch die Symbolgraph-Traversierungslogik der
/// Symbolgraph-Tools teilt — es nutzt ausschliesslich die eigenstaendige
/// <see cref="AiNetLinter.Core.DuplicateDetection.DuplicateDetectionEngine"/> (Core/DuplicateDetection/, auch
/// vom Linter-Checker <c>DuplicateCodeChecker</c> genutzt). Das Lambda ist projektgebunden:
/// <c>targetType</c> und <c>targetPath</c> sind Pflicht und adressieren den gemeinsamen Dispatch.
/// </summary>
internal static class DuplicateDetectionToolRegistrations
{
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, int? minTokens = null, string? similarityThreshold = null, bool? normalizeIdentifiers = null,
                string? scopeDir = null, int? maxResults = null, string? mode = null, string? helperSymbol = null,
                string? scopeType = null,
                CancellationToken ct = default) =>
                await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease =>
                    {
                        var input = new DuplicateDetectionInput(
                            minTokens, similarityThreshold, normalizeIdentifiers, scopeDir, maxResults, mode, helperSymbol, scopeType);
                        return DuplicateDetectionTool.ExecuteAsync(lease.Server, input, ct);
                    }),
            McpToolRegistrationOptions.ReadOnlyTool("find_duplicates", FindDuplicatesDescription)));
    }

    private const string FindDuplicatesDescription =
        "Wann nutzen: Solution-weite DRY-Audit-Suche nach Code-Duplikaten (Token-basierte " +
        "Clone-Detection, Jaccard-N-Gram, Method-Granularitaet). mode: 'clone' [Default] (findet transitiv " +
        "aehnliche Methodencluster), 'refactoring-drift' (findet Methoden, die helperSymbol nachbauen statt es aufzurufen), " +
        "'structural' (semantisch aehnliche Hilfsmethoden per Roslyn-Strukturprofil & Cosine-Similarity). " +
        "helperSymbol: Ziel-Helper (Pflicht bei mode='refactoring-drift': Datei:Zeile:Spalte, DocCommentId oder Name). " +
        "minTokens: Mindest-Tokens (Default aus rules.json, 30). similarityThreshold: 'exact' (>=0.95), 'near' (>=0.80), " +
        "'fuzzy' (>=0.65 [Default]). normalizeIdentifiers: Klone mit umbenannten Variablen erkennen (Default false). " +
        "scopeDir: Verzeichnispfad zur Eingrenzung. scopeType: 'all' [Default], 'production', 'tests'. maxResults: Begrenzung (Default 20).";
}
