#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Mcp.Tools.TestContext;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Validiert JSON-Argumenttypen vor der SDK-Parameterbindung. Dadurch werden
/// erwartbare Bindefehler als recoverable <c>INVALID_ARGUMENT</c> mit Feldpfad
/// ausgegeben statt als generischer SDK-Toolfehler.
/// </summary>
internal static partial class McpArgumentValidationFilter
{
    // Limits are intentionally scoped by tool.  A name-only rule is incorrect:
    // several assembly tools and the change-context branch use 0 as "use the
    // documented default", while the corresponding source tools require a
    // positive explicit limit.
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> PositiveLimitArgumentsByTool =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["find_symbol"] = Set("maxResults"),
            ["find_references"] = Set("maxResults", "depth"),
            ["get_call_tree"] = Set("depth", "topN"),
            ["get_impact"] = Set("maxResults", "depth"),
            ["dependency_graph"] = Set("depth", "maxResults"),
            ["get_type_hierarchy"] = Set("maxResults"),
            ["find_implementations"] = Set("maxResults"),
            ["get_symbol_body"] = Set("maxBodyLines"),
            ["get_class_structure"] = Set("maxMembers"),
            ["get_namespace_tree"] = Set("depth", "maxResults"),
            ["get_file_tree"] = Set("maxResults"),
            ["get_hotspots"] = Set("maxResults"),
            ["get_violations"] = Set("maxResults"),
            ["search_pattern"] = Set("maxResults"),
            ["metrics_tree"] = Set("depth", "topN"),
            ["pattern_detect"] = Set("maxResultsPerPattern"),
            ["find_duplicates"] = Set("maxResults"),
            ["find_dead_code"] = Set("maxResults"),
            ["get_feature_context"] = Set("maxCallers", "maxTests"),
            ["get_test_context"] = Set("maxResults"),
            ["get_assembly_context"] = Set("maxBodyLines", "maxCallers", "depth", "topN"),
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> NonNegativeLimitArgumentsByTool =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["find_symbol"] = Set("maxResponseBytes"),
            ["dependency_graph"] = Set("maxResponseBytes"),
            ["get_call_tree"] = Set("maxResponseBytes"),
            ["get_impact"] = Set("maxChangedSymbols", "maxTestsPerSymbol"),
            ["get_file_tree"] = Set("maxDepth", "treeDepth", "maxResponseBytes"),
            ["get_file_skeleton"] = Set("maxResponseBytes"),
            ["get_namespace_tree"] = Set("maxResponseBytes"),
            ["get_class_structure"] = Set("maxResponseBytes"),
            ["get_violations"] = Set("contextLines"),
            ["search_pattern"] = Set("maxFiles", "contextLines", "maxResponseBytes"),
            ["search_assembly"] = Set("maxResults", "maxFiles", "contextLines", "maxResponseBytes"),
            ["inspect_assembly"] = Set("maxResults", "maxMembers", "maxResponseBytes"),
            ["find_assembly_extensions"] = Set("maxResults", "maxResponseBytes"),
            ["get_assembly_context"] = Set("maxResults", "maxResponseBytes"),
            ["get_feature_context"] = Set("maxResponseBytes"),
            ["get_test_context"] = Set("maxResponseBytes"),
        };

    internal static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> MaximumLimitArgumentsByTool =
        new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal)
        {
            ["find_symbol"] = Limits(("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["dependency_graph"] = Limits(("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["get_call_tree"] = Limits(("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["get_file_tree"] = Limits(
                ("maxResults", GetFileTreeTool.MaxResultsCap),
                ("maxDepth", GetFileTreeTool.MaxDepthCap),
                ("treeDepth", GetFileTreeTool.MaxDepthCap),
                ("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["get_class_structure"] = Limits(
                ("maxMembers", GetClassStructureTool.MaxMembersCap),
                ("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["get_file_skeleton"] = Limits(("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["get_namespace_tree"] = Limits(
                ("maxResults", GetNamespaceTreeTool.MaxResultsCap),
                ("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["get_hotspots"] = Limits(("maxResults", GetHotspotsScanner.MaxResultsCap)),
            ["search_pattern"] = Limits(
                ("maxResults", SearchPatternTool.MaxResultsCap),
                ("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["metrics_tree"] = Limits(("depth", MetricsTreeTool.MaxDepthCap)),
            ["get_feature_context"] = Limits(
                ("maxCallers", FeatureContextScanner.MaxCallersLimit),
                ("maxTests", FeatureContextScanner.MaxTestFilesLimit),
                ("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["get_test_context"] = Limits(
                ("maxResults", GetTestContextTool.MaxResultsCap),
                ("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["get_violations"] = Limits(("contextLines", GetViolationsScanner.MaxContextLines)),
            ["search_assembly"] = Limits(
                ("maxResults", AssemblySearchTool.MaxResultsCap),
                ("maxFiles", GetFileTreeTool.MaxResultsCap),
                ("contextLines", AssemblySearchTool.MaxContextLines),
                ("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["inspect_assembly"] = Limits(
                ("maxResults", AssemblyAnalysisService.MaxResults),
                ("maxMembers", AssemblyAnalysisService.MaxMembers),
                ("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["find_assembly_extensions"] = Limits(
                ("maxResults", AssemblyAnalysisService.MaxResults),
                ("maxResponseBytes", McpResponseBudgetLimits.MaxBytes)),
            ["get_assembly_context"] = Limits(
                ("maxResults", AssemblyAnalysisService.MaxResults),
                ("maxResponseBytes", McpResponseBudgetLimits.MaxBytes),
                ("maxBodyLines", AssemblyAnalysisContextTool.MaxBodyLinesCap),
                ("maxCallers", AssemblyAnalysisContextTool.MaxCallersCap),
                ("depth", AssemblyAnalysisContextTool.MaxDepthCap),
                ("topN", AssemblyAnalysisContextTool.MaxTopNCap)),
        };

    private static IReadOnlySet<string> Set(params string[] names) => names.ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, int> Limits(params (string Name, int Maximum)[] limits) =>
        limits.ToDictionary(item => item.Name, item => item.Maximum, StringComparer.Ordinal);

    internal static void Configure(IMcpServerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next =>
            async (context, cancellationToken) =>
            {
                var validationError = Validate(context);
                if (validationError is not null)
                {
                    return ProjectFilterError(context, validationError);
                }
                var result = await next(context, cancellationToken).ConfigureAwait(false);
                return EnsureNavigationEnvelope(context, result);
            }));
    }

    private static CallToolResult EnsureNavigationEnvelope(
        RequestContext<CallToolRequestParams> context,
        CallToolResult result)
    {
        if (result.StructuredContent is { ValueKind: JsonValueKind.Object } structured
            && structured.TryGetProperty("code", out var code)
            && code.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(code.GetString())
            && !structured.TryGetProperty("navigation", out _))
        {
            return ProjectFilterError(context, result);
        }

        return result;
    }

    private static CallToolResult ProjectFilterError(
        RequestContext<CallToolRequestParams> context,
        CallToolResult validationError)
    {
        string? targetPath = null;
        if (context.Params?.Arguments is not null
            && context.Params.Arguments.TryGetValue("targetPath", out var targetPathElement)
            && targetPathElement.ValueKind == JsonValueKind.String)
        {
            targetPath = targetPathElement.GetString();
        }

        AnalysisTarget? target = null;
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            var resolution = AnalysisTargetResolver.ResolveOptional(new AnalysisTargetRequest(targetPath));
            target = resolution.Target;
        }

        return ProjectFilterError(validationError, target, targetPath);
    }

    internal static CallToolResult? Validate(RequestContext<CallToolRequestParams> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.MatchedPrimitive is not McpServerTool tool)
        {
            return null;
        }

        if (!tool.ProtocolTool.InputSchema.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var arguments = context.Params?.Arguments
            ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        return ValidateArguments(tool.ProtocolTool.Name, tool.ProtocolTool.InputSchema, arguments);
    }

    private static CallToolResult? ValidateRequiredArguments(
        JsonElement inputSchema,
        IDictionary<string, JsonElement> arguments)
    {
        if (!inputSchema.TryGetProperty("required", out var required)
            || required.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var requiredProperty in required.EnumerateArray())
        {
            if (requiredProperty.ValueKind != JsonValueKind.String) continue;
            var name = requiredProperty.GetString();
            if (string.IsNullOrWhiteSpace(name) || HasNonNullArgument(arguments, name)) continue;

            return McpToolResults.InvalidArgument(
                $"Pflichtargument '{name}' fehlt oder ist null.",
                $"'{name}' mit dem im tools/list-Schema beschriebenen Wert angeben.",
                $"$.{name}");
        }

        return null;
    }

    private static CallToolResult? ValidateArgument(
        string toolName,
        JsonElement properties,
        KeyValuePair<string, JsonElement> argument,
        IDictionary<string, JsonElement> arguments)
    {
        if (!properties.TryGetProperty(argument.Key, out var schema)) return null;

        return ValidateType(argument.Key, argument.Value, schema)
            ?? ValidatePositiveLimit(toolName, argument.Key, argument.Value, arguments)
            ?? ValidateNonNegativeLimit(toolName, argument.Key, argument.Value)
            ?? ValidateMinimumResponseBudget(toolName, argument.Key, argument.Value)
            ?? ValidateMaximumLimit(toolName, argument.Key, argument.Value);
    }

    private static CallToolResult? ValidateType(
        string argumentName,
        JsonElement value,
        JsonElement schema)
    {
        if (!TryFindTypeMismatch(
                value, schema, $"$.{argumentName}", out var mismatchPath, out var expected, out var actual))
        {
            return null;
        }

        return McpToolResults.InvalidArgument(
            $"Argument '{mismatchPath}' hat den JSON-Typ '{actual}', erwartet wird '{expected}'.",
            $"'{mismatchPath}' als {expected} uebergeben.",
            mismatchPath);
    }

    private static CallToolResult? ValidatePositiveLimit(
        string toolName,
        string argumentName,
        JsonElement value,
        IDictionary<string, JsonElement> arguments)
    {
        // The Git branch of get_impact deliberately ignores depth (both
        // callers and change-context). Keep that documented no-op backwards
        // compatible: only the symbol branch has a positive depth contract.
        if (toolName == "get_impact"
            && argumentName == "depth"
            && !HasNonEmptyStringArgument(arguments, "symbolIdentifier"))
        {
            return null;
        }

        if (!PositiveLimitArgumentsByTool.TryGetValue(toolName, out var positiveArguments)
            || !positiveArguments.Contains(argumentName)
            || !TryGetInt32Number(value, out var requested)
            || requested >= 1)
        {
            return null;
        }

        return McpToolResults.InvalidArgument(
            $"{argumentName} muss mindestens 1 sein.",
            $"'{argumentName}' auf einen positiven Wert setzen.",
            $"$.{argumentName}");
    }

    private static CallToolResult? ValidateNonNegativeLimit(
        string toolName,
        string argumentName,
        JsonElement value)
    {
        if (!NonNegativeLimitArgumentsByTool.TryGetValue(toolName, out var nonNegativeArguments)
            || !nonNegativeArguments.Contains(argumentName)
            || !TryGetInt32Number(value, out var requested)
            || requested >= 0)
        {
            return null;
        }

        return McpToolResults.InvalidArgument(
            $"{argumentName} darf nicht negativ sein.",
            $"'{argumentName}' auf 0 oder einen positiven Wert setzen.",
            $"$.{argumentName}");
    }

    private static CallToolResult? ValidateMinimumResponseBudget(
        string toolName,
        string argumentName,
        JsonElement value)
    {
        if (argumentName != "maxResponseBytes"
            || toolName is not ("find_symbol" or "find_references" or "get_call_tree"
                or "find_implementations" or "get_type_hierarchy" or "dependency_graph"
                or "get_feature_context" or "get_test_context" or "get_file_skeleton"
                or "get_symbol_body")
            || !TryGetInt32Number(value, out var responseBudget)
            || McpResponseBudgetLimits.IsPublicBudget(responseBudget))
        {
            return null;
        }

        return McpToolResults.InvalidArgument(
            $"{argumentName} muss zwischen {McpResponseBudgetLimits.MinimumStructuredBytes} und {McpResponseBudgetLimits.MaxBytes} Bytes liegen.",
            $"'{argumentName}' weglassen oder einen Wert zwischen {McpResponseBudgetLimits.MinimumStructuredBytes} und {McpResponseBudgetLimits.MaxBytes} setzen.",
            $"$.{argumentName}");
    }

    private static CallToolResult? ValidateMaximumLimit(
        string toolName,
        string argumentName,
        JsonElement value)
    {
        if (!MaximumLimitArgumentsByTool.TryGetValue(toolName, out var maximumArguments)
            || !maximumArguments.TryGetValue(argumentName, out var maximum)
            || !TryGetInt32Number(value, out var requested)
            || requested <= maximum)
        {
            return null;
        }

        return McpToolResults.InvalidArgument(
            $"{argumentName} darf höchstens {maximum} sein.",
            $"'{argumentName}' auf höchstens {maximum} setzen.",
            $"$.{argumentName}");
    }

    private static bool HasNonNullArgument(
        IDictionary<string, JsonElement> arguments,
        string name) =>
        arguments.TryGetValue(name, out var value) && value.ValueKind != JsonValueKind.Null;

    private static bool TryGetInt32Number(JsonElement value, out int result)
    {
        result = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result);
    }

    private static bool HasNonEmptyStringArgument(
        IDictionary<string, JsonElement> arguments,
        string name) =>
        arguments.TryGetValue(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString());

    private static bool TryFindTypeMismatch(
        JsonElement value,
        JsonElement schema,
        string path,
        out string mismatchPath,
        out string expected,
        out string actual)
    {
        mismatchPath = path;
        expected = string.Empty;
        actual = value.ValueKind.ToString().ToLowerInvariant();

        if (schema.TryGetProperty("type", out var type) && !MatchesType(value, type))
        {
            expected = FormatExpectedType(type);
            return true;
        }

        if (schema.TryGetProperty("type", out type)
            && IsIntegerType(type)
            && value.ValueKind == JsonValueKind.Number
            && !value.TryGetInt32(out _))
        {
            expected = "integer (Int32)";
            actual = "number (ausserhalb Int32 oder nicht ganzzahlig)";
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array
            || !schema.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (TryFindTypeMismatch(item, items, $"{path}[{index}]", out mismatchPath, out expected, out actual))
            {
                return true;
            }

            index++;
        }

        return false;
    }

    private static bool MatchesType(JsonElement value, JsonElement type)
    {
        if (type.ValueKind == JsonValueKind.String)
        {
            return MatchesType(value.ValueKind, type.GetString());
        }

        if (type.ValueKind == JsonValueKind.Array)
        {
            return type.EnumerateArray()
                .Any(candidate => candidate.ValueKind == JsonValueKind.String &&
                    MatchesType(value.ValueKind, candidate.GetString()));
        }

        return true;
    }

    private static bool MatchesType(JsonValueKind valueKind, string? type) => type switch
    {
        "array" => valueKind == JsonValueKind.Array,
        "boolean" => valueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" or "number" => valueKind == JsonValueKind.Number,
        "null" => valueKind == JsonValueKind.Null,
        "object" => valueKind == JsonValueKind.Object,
        "string" => valueKind == JsonValueKind.String,
        _ => true,
    };

    private static string FormatExpectedType(JsonElement type)
    {
        if (type.ValueKind == JsonValueKind.String)
        {
            return type.GetString() ?? "unbekannt";
        }

        if (type.ValueKind == JsonValueKind.Array)
        {
            var types = type.EnumerateArray()
                .Where(candidate => candidate.ValueKind == JsonValueKind.String)
                .Select(candidate => candidate.GetString())
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate));
            return string.Join(" oder ", types);
        }

        return "gemaess Schema";
    }

    private static bool IsIntegerType(JsonElement type) =>
        type.ValueKind == JsonValueKind.String
            ? string.Equals(type.GetString(), "integer", StringComparison.Ordinal)
            : type.ValueKind == JsonValueKind.Array
                && type.EnumerateArray().Any(candidate =>
                    candidate.ValueKind == JsonValueKind.String
                    && string.Equals(candidate.GetString(), "integer", StringComparison.Ordinal));
}
