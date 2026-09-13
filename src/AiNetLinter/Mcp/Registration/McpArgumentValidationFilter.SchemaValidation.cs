#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Registration;

internal static partial class McpArgumentValidationFilter
{
    internal static CallToolResult ProjectFilterError(
        CallToolResult validationError,
        AnalysisTarget? target,
        string? targetPath) =>
        target is not null
            ? McpToolResults.WithNavigation(validationError, target)
            : McpToolResults.WithNavigation(validationError, targetPath);

    internal static CallToolResult? ValidateArguments(
        string toolName,
        JsonElement inputSchema,
        IDictionary<string, JsonElement> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!inputSchema.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var unknownError = CreateUnknownArgumentError(toolName, properties, arguments);
        if (unknownError is not null) return unknownError;

        var requiredError = ValidateRequiredArguments(inputSchema, arguments);
        if (requiredError is not null) return requiredError;

        foreach (var argument in arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var argumentError = ValidateArgument(toolName, properties, argument, arguments);
            if (argumentError is not null) return argumentError;
        }

        return null;
    }

    private static CallToolResult? CreateUnknownArgumentError(
        string toolName,
        JsonElement properties,
        IDictionary<string, JsonElement> arguments)
    {
        if (toolName == "get_symbol_body"
            && arguments.ContainsKey("symbolIdentifier")
            && !properties.TryGetProperty("symbolIdentifier", out _))
        {
            return McpToolResults.InvalidArgument(
                "get_symbol_body verwendet keinen einzelnen Parameter 'symbolIdentifier'.",
                "symbolIdentifiers: [\"h:…\"] für einen oder mehrere Werte verwenden.",
                "$.symbolIdentifier");
        }

        return TargetPathToolRegistrationOptions.RejectUnknownArguments(properties, arguments);
    }
}
