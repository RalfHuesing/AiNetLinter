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
        "Wann nutzen: Solution-weite DRY-Audit-Suche nach Code-Duplikaten (Token-basiertes " +
        "Clone-Detection, Jaccard-N-Gram, Method-Granularitaet). mode='clone' (Default): findet " +
        "z. B. mehrfach separat instanziierte, eigentlich identische Objekt-Initialisierungen, die " +
        "zentralisiert werden sollten. Ergebnis als Cluster (transitiv aehnliche Methoden), nicht " +
        "als isolierte Paare. minTokens (Default aus rules.json, 30) filtert triviale Methoden. " +
        "similarityThreshold: 'exact' (>=0.95, fast identisch), 'near' (>=0.80) oder 'fuzzy' " +
        "(>=0.65, Default — niedrigste noch angezeigte Stufe). normalizeIdentifiers (Default " +
        "false) schaltet Erkennung umbenannter Klone an (Identifier/Literale werden vor dem " +
        "Vergleich normalisiert). scopeDir grenzt auf einen Teilbereich ein (Default " +
        "Solution-Root). scopeType ('all' [Default], 'production', 'tests') filtert nach " +
        "Produktions- oder Test-Code. maxResults begrenzt die gezeigten Cluster/Kandidaten (Default 20). " +
        "mode='refactoring-drift': findet Methoden, die einen bereits existierenden Helper " +
        "(helperSymbol, Pflicht bei diesem mode — Format wie find_references: " +
        "Datei:Zeile:Spalte, stabile DocumentationCommentId oder qualifizierter Name) strukturell " +
        "nachbauen statt ihn aufzurufen ('absence-of-calls'-Heuristik). Ergebnis als Kandidaten, " +
        "nicht als Verstoesse (hoeheres False-Positive-Budget als mode='clone' — strukturelle " +
        "Aehnlichkeit ist nicht zwingend Drift). similarityThreshold wird in diesem Modus ignoriert " +
        "(fester near-Schwellwert aus rules.json). " +
        "mode='structural': findet semantisch aehnliche Hilfsmethoden anhand eines Roslyn-" +
        "Strukturprofils und Cosine-Similarity (Typ-4/Intended-Duplication, unabhaengige " +
        "Namens-/Literal-Varianten). Ergebnis als manuell zu pruefende Kandidatencluster, " +
        "keine automatische DuplicateCode-Violation. similarityThreshold filtert exact/near/fuzzy " +
        "ueber eigene Cosine-Schwellwerte aus rules.json (StructuralDuplicate*Threshold), nicht " +
        "ueber die Jaccard-DuplicateCode-Schwellwerte. helperSymbol wird in diesem Modus ignoriert. " +
        "Kleine Helper oft nur mit minTokens unter dem Lint-Default 30 sichtbar.";
}
