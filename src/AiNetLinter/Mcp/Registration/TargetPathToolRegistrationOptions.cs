#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// MCP-Metadaten fuer den oeffentlichen targetPath-only-Vertrag.
/// </summary>
internal static class TargetPathToolRegistrationOptions
{
    private const string SourceTargetContract =
        " Ziel: absolute .sln/.slnx (Source).";

    private const string ProjectAssemblyTargetContract =
        " Ziel: absolute .sln/.slnx (Source) oder .dll/.exe (Assembly).";

    private const string AssemblyTargetContract =
        " Ziel: absolute .dll/.exe.";

    private static readonly AnnotationValues ReadOnlyValues = new(
        ReadOnly: true,
        Destructive: false,
        Idempotent: true,
        OpenWorld: false);

    private static readonly AnnotationValues ReloadConfigValues = new(
        ReadOnly: false,
        Destructive: false,
        Idempotent: true,
        OpenWorld: false);

    private static readonly AnnotationValues FeedbackValues = new(
        ReadOnly: false,
        Destructive: false,
        Idempotent: false,
        OpenWorld: false);

    internal static McpServerToolCreateOptions SourceReadOnlyTool(string name, string description) =>
        Create(name, description + SourceTargetContract, ReadOnlyValues);

    internal static McpServerToolCreateOptions TargetPathReadOnlyTool(string name, string description) =>
        Create(name, description + ProjectAssemblyTargetContract, ReadOnlyValues);

    internal static McpServerToolCreateOptions AssemblyTool(string name, string description) =>
        Create(name, description + AssemblyTargetContract, ReadOnlyValues);

    internal static McpServerToolCreateOptions ReloadConfigTool(string name, string description) =>
        Create(name, description + SourceTargetContract, ReloadConfigValues);

    internal static McpServerToolCreateOptions ServerHealthTool(string name, string description) =>
        Create(name, description, ReadOnlyValues);

    internal static McpServerToolCreateOptions FeedbackTool(string name, string description) =>
        Create(name, description, FeedbackValues);

    internal static CallToolResult? RejectUnknownArguments(
        RequestContext<CallToolRequestParams> context)
    {
        var arguments = context.Params.Arguments;
        if (arguments is null || arguments.Count == 0)
        {
            return null;
        }

        if (context.MatchedPrimitive is not McpServerTool tool
            || !tool.ProtocolTool.InputSchema.TryGetProperty("properties", out var properties)
            || properties.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return McpToolResults.InvalidArgument(
                "Das aktuelle Tool-Schema ist nicht verfügbar.",
                "Nur Argumente aus dem von tools/list gelieferten Schema verwenden.");
        }

        var allowedNames = properties.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        var unknownNames = arguments.Keys
            .Where(name => !allowedNames.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        return unknownNames is not { Length: > 0 }
            ? null
            : McpToolResults.InvalidArgument(
                string.Join("\n", unknownNames.Select(name => $"Unbekanntes Argument: {name}")),
                "Nur Argumente aus dem von tools/list gelieferten Schema verwenden.",
                fieldPath: unknownNames.Length == 1 ? $"$.{unknownNames[0]}" : "$.<argument>");
    }

    internal static async Task<CallToolResult> ExecuteWithUnknownArgumentGuardAsync(
        RequestContext<CallToolRequestParams> context,
        Func<Task<CallToolResult>> execute)
    {
        var unknownError = RejectUnknownArguments(context);
        return unknownError ?? await execute();
    }

    private static McpServerToolCreateOptions Create(
        string name,
        string description,
        AnnotationValues annotations) =>
        new()
        {
            Name = name,
            Description = description,
            ReadOnly = annotations.ReadOnly,
            Destructive = annotations.Destructive,
            Idempotent = annotations.Idempotent,
            OpenWorld = annotations.OpenWorld,
        };

    private readonly record struct AnnotationValues(
        bool ReadOnly,
        bool Destructive,
        bool Idempotent,
        bool OpenWorld);
}
