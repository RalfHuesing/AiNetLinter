#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.Analysis;

internal static class SearchPatternTool
{
    internal const int DefaultMaxResults = 20;
    internal const int DefaultMaxResponseBytes = 8 * 1024;
    internal const int MaxResultsCap = 2_000;
    internal const int MaxResponseBytesCap = 64 * 1024;

    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string? pattern,
        bool? isRegex,
        int maxResults,
        CancellationToken ct) =>
        ExecuteAsync(
            state,
            new SearchPatternToolArguments(pattern, isRegex, maxResults, 0, 0, DefaultMaxResponseBytes, null, null, null),
            ct);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        SearchPatternToolArguments arguments,
        CancellationToken ct)
    {
        var validation = ValidateArguments(arguments);
        if (validation is not null) return validation;

        var normalizedMaxResults = arguments.MaxResults < 1 ? 1 : arguments.MaxResults;
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var scannerParameters = new SearchPatternScannerParameters(
            solution,
            arguments.Pattern!,
            arguments.IsRegex,
            normalizedMaxResults,
            arguments.MaxFiles,
            arguments.ContextLines,
            arguments.MaxResponseBytes,
            arguments.Scope,
            arguments.IncludePatterns,
            arguments.ExcludePatterns,
            ct,
            arguments.EnrichCSharp,
            arguments.ScopeType);
        SearchPatternScanResult scan;
        try
        {
            scan = await Task.Run(
                () => SearchPatternScannerEnrichment.ScanAsync(scannerParameters),
                ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            var hint = IsInvalidRegexArgument(arguments, ex)
                ? "Pruefe pattern auf gueltige Regex-Syntax."
                : "Pattern, Scope und Filter muessen gueltige solution-relative Werte sein.";
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                ex.Message,
                hint: hint);
        }

        var text = SearchPatternTextFormatter.Format(scan);
        if (scan.Payload.Completeness.CancellationRequested)
        {
            return McpToolResults.Text(text);
        }

        return McpToolResults.Text(text);
    }

    private static CallToolResult? ValidateArguments(SearchPatternToolArguments arguments)
    {
        if (string.IsNullOrEmpty(arguments.Pattern))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "pattern darf nicht leer sein.",
                hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.");
        }

        if (IsInvalidBudget(arguments))
        {
            return McpToolResults.InvalidArgument(
                "maxFiles, contextLines und maxResponseBytes duerfen nicht negativ sein.");
        }

        if (arguments.MaxResults > MaxResultsCap)
        {
            return McpToolResults.InvalidArgument(
                $"maxResults darf {MaxResultsCap} nicht überschreiten.",
                fieldPath: "$.maxResults");
        }

        if (arguments.MaxResponseBytes > MaxResponseBytesCap)
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes darf {MaxResponseBytesCap} nicht überschreiten.",
                fieldPath: "$.maxResponseBytes");
        }

        if (!string.IsNullOrWhiteSpace(arguments.ScopeType)
            && !arguments.ScopeType.Equals("all", StringComparison.OrdinalIgnoreCase)
            && !arguments.ScopeType.Equals("production", StringComparison.OrdinalIgnoreCase)
            && !arguments.ScopeType.Equals("tests", StringComparison.OrdinalIgnoreCase))
        {
            return McpToolResults.InvalidArgument(
                "scopeType muss all, production oder tests sein.",
                fieldPath: "$.scopeType");
        }

        return null;
    }

    private static bool IsInvalidBudget(SearchPatternToolArguments arguments) =>
        arguments.MaxFiles < 0
        || arguments.ContextLines < 0
        || arguments.MaxResponseBytes < 0;

    private static bool IsInvalidRegexArgument(
        SearchPatternToolArguments arguments,
        ArgumentException exception) =>
        string.Equals(exception.ParamName, "pattern", StringComparison.Ordinal)
        || (arguments.IsRegex == true && exception.Message.Contains("Invalid pattern", StringComparison.Ordinal));
}
