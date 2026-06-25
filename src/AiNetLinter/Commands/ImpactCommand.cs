#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Core;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Führt die semantische Diff-Impact-Analyse ab einer Git-Referenz aus.
/// </summary>
internal static class ImpactCommand
{
    private const string NoImpactCallSitesMessage = "Keine betroffenen Aufrufstellen gefunden.";
    private const string ImpactHeaderMessage = "# Semantische Diff-Impact-Analyse";
    private const string CallSitesFoundMessage = "Gefundene betroffene Aufrufstellen fuer geaenderte Signaturen:";

    /// <summary>
    /// Führt die Impact-Analyse für die Solution aus.
    /// </summary>
    internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath, ct);
        var callSites = await DiffImpactAnalyzer.AnalyzeAsync(catalog.Solution, args.TargetPath, args.ImpactRef, args.Verbose);

        if (callSites.Count == 0)
        {
            c.WriteLine(NoImpactCallSitesMessage);
        }
        else
        {
            c.WriteLine(ImpactHeaderMessage);
            c.WriteLine(CallSitesFoundMessage);
            foreach (var callSite in callSites)
            {
                c.WriteLine(callSite);
            }
        }

        return 0;
    }
}
