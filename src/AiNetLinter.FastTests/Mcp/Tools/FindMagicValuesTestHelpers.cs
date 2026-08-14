#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.MagicValues;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.FastTests.Mcp.Tools;

/// <summary>
/// Geteilte Helpers fuer <see cref="FindMagicValuesScannerTests"/> und
/// <see cref="FindMagicValuesScannerHeuristicTests"/> — einheitliches
/// <c>RunAsync</c>-Setup fuer die komponentenweise Scanner-Verifikation, ausgelagert in
/// eine eigene Datei, damit beide Test-Klassen unter dem <c>MaxLineCount: 500</c>-Limit
/// bleiben. <c>internal static</c>, weil Test-Klassen selbst projekt-intern sind.
/// </summary>
internal static class FindMagicValuesTestHelpers
{
    // ainetlinter-disable MaxMethodParameterCount — Test-Helper mit bequemer positional-API
    // (je ein optionales Argument pro Filter); Aufrufer uebergeben typischerweise 1-2 Filter,
    // die uebrigen Defaults sind unkritisch. Direkter Pfad ohne ScanAsyncParams-Record wuerde
    // die Aufrufstellen der Tests kuenstlich aufblahen (alle 7 Defaults muessten benannt werden).
    internal static async Task<FindMagicValuesResult> RunAsync(
        (string FileName, string Source) file,
        MagicValueValueType? valueType = null,
        MagicValueCategory? category = null,
        int minOccurrences = 1,
        int maxResults = 50,
        HashSet<int>? ignoreNumbers = null,
        bool includeSuppressed = false)
    {
        using var testSolution = CreateSolution(file);
        return await RunAsync(testSolution.Solution, new ScanAsyncParams(
            ValueType: valueType,
            Category: category,
            MinOccurrences: minOccurrences,
            MaxResults: maxResults,
            IgnoreNumbers: ignoreNumbers,
            IncludeSuppressed: includeSuppressed));
    }

    // ainetlinter-disable MaxMethodParameterCount — siehe oben (Aufrufstellen-Komfort).
    internal static Task<FindMagicValuesResult> RunAsync(
        Solution solution,
        MagicValueValueType? valueType = null,
        MagicValueCategory? category = null,
        int minOccurrences = 1,
        int maxResults = 50,
        HashSet<int>? ignoreNumbers = null,
        bool includeSuppressed = false)
    {
        return RunAsync(solution, new ScanAsyncParams(
            ValueType: valueType,
            Category: category,
            MinOccurrences: minOccurrences,
            MaxResults: maxResults,
            IgnoreNumbers: ignoreNumbers,
            IncludeSuppressed: includeSuppressed));
    }

    internal static async Task<FindMagicValuesResult> RunAsync(
        Solution solution,
        ScanAsyncParams p)
    {
        return await FindMagicValuesScanner.ScanAsync(new FindMagicValuesScannerParameters(
            Solution: solution,
            ScopeFilter: p.ScopeFilter,
            ValueType: p.ValueType,
            Category: p.Category,
            MinOccurrences: p.MinOccurrences,
            MaxResults: p.MaxResults,
            IgnoreNumbers: p.IgnoreNumbers?.ToArray(),
            IncludeTests: false,
            IncludeSuppressed: p.IncludeSuppressed,
            ChangedOnly: false,
            CancellationToken: CancellationToken.None));
    }

    internal static RoslynTestSolution CreateSolution(params (string fileName, string content)[] files) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\FindMagicValuesScannerTests.slnx",
            new ProjectSpec("TestProject", files, VirtualProjectDirectory: "."));
}

/// <summary>Hilfs-Record fuer <see cref="FindMagicValuesTestHelpers.RunAsync(Solution, ScanAsyncParams)"/>
/// — buendelt die 7 Konfigurations-Felder in einem Parameter-Object, damit die Methoden-Signatur
/// das <c>MaxMethodParameterCount: 4</c>-Limit (siehe <c>AiNetLinter.mdc</c>) einhaelt. Bewusst
/// auf Top-Level statt nested, weil <c>BanPublicNestedTypes</c> auch <c>internal</c> nested Typen
/// verbietet (Ausnahme nur fuer <c>private</c>) — und der Record <c>internal</c> sein muss, damit
/// die Test-Klassen ihn ueber ihre <c>RunAsync(... ScanAsyncParams)</c>-Aufrufe konstruieren koennen.</summary>
internal sealed record ScanAsyncParams(
    string? ScopeFilter = null,
    MagicValueValueType? ValueType = null,
    MagicValueCategory? Category = null,
    int MinOccurrences = 1,
    int MaxResults = 50,
    HashSet<int>? IgnoreNumbers = null,
    bool IncludeSuppressed = false);
