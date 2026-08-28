#nullable enable

using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Tools;

internal static class McpToolRegistrationOptions
{
    private const string ProjectTargetContract =
        " Zielvertrag: targetType='project' und targetPath als absoluter, kanonischer Projektpfad.";
    private const string AssemblyTargetContract =
        " Zielvertrag: targetType='assembly' und targetPath als absoluter, existierender .dll-Pfad.";

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

    internal static McpServerToolCreateOptions ReadOnlyTool(string name, string description) =>
        Create(name, description + ProjectTargetContract, ReadOnlyValues);

    internal static McpServerToolCreateOptions ReloadConfigTool(string name, string description) =>
        Create(name, description + ProjectTargetContract, ReloadConfigValues);

    internal static McpServerToolCreateOptions AssemblyTool(string name, string description) =>
        Create(name, description + AssemblyTargetContract, ReadOnlyValues);

    internal static McpServerToolCreateOptions ServerHealthTool(string name, string description) =>
        Create(name, description, ReadOnlyValues);

    internal static McpServerToolCreateOptions FeedbackTool(string name, string description) =>
        Create(name, description, FeedbackValues);

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
