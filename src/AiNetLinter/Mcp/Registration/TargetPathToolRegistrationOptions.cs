#nullable enable

using System;
using System.Linq;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// MCP-Metadaten fuer den oeffentlichen targetPath-only-Vertrag.
/// </summary>
internal static class TargetPathToolRegistrationOptions
{
    private static readonly string[] LegacyArgumentNames =
        ["targetType", "projectRoot", "configPath", "assemblyPath", "ainetlinter.project.json"];

    private const string SourceTargetContract =
        " Zielvertrag: targetPath als absoluter, existierender Pfad einer .sln- oder .slnx-Datei. " +
        "Die Quelle wird aus der Dateiendung bestimmt; Assembly-Ziele sind fuer dieses Tool unsupported.";

    private const string ProjectAssemblyTargetContract =
        " Zielvertrag: targetPath als absoluter, existierender Pfad einer .sln-, .slnx-, .dll- oder .exe-Datei. " +
        "Source- oder Assembly-Route wird aus targetPath bestimmt; Antworten weisen Herkunft, Snapshot/Generation, " +
        "Status und Vollstaendigkeit aus.";

    private const string AssemblyTargetContract =
        " Zielvertrag: targetPath als absoluter, existierender .dll- oder .exe-Pfad.";

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

    internal static CallToolResult? RejectLegacyArguments(
        RequestContext<CallToolRequestParams> context)
    {
        var legacyKeys = context.Params.Arguments?.Keys
            .Where(key => LegacyArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return legacyKeys is not { Length: > 0 }
            ? null
            : McpToolResults.InvalidArgument(
                $"Legacy-Argumente sind im targetPath-only Vertrag nicht zulaessig: {string.Join(", ", legacyKeys)}.",
                "Nur targetPath mit dem absoluten Pfad der konkreten .sln/.slnx/.dll/.exe-Datei uebergeben.");
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
