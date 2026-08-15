#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// Rohe, noch ungeparste <c>find_magic_values</c>-Toolargumente vor der Validierung in
/// <see cref="FindMagicValuesTool.ExecuteAsync"/>. Stellt alle 9 Konfigurations-Felder
/// bereit, die das Tool akzeptiert. <c>IncludeSuppressed</c>, <c>IncludeTests</c>
/// und <c>ChangedOnly</c> sind wirksam (siehe <c>FindMagicValuesScanner</c> fuer die
/// jeweilige Implementierung).
/// </summary>
internal sealed record FindMagicValuesToolArgs(
    string? ScopeFilter,
    string ValueType,
    string CategoryFilter,
    int MinOccurrences,
    int MaxResults,
    int[]? IgnoreNumbers,
    bool IncludeTests,
    bool IncludeSuppressed,
    bool ChangedOnly);

/// <summary>
/// MCP-Tool <c>find_magic_values</c>: fuehrt einen On-Demand-Audit ueber alle
/// <c>.cs</c>-Dokumente der geladenen Solution durch, klassifiziert Literale (Strings/Zahlen)
/// nach fachlichen Refactoring-Zielen (URLs, Pfade, Timeouts, Format-Strings, Schwellenwerte,
/// HTTP-Statuscodes) und liefert strukturierte Empfehlungen. Bewusst duenner Dispatch auf
/// <see cref="FindMagicValuesScanner.ScanAsync"/>: Parameter-Validierung hier (analog
/// <see cref="Mcp.Tools.PatternDetect.PatternDetectTool"/> und
/// <see cref="Mcp.Tools.Analysis.MetricsTree.MetricsTreeTool"/>), Scan-/Aggregationslogik im
/// Scanner.
/// </summary>
internal static class FindMagicValuesTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, FindMagicValuesToolArgs args, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var valueTypeResult = ResolveValueType(args.ValueType);
        if (valueTypeResult.Error is not null) return valueTypeResult.Error;

        var categoryResult = ResolveCategory(args.CategoryFilter);
        if (categoryResult.Error is not null) return categoryResult.Error;

        // minOccurrences und maxResults werden defensiv geclamped, nicht als INVALID_ARGUMENT
        // abgelehnt (Clamp statt reject). Sucht der Agent nach minOccurrences=0 oder
        // maxResults=0, bekommt er ein sinnvolles Default-Ergebnis statt eines Formalfehlers —
        // harte Ablehnung würde den Agenten ohne Alternative dastehen lassen.
        var minOccurrences = Math.Max(1, args.MinOccurrences);
        var maxResults = Math.Max(1, args.MaxResults);

        FindMagicValuesResult result;
        try
        {
            // Task.Run umschliesst den CPU-/IO-bound Scan, damit der McpCodeGraphServer-Lock
            // nicht unnoetig gehalten wird (Pattern 1:1 von SearchPatternTool.cs:56-58).
            result = await Task.Run(
                () => FindMagicValuesScanner.ScanAsync(new FindMagicValuesScannerParameters(
                    Solution: solution,
                    ScopeFilter: string.IsNullOrWhiteSpace(args.ScopeFilter) ? null : args.ScopeFilter,
                    ValueType: valueTypeResult.Value,
                    Category: categoryResult.Category,
                    MinOccurrences: minOccurrences,
                    MaxResults: maxResults,
                    IgnoreNumbers: args.IgnoreNumbers,
                    IncludeTests: args.IncludeTests,
                    IncludeSuppressed: args.IncludeSuppressed, // siehe Classifier
                    ChangedOnly: args.ChangedOnly,               // siehe Scanner
                    CancellationToken: ct)),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.Error(
                LinterErrorCodes.AnalysisFailed,
                "Unerwarteter Fehler beim Magic-Value-Scan.",
                context: ex.Message,
                hint: "Einmal erneut versuchen — bleibt der Fehler bestehen, LinterEngine-Log pruefen.");
        }

        // Echte Malfunction (z. B. unerwartete Exception in der Roslyn-Iteration) — IsError=true
        // mit Retry-once-Hinweis, siehe IsErrorPolicy.md (Pattern 1:1 von PatternDetectTool).
        if (result.IsMalfunction)
        {
            return McpToolResults.Error(
                LinterErrorCodes.AnalysisFailed,
                "Unerwarteter Fehler beim Magic-Value-Scan.",
                context: result.Context,
                hint: "Einmal erneut versuchen — bleibt der Fehler bestehen, LinterEngine-Log pruefen.");
        }

        // "Keine Dateien im Scope" hat keinen strukturierten Payload (kein Report gebaut) —
        // Text-only, analog get_violations/pattern_detect.
        if (result.Payload is null)
        {
            return McpToolResults.Text(result.Text);
        }

        // StructuredContent als Objekt-Wrapper (NICHT das nackte Array) — siehe McpToolResults.Text<T>-Doc.
        return McpToolResults.Text(
            result.Text,
            new { MagicValues = result.Payload.MagicValues, Summary = result.Payload.Summary });
    }

    private readonly struct ValueTypeResolution
    {
        public ValueTypeResolution(MagicValueValueType? value, CallToolResult? error)
        {
            Value = value;
            Error = error;
        }

        public MagicValueValueType? Value { get; }
        public CallToolResult? Error { get; }
    }

    private readonly struct CategoryResolution
    {
        public CategoryResolution(MagicValueCategory? category, CallToolResult? error)
        {
            Category = category;
            Error = error;
        }

        public MagicValueCategory? Category { get; }
        public CallToolResult? Error { get; }
    }

    /// <summary>Parst den <c>valueType</c>-String. <see langword="null"/> Value signalisiert
    /// 'all' (Default); unbekannte Werte liefern ein recoverable <c>INVALID_ARGUMENT</c>.</summary>
    private static ValueTypeResolution ResolveValueType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new ValueTypeResolution(null, null);
        return raw.Trim().ToLowerInvariant() switch
        {
            "all" => new ValueTypeResolution(null, null),
            "strings" => new ValueTypeResolution(MagicValueValueType.String, null),
            "numbers" => new ValueTypeResolution(MagicValueValueType.Number, null),
            _ => new ValueTypeResolution(
                null,
                McpToolResults.InvalidArgument(
                    $"Unbekannter valueType '{raw}'. Gueltige Werte: all, strings, numbers.",
                    hint: "valueType korrigieren.")),
        };
    }

    /// <summary>Parst den <c>categoryFilter</c>-String. <see langword="null"/> Category
    /// signalisiert 'all' (Default); unbekannte Werte liefern recoverable <c>INVALID_ARGUMENT</c>
    /// mit Hint-Liste der gueltigen Kategorien.</summary>
    private static CategoryResolution ResolveCategory(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return new CategoryResolution(null, null);
        }

        var normalized = raw.Trim().ToLowerInvariant();
        return normalized switch
        {
            "config_candidates" => new CategoryResolution(MagicValueCategory.ConfigCandidates, null),
            "constant_candidates" => new CategoryResolution(MagicValueCategory.ConstantCandidates, null),
            "enum_candidates" => new CategoryResolution(MagicValueCategory.EnumCandidates, null),
            "nameof_candidates" => new CategoryResolution(MagicValueCategory.NameofCandidates, null),
            "localization_candidates" => new CategoryResolution(MagicValueCategory.LocalizationCandidates, null),
            "standard_candidates" => new CategoryResolution(MagicValueCategory.StandardCandidates, null),
            "security_candidates" => new CategoryResolution(MagicValueCategory.SecurityCandidates, null),
            _ => new CategoryResolution(
                null,
                McpToolResults.InvalidArgument(
                    $"Unbekannter categoryFilter '{raw}'. Gueltige Werte: {MagicValueCategoryExtensions.AllCategoryIds()}.",
                    hint: "categoryFilter korrigieren.")),
        };
    }
}
