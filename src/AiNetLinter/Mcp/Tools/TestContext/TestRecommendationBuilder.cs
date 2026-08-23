#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Core;

namespace AiNetLinter.Mcp.Tools.TestContext;

/// <summary>
/// Gemeinsame Quelle der direkt ausfuehrbaren <c>dotnet test</c>-Befehle fuer
/// <c>get_test_context</c> und die <c>recommendedTestCommands</c> der change-context-Antwort:
/// je betroffenem Testprojekt GENAU EIN Befehl, dessen Filter die Vereinigung aller
/// Trefferklassen des Projekts enthaelt. Projekte und Klassennamen sind ordinal-alphabetisch
/// sortiert, damit die Ausgabe deterministisch ist.
/// </summary>
internal static class TestRecommendationBuilder
{
    /// <summary>
    /// Baut aus den zugeordneten Testdateien deduplizierte Befehle: mehrere Treffer im
    /// selben Testprojekt ergeben genau einen Befehl je Projekt.
    /// </summary>
    public static IReadOnlyList<string> BuildDotNetTestCommands(IReadOnlyList<TestFileCoverageResult> testFiles)
    {
        var classNamesByProject = GroupClassNamesByProject(testFiles);
        var commands = new List<string>(classNamesByProject.Count);

        foreach (var pair in classNamesByProject.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            commands.Add(BuildCommand(pair.Key, pair.Value));
        }

        return commands;
    }

    private static Dictionary<string, List<string>> GroupClassNamesByProject(
        IReadOnlyList<TestFileCoverageResult> testFiles)
    {
        var classNamesByProject = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var file in testFiles)
        {
            if (string.IsNullOrWhiteSpace(file.TestClassName)) continue;

            var projectDir = NormalizeProjectDirectory(file.ProjectDirectory);
            if (!classNamesByProject.TryGetValue(projectDir, out var classNames))
            {
                classNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                classNamesByProject[projectDir] = classNames;
            }

            classNames.Add(file.TestClassName);
        }

        return classNamesByProject.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.OrderBy(name => name, StringComparer.Ordinal).ToList());
    }

    private static string BuildCommand(string projectDir, IReadOnlyList<string> classNames)
    {
        // Ab zwei Klassen enthaelt der Filter ein | und muss als Shell-Zeile gequotet sein.
        var filterValue = string.Join("|", classNames.Select(name => $"FullyQualifiedName~{name}"));
        var filter = classNames.Count > 1 ? $"\"{filterValue}\"" : filterValue;
        return projectDir.Length == 0
            ? $"dotnet test --filter {filter}"
            : $"dotnet test {projectDir} --filter {filter}";
    }

    private static string NormalizeProjectDirectory(string? projectDirectory) =>
        string.IsNullOrWhiteSpace(projectDirectory) || projectDirectory == "."
            ? string.Empty
            : projectDirectory;
}
