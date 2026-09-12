#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.MagicValues;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.FastTests.Mcp.Tools.MagicValueAdvisory;

/// <summary>
/// Geteilte Helpers fuer <see cref="MagicValueAdvisoryScannerTests"/> und
/// <see cref="MagicValueAdvisoryScannerHeuristicTests"/> â€” einheitliches
/// <c>RunAsync</c>-Setup fuer die komponentenweise Scanner-Verifikation, ausgelagert in
/// eine eigene Datei, damit beide Test-Klassen unter dem <c>MaxLineCount: 500</c>-Limit
/// bleiben. <c>internal static</c>, weil Test-Klassen selbst projekt-intern sind.
/// </summary>
internal static class MagicValueAdvisoryTestHelpers
{
    // ainetlinter-disable MaxMethodParameterCount â€” Test-Helper mit bequemer positional-API
    // (je ein optionales Argument pro Filter); Aufrufer uebergeben typischerweise 1-2 Filter,
    // die uebrigen Defaults sind unkritisch. Direkter Pfad ohne ScanAsyncParams-Record wuerde
    // die Aufrufstellen der Tests kuenstlich aufblahen (alle Defaults muessten benannt werden).
    internal static async Task<MagicValueAdvisoryResult> RunAsync(
        (string FileName, string Source) file,
        MagicValueValueType? valueType = null,
        MagicValueCategory? category = null,
        int minOccurrences = 1,
        int maxResults = 50,
        HashSet<int>? ignoreNumbers = null,
        MagicValueAdvisoryRunOptions? options = null)
    {
        using var testSolution = CreateSolution(file);
        return await RunAsync(testSolution.Solution, new ScanAsyncParams(
            ValueType: valueType,
            Category: category,
            MinOccurrences: minOccurrences,
            MaxResults: maxResults,
            IgnoreNumbers: ignoreNumbers,
            Options: options));
    }

    // ainetlinter-disable MaxMethodParameterCount â€” siehe oben (Aufrufstellen-Komfort).
    internal static Task<MagicValueAdvisoryResult> RunAsync(
        Solution solution,
        MagicValueValueType? valueType = null,
        MagicValueCategory? category = null,
        int minOccurrences = 1,
        int maxResults = 50,
        HashSet<int>? ignoreNumbers = null,
        MagicValueAdvisoryRunOptions? options = null)
    {
        return RunAsync(solution, new ScanAsyncParams(
            ValueType: valueType,
            Category: category,
            MinOccurrences: minOccurrences,
            MaxResults: maxResults,
            IgnoreNumbers: ignoreNumbers,
            Options: options));
    }

    internal static async Task<MagicValueAdvisoryResult> RunAsync(
        Solution solution,
        ScanAsyncParams p)
    {
        var options = p.Options ?? new MagicValueAdvisoryRunOptions();
        return await MagicValueAdvisoryScanner.ScanAsync(new MagicValueAdvisoryScannerParameters(
            Solution: solution,
            ScopeFilter: p.ScopeFilter,
            ValueType: p.ValueType,
            Category: p.Category,
            MinOccurrences: p.MinOccurrences,
            MaxResults: p.MaxResults,
            IgnoreNumbers: p.IgnoreNumbers?.ToArray(),
            IncludeTests: options.IncludeTests,
            IncludeSuppressed: options.IncludeSuppressed,
            ChangedOnly: options.ChangedOnly,
            CancellationToken: CancellationToken.None));
    }

    internal static RoslynTestSolution CreateSolution(params (string fileName, string content)[] files) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\MagicValueAdvisoryScannerTests.slnx",
            new ProjectSpec("TestProject", files, VirtualProjectDirectory: "."));
}

/// <summary>Bool-Parameter-Object fuer <see cref="MagicValueAdvisoryTestHelpers.RunAsync"/> â€”
/// buendelt die drei Bool-Flags (includeSuppressed/includeTests/changedOnly) in einem
/// Record, damit die Helper-Methoden das <c>MaxBoolParameterCount: 1</c>-Limit (siehe
/// Linter-Regel <c>MaxBoolParameterCount</c>) einhalten. <see langword="null"/> und <c>default</c> bedeuten
/// "alle drei Flags aus".</summary>
internal sealed record MagicValueAdvisoryRunOptions(
    bool IncludeSuppressed = false,
    bool IncludeTests = false,
    bool ChangedOnly = false)
{
    /// <summary>Impliziter Konvertierungs-Operator von <see langword="bool"/> (Stil mit
    /// positionalem <c>includeSuppressed: true</c>) auf den neuen Options-Record. Erlaubt
    /// Aufrufer-kompatible Uebergaenge, ohne den neuen Stil zu erzwingen â€” die bestehenden
    /// Tests koennen weiterhin <c>includeSuppressed: true</c> schreiben, der Wert landet
    /// automatisch in <see cref="IncludeSuppressed"/>.</summary>
    public static implicit operator MagicValueAdvisoryRunOptions(bool includeSuppressed) =>
        new(IncludeSuppressed: includeSuppressed);
}

/// <summary>Hilfs-Record fuer <see cref="MagicValueAdvisoryTestHelpers.RunAsync(Solution, ScanAsyncParams)"/>
/// â€” buendelt die Konfigurations-Felder in einem Parameter-Object, damit die Methoden-Signatur
/// das <c>MaxMethodParameterCount: 4</c>-Limit (siehe Linter-Regel <c>MaxMethodParameterCount</c>) einhaelt. Bewusst
/// auf Top-Level statt nested, weil <c>BanPublicNestedTypes</c> auch <c>internal</c> nested Typen
/// verbietet (Ausnahme nur fuer <c>private</c>) â€” und der Record <c>internal</c> sein muss, damit
/// die Test-Klassen ihn ueber ihre <c>RunAsync(... ScanAsyncParams)</c>-Aufrufe konstruieren koennen.
/// <para><c>Options</c> ersetzt die drei bool-Felder am
/// <see cref="MagicValueAdvisoryTestHelpers.RunAsync"/>-Helper. Aufrufer koennen weiterhin
/// <c>includeSuppressed: true</c> schreiben â€” der Wert wird via impliziter Konvertierung in
/// <see cref="MagicValueAdvisoryRunOptions"/> ueberfuehrt.</para></summary>
internal sealed record ScanAsyncParams(
    string? ScopeFilter = null,
    MagicValueValueType? ValueType = null,
    MagicValueCategory? Category = null,
    int MinOccurrences = 1,
    int MaxResults = 50,
    HashSet<int>? IgnoreNumbers = null,
    MagicValueAdvisoryRunOptions? Options = null);

