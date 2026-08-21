#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Analysis;

internal static class SearchPatternScannerEnrichment
{
    internal static async Task<SearchPatternScanResult> ScanAsync(
        SearchPatternScannerParameters parameters)
        => await ScanAsync(
            parameters,
            SearchPatternScanner.Scan,
            SearchPatternRoslynEnricher.EnrichAsync).ConfigureAwait(false);

    internal static async Task<SearchPatternScanResult> ScanAsync(
        SearchPatternScannerParameters parameters,
        Func<SearchPatternScannerParameters, SearchPatternScanResult> lexicalScan,
        Func<Solution, IReadOnlyList<SearchPatternMatch>, CancellationToken, Task<IReadOnlyList<SearchPatternMatch>>> enrich)
    {
        var lexicalResult = lexicalScan(parameters);
        if (!parameters.EnrichCSharp || lexicalResult.Payload.Matches.Count == 0)
        {
            return lexicalResult;
        }

        try
        {
            var enrichedMatches = await enrich(
                parameters.Solution,
                lexicalResult.Payload.Matches,
                parameters.CancellationToken).ConfigureAwait(false);
            return lexicalResult with
            {
                Payload = lexicalResult.Payload with { Matches = enrichedMatches },
            };
        }
        catch (OperationCanceledException) when (parameters.CancellationToken.IsCancellationRequested)
        {
            return lexicalResult with
            {
                Payload = lexicalResult.Payload with
                {
                    Completeness = SearchPatternScannerCompleteness.MarkCancellation(
                        lexicalResult.Payload.Completeness),
                },
            };
        }
    }
}
