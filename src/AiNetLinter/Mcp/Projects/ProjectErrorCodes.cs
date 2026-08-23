#nullable enable

namespace AiNetLinter.Mcp.Projects;

/// <summary>
/// Einzige Quelle der Fehlerverträge der Projektdefinitions-Ebene. Die vier loader-seitigen
/// Codes werden vom <see cref="ProjectDefinitionLoader"/> ausgegeben; die beiden
/// projectRoot-Codes greifen erst mit dem Registry-Wiring.
/// </summary>
internal static class ProjectErrorCodes
{
    internal const string ProjectRootRequired = "PROJECT_ROOT_REQUIRED";
    internal const string ProjectRootInvalid = "PROJECT_ROOT_INVALID";
    internal const string ProjectNotInitialized = "PROJECT_NOT_INITIALIZED";
    internal const string ProjectDefinitionInvalid = "PROJECT_DEFINITION_INVALID";
    internal const string SolutionNotFound = "SOLUTION_NOT_FOUND";
    internal const string RulesNotFound = "RULES_NOT_FOUND";
}
