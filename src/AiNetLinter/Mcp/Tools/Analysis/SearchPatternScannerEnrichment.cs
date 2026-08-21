#nullable enable

using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Tools.Analysis;

internal static class SearchPatternScannerEnrichment
{
    internal static async Task<SearchPatternScanResult> ScanAsync(
        SearchPatternScannerParameters parameters)
    {
        var lexicalResult = SearchPatternScanner.Scan(parameters);
        if (!parameters.EnrichCSharp || lexicalResult.Payload.Matches.Count == 0)
        {
            return lexicalResult;
        }

        var enrichedMatches = await SearchPatternRoslynEnricher.EnrichAsync(
            parameters.Solution,
            lexicalResult.Payload.Matches,
            parameters.CancellationToken).ConfigureAwait(false);
        return lexicalResult with
        {
            Payload = lexicalResult.Payload with { Matches = enrichedMatches },
        };
    }
}
