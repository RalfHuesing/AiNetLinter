#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Validiert JSON-Argumenttypen vor der SDK-Parameterbindung. Dadurch werden
/// erwartbare Bindefehler als recoverable <c>INVALID_ARGUMENT</c> mit Feldpfad
/// ausgegeben statt als generischer SDK-Toolfehler.
/// </summary>
internal static class McpArgumentValidationFilter
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
            ["get_impact"] = Set("maxChangedSymbols", "maxTestsPerSymbol"),
            ["get_file_tree"] = Set("maxDepth", "treeDepth", "maxResponseBytes"),
            ["get_namespace_tree"] = Set("maxResponseBytes"),
            ["get_class_structure"] = Set("maxResponseBytes"),
            ["get_violations"] = Set("contextLines"),
            ["search_pattern"] = Set("maxFiles", "contextLines", "maxResponseBytes"),
            ["search_assembly"] = Set("maxResults", "maxFiles", "contextLines", "maxResponseBytes"),
            ["inspect_assembly"] = Set("maxResults", "maxMembers", "maxResponseBytes"),
            ["find_assembly_extensions"] = Set("maxResults", "maxResponseBytes"),
            ["get_assembly_context"] = Set("maxResults", "maxResponseBytes"),
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> MaximumLimitArgumentsByTool =
        new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal)
        {
            ["get_namespace_tree"] = Limits(("maxResults", 200)),
            ["get_file_tree"] = Limits(("maxResults", 2_000), ("maxDepth", 32), ("treeDepth", 32), ("maxResponseBytes", 64 * 1024)),
            ["get_class_structure"] = Limits(("maxMembers", 200)),
            ["get_hotspots"] = Limits(("maxResults", 200)),
            ["search_pattern"] = Limits(("maxResults", 2_000), ("maxResponseBytes", 64 * 1024)),
            ["metrics_tree"] = Limits(("depth", 5)),
            ["get_feature_context"] = Limits(("maxCallers", 50), ("maxTests", 50)),
            ["get_test_context"] = Limits(("maxResults", 100)),
            ["get_violations"] = Limits(("contextLines", 5)),
            ["search_assembly"] = Limits(("maxResults", 1_000), ("maxFiles", 2_000), ("contextLines", 5), ("maxResponseBytes", 64 * 1024)),
            ["inspect_assembly"] = Limits(("maxResults", 1_000), ("maxMembers", 1_000), ("maxResponseBytes", 64 * 1024)),
            ["find_assembly_extensions"] = Limits(("maxResults", 1_000), ("maxResponseBytes", 64 * 1024)),
            ["get_assembly_context"] = Limits(
                ("maxResults", 1_000),
                ("maxResponseBytes", 64 * 1024),
                ("maxBodyLines", 1_000),
                ("maxCallers", 200),
                ("depth", 3),
                ("topN", 200)),
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
                return validationError ?? await next(context, cancellationToken).ConfigureAwait(false);
            }));
    }

    internal static CallToolResult? Validate(RequestContext<CallToolRequestParams> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.MatchedPrimitive is not McpServerTool tool ||
            !tool.ProtocolTool.InputSchema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var arguments = context.Params?.Arguments
            ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        if (tool.ProtocolTool.InputSchema.TryGetProperty("required", out var required)
            && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var requiredProperty in required.EnumerateArray())
            {
                if (requiredProperty.ValueKind != JsonValueKind.String) continue;
                var name = requiredProperty.GetString();
                if (string.IsNullOrWhiteSpace(name)
                    || (arguments.TryGetValue(name, out var value) && value.ValueKind != JsonValueKind.Null))
                {
                    continue;
                }

                return McpToolResults.InvalidArgument(
                    $"Pflichtargument '{name}' fehlt oder ist null.",
                    $"'{name}' mit dem im tools/list-Schema beschriebenen Wert angeben.",
                    $"$.{name}");
            }
        }

        foreach (var argument in arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!properties.TryGetProperty(argument.Key, out var schema))
            {
                continue;
            }

            if (TryFindTypeMismatch(argument.Value, schema, $"$.{argument.Key}", out var mismatchPath, out var expected, out var actual))
            {
                return McpToolResults.InvalidArgument(
                    $"Argument '{mismatchPath}' hat den JSON-Typ '{actual}', erwartet wird '{expected}'.",
                    $"'{mismatchPath}' als {expected} uebergeben.",
                    mismatchPath);
            }

            // The Git branch of get_impact deliberately ignores depth (both
            // callers and change-context).  Keep that documented no-op
            // backwards compatible: only the symbol branch has a positive
            // depth contract.
            var isIgnoredGitImpactDepth = tool.ProtocolTool.Name == "get_impact"
                && string.Equals(argument.Key, "depth", StringComparison.Ordinal)
                && !HasNonEmptyStringArgument(arguments, "symbolIdentifier");

            if (!isIgnoredGitImpactDepth
                && PositiveLimitArgumentsByTool.TryGetValue(tool.ProtocolTool.Name, out var positiveArguments)
                && positiveArguments.Contains(argument.Key)
                && argument.Value.ValueKind == JsonValueKind.Number
                && argument.Value.TryGetInt32(out var requested)
                && requested < 1)
            {
                return McpToolResults.InvalidArgument(
                    $"{argument.Key} muss mindestens 1 sein.",
                    $"'{argument.Key}' auf einen positiven Wert setzen.",
                    $"$.{argument.Key}");
            }

            if (NonNegativeLimitArgumentsByTool.TryGetValue(tool.ProtocolTool.Name, out var nonNegativeArguments)
                && nonNegativeArguments.Contains(argument.Key)
                && argument.Value.ValueKind == JsonValueKind.Number
                && argument.Value.TryGetInt32(out var nonNegativeRequest)
                && nonNegativeRequest < 0)
            {
                return McpToolResults.InvalidArgument(
                    $"{argument.Key} darf nicht negativ sein.",
                    $"'{argument.Key}' auf 0 oder einen positiven Wert setzen.",
                    $"$.{argument.Key}");
            }

            if (MaximumLimitArgumentsByTool.TryGetValue(tool.ProtocolTool.Name, out var maximumArguments)
                && maximumArguments.TryGetValue(argument.Key, out var maximum)
                && argument.Value.ValueKind == JsonValueKind.Number
                && argument.Value.TryGetInt32(out var boundedRequest)
                && boundedRequest > maximum)
            {
                return McpToolResults.InvalidArgument(
                    $"{argument.Key} darf höchstens {maximum} sein.",
                    $"'{argument.Key}' auf höchstens {maximum} setzen.",
                    $"$.{argument.Key}");
            }
        }

        return null;
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
