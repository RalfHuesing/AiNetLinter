#nullable enable

namespace AiNetLinter.Mcp.Projects;

/// <summary>
/// Unveränderlicher Werttyp einer geladenen Projektdefinition. Beide Pfade sind absolut
/// und existenzgeprüft — die Garantie dafür gibt der Loader, nicht der Record selbst.
/// </summary>
internal sealed record ProjectDefinition(string SolutionPath, string RulesPath);
