#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.Mcp.Assemblies.Analysis.References;

namespace AiNetLinter.Mcp.Tools.SymbolGraph.Navigation;

internal static class AssemblySymbolCandidateFormatter
{
    internal static IEnumerable<string> FormatLocations(AssemblySymbolTarget candidate)
    {
        var solution = candidate.Lease.Server.GetCurrentSolution();
        if (solution is null) return [];

        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;
        var view = AssemblyNavigationLeaseAccess.CreateView(candidate.Lease);
        return FindSymbolTool.FormatSymbolLocationEntries(
                candidate.Symbol,
                outputRoot,
                view.Identity)
            .Select(entry => FormatLocation(entry with
            {
                Origin = view.Origin,
            }));
    }

    private static string FormatLocation(SymbolLocationEntry entry) =>
        $"{entry.FilePath}:{entry.Line} - {entry.Kind}: {entry.Name} " +
        $"[assembly={entry.Origin?.CanonicalPath}; origin={entry.Origin?.OriginKind}]";
}
