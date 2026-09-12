#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using ModelContextProtocol.Protocol;
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
/// <c>targetPath</c> ist Pflicht und adressiert den gemeinsamen Dispatch.
/// </summary>
internal static class DuplicateDetectionToolRegistrations
{
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, int? minTokens = null, string? similarityThreshold = null, bool? normalizeIdentifiers = null,
                string? scopeDir = null, int? maxResults = null, string? mode = null, string? helperSymbol = null,
                string? scopeType = "production",
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease =>
                    {
                        var input = new DuplicateDetectionInput(
                            minTokens, similarityThreshold, normalizeIdentifiers, scopeDir, maxResults, mode, helperSymbol, scopeType);
                        return DuplicateDetectionTool.ExecuteAsync(lease.Server, input, ct);
                    });
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("find_duplicates", FindDuplicatesDescription)));
    }

    private const string FindDuplicatesDescription =
        "Suche nach Code-Duplikaten auf Methodenebene. " +
        "mode: 'clone' [Default: aehnliche Cluster], 'refactoring-drift' [baut helperSymbol nach statt Aufruf], 'structural' [strukturelle Aehnlichkeit]. " +
        "helperSymbol: Zielmethode (Pflicht bei mode='refactoring-drift'). " +
        "similarityThreshold: 'exact' (>=0.95), 'near' (>=0.80), 'fuzzy' (>=0.65 [Default]). " +
        "minTokens: Mindest-Tokens (Default 30). normalizeIdentifiers: Variablenumbenennungen ignorieren (Default false). " +
        "scopeDir: Verzeichnispfad. scopeType: 'production' [Default], 'all', 'tests'. maxResults: Default 20.";
}
