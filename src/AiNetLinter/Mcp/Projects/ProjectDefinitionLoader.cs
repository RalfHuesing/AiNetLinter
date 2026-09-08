#nullable enable

namespace AiNetLinter.Mcp.Projects;

/// <summary>
/// Lädt die Definition für eine konkrete, bereits aufgelöste Solution-Datei.
/// Es gibt keine Projektdefinitionsdatei und keine Pfadsuche: Die optionale
/// Regeldatei liegt ausschließlich direkt neben der adressierten Solution.
/// </summary>
internal static class ProjectDefinitionLoader
{
    internal const string RulesFileName = "ainetlinter-rules.json";

    internal static ProjectDefinitionLoadResult LoadSolutionTarget(string? solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return Fail(
                ProjectErrorCodes.ProjectRootRequired,
                "Der Parameter 'targetPath' ist erforderlich; übergib den absoluten Pfad einer vorhandenen .sln- oder .slnx-Datei.");
        }

        var canonicalSolutionPath = Canonicalize(solutionPath);
        if (canonicalSolutionPath is null
            || !File.Exists(canonicalSolutionPath)
            || !IsSolutionPath(canonicalSolutionPath))
        {
            return Fail(
                ProjectErrorCodes.SolutionNotFound,
                $"Solution-Datei nicht gefunden oder nicht unterstützt: '{solutionPath}'. " +
                "Erforderlich ist der absolute Pfad einer vorhandenen .sln- oder .slnx-Datei.");
        }

        var rulesPath = Path.Combine(
            Path.GetDirectoryName(canonicalSolutionPath)!,
            RulesFileName);
        return ProjectDefinitionLoadResult.Success(
            new ProjectDefinition(canonicalSolutionPath, File.Exists(rulesPath) ? rulesPath : string.Empty));
    }

    // Interne Übergangskompatibilität für noch nicht migrierte Aufrufer; auch dieser
    // Einstieg akzeptiert ausschließlich den konkreten Solution-Dateipfad.
    internal static ProjectDefinitionLoadResult Load(string? solutionPath) =>
        LoadSolutionTarget(solutionPath);

    private static string? Canonicalize(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path)
                ? Path.GetFullPath(path)
                : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsSolutionPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".sln" or ".slnx";

    private static ProjectDefinitionLoadResult Fail(string errorCode, string message) =>
        ProjectDefinitionLoadResult.Failure(errorCode, message);
}
