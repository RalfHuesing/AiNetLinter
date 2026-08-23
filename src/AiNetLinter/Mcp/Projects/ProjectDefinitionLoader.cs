#nullable enable

using System.Text.Json;

namespace AiNetLinter.Mcp.Projects;

/// <summary>
/// Lädt <c>ainetlinter.project.json</c> aus einem Projektroot: beide Felder sind Pflicht,
/// relative Pfade werden ausschließlich relativ zur Definitionsdatei aufgelöst (nie zum
/// cwd), beide Zieldateien werden auf Existenz geprüft — ohne Fallback und ohne Raten.
/// Erwartbare Fehler kehren als <see cref="ProjectDefinitionLoadResult.Failed"/> zurück,
/// nie als Exception.
/// </summary>
internal static class ProjectDefinitionLoader
{
    internal const string DefinitionFileName = "ainetlinter.project.json";

    internal static ProjectDefinitionLoadResult Load(string? projectRoot)
    {
        var definitionPath = Path.Combine(projectRoot ?? string.Empty, DefinitionFileName);

        if (!File.Exists(definitionPath))
            return Fail(ProjectErrorCodes.ProjectNotInitialized, NotInitializedTemplate(definitionPath));

        var parsed = TryParseObject(definitionPath);
        if (parsed.Error is { } parseError)
            return Fail(ProjectErrorCodes.ProjectDefinitionInvalid, parseError);

        var fields = ReadFields(parsed.Value!, definitionPath);
        if (fields.Error is { } fieldError)
            return Fail(ProjectErrorCodes.ProjectDefinitionInvalid, fieldError);

        var solutionPath = ResolveAgainstDefinition(fields.Value!.Solution, definitionPath);
        if (!File.Exists(solutionPath))
            return Fail(ProjectErrorCodes.SolutionNotFound, SolutionMissing(solutionPath, definitionPath));

        var rulesPath = ResolveAgainstDefinition(fields.Value.Rules, definitionPath);
        if (!File.Exists(rulesPath))
            return Fail(ProjectErrorCodes.RulesNotFound, RulesMissing(rulesPath, definitionPath));

        return ProjectDefinitionLoadResult.Success(new ProjectDefinition(solutionPath, rulesPath));
    }

    private static Outcome<JsonElement> TryParseObject(string definitionPath)
    {
        string json;
        try
        {
            json = File.ReadAllText(definitionPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Outcome<JsonElement>.Fail($"Project definition '{definitionPath}' could not be read: {ex.Message}");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Outcome<JsonElement>.Fail(Invalid(definitionPath, "the root must be a JSON object."));

            return Outcome<JsonElement>.Succeed(document.RootElement.Clone());
        }
        catch (JsonException ex)
        {
            return Outcome<JsonElement>.Fail(Invalid(definitionPath, $"not valid JSON ({ex.Message})."));
        }
    }

    private static Outcome<(string Solution, string Rules)> ReadFields(JsonElement root, string definitionPath)
    {
        var solution = RequiredString(root, "solution", definitionPath);
        if (solution.Error is { } solutionError)
            return Outcome<(string, string)>.Fail(solutionError);

        var rules = RequiredString(root, "rules", definitionPath);
        if (rules.Error is { } rulesError)
            return Outcome<(string, string)>.Fail(rulesError);

        return Outcome<(string Solution, string Rules)>.Succeed((solution.Value!, rules.Value!));
    }

    private static Outcome<string> RequiredString(JsonElement root, string fieldName, string definitionPath)
    {
        if (!root.TryGetProperty(fieldName, out var property))
            return Outcome<string>.Fail(Invalid(definitionPath, $"required field '{fieldName}' is missing."));

        if (property.ValueKind != JsonValueKind.String
            || property.GetString() is not { } value
            || value.Trim().Length == 0)
        {
            return Outcome<string>.Fail(Invalid(definitionPath, $"field '{fieldName}' must be a non-empty string."));
        }

        return Outcome<string>.Succeed(value);
    }

    private static string ResolveAgainstDefinition(string value, string definitionPath)
    {
        if (Path.IsPathRooted(value))
            return value;

        var definitionDirectory = Path.GetDirectoryName(definitionPath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(definitionDirectory, value));
    }

    private static string NotInitializedTemplate(string definitionPath) =>
        string.Join(
            Environment.NewLine,
            $"No project definition found at '{definitionPath}'.",
            $"Create {definitionPath} with:",
            "{",
            "  \"solution\": \"<path/to/your.slnx or .sln>\",  // relative to this file, or absolute",
            "  \"rules\":    \"<path/to/rules.json>\"          // relative to this file, or absolute; MUST exist",
            "}",
            "Then retry the call with the same projectRoot.");

    private static ProjectDefinitionLoadResult Fail(string errorCode, string message) =>
        ProjectDefinitionLoadResult.Failure(errorCode, message);

    private static string Invalid(string definitionPath, string detail) =>
        $"Project definition '{definitionPath}' is invalid: {detail}";

    private static string SolutionMissing(string resolvedPath, string definitionPath) =>
        $"Solution file not found: '{resolvedPath}' (resolved relative to the project definition at '{definitionPath}').";

    private static string RulesMissing(string resolvedPath, string definitionPath) =>
        $"Rules file not found: '{resolvedPath}' (resolved relative to the project definition at "
        + $"'{definitionPath}'; no neighbor search, no default rules).";

    private sealed record Outcome<T>(T? Value, string? Error)
    {
        internal static Outcome<T> Succeed(T value) => new(value, null);

        internal static Outcome<T> Fail(string error) => new(default, error);
    }
}
