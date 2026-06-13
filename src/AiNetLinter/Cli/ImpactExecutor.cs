#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Core;

namespace AiNetLinter.Cli;

/// <summary>
/// Führt die semantische Diff-Impact-Analyse ab einer Git-Referenz aus.
/// </summary>
public static class ImpactExecutor
{
    private const string NoImpactCallSitesMessage = "Keine betroffenen Aufrufstellen gefunden.";
    private const string ImpactHeaderMessage = "# Semantische Diff-Impact-Analyse";
    private const string CallSitesFoundMessage = "Gefundene betroffene Aufrufstellen fuer geaenderte Signaturen:";

    /// <summary>
    /// Führt die Impact-Analyse für die Solution aus.
    /// </summary>
    public static async Task<int> RunImpactAnalysisAsync(LinterArgs args)
    {
        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath);
        var callSites = await DiffImpactAnalyzer.AnalyzeAsync(catalog.Solution, args.TargetPath, args.ImpactRef, args.Verbose);

        if (callSites.Count == 0)
        {
            Console.WriteLine(NoImpactCallSitesMessage);
        }
        else
        {
            Console.WriteLine(ImpactHeaderMessage);
            Console.WriteLine(CallSitesFoundMessage);
            foreach (var callSite in callSites)
            {
                Console.WriteLine(callSite);
            }
        }

        return 0;
    }
}
