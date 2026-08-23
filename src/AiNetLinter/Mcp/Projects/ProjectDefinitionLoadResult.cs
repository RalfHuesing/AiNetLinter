#nullable enable

namespace AiNetLinter.Mcp.Projects;

/// <summary>
/// Ergebnis des Ladens einer Projektdefinition (Result-Pattern): entweder eine vollständige,
/// existenzgeprüfte Definition (<see cref="Succeeded"/>) oder ein Fehlervertrag mit Code aus
/// <see cref="ProjectErrorCodes"/> und fertiger Agentenmeldung. Keine Teil-Initialisierung.
/// </summary>
internal sealed record ProjectDefinitionLoadResult(ProjectDefinition? Definition, string? ErrorCode, string? Message)
{
    /// <summary>True, wenn eine vollständige Definition geladen wurde.</summary>
    internal bool Succeeded => Definition is not null;

    internal static ProjectDefinitionLoadResult Success(ProjectDefinition definition) => new(definition, null, null);

    internal static ProjectDefinitionLoadResult Failure(string errorCode, string message) => new(null, errorCode, message);
}
