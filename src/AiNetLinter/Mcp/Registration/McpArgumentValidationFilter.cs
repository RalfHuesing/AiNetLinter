#nullable enable

using System;
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
            context.Params?.Arguments is not { Count: > 0 } arguments ||
            !tool.ProtocolTool.InputSchema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var argument in arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!properties.TryGetProperty(argument.Key, out var schema) ||
                !schema.TryGetProperty("type", out var type) ||
                MatchesType(argument.Value, type))
            {
                continue;
            }

            var expected = FormatExpectedType(type);
            var actual = argument.Value.ValueKind.ToString().ToLowerInvariant();
            return McpToolResults.InvalidArgument(
                $"Argument '{argument.Key}' hat den JSON-Typ '{actual}', erwartet wird '{expected}'.",
                $"'{argument.Key}' als {expected} uebergeben.",
                $"$.{argument.Key}");
        }

        return null;
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
}
