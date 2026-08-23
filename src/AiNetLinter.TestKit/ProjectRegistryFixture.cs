#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp.Projects;

namespace AiNetLinter.TestKit;

/// <summary>
/// Gemeinsame Infrastruktur fuer Registry-Wiring-Tests: legt Projektroots mit
/// Definitionsdatei (<c>ainetlinter.project.json</c>) an und baut Registries mit
/// injizierbaren Server-Fabriken bzw. reine Inspektions-Registries ohne Fabrik-Aufruf.
/// </summary>
internal static class ProjectRegistryFixture
{
    internal const string DefaultRulesContent = "{ \"Global\": {}, \"Metrics\": {} }";

    /// <summary>Absolute Definitionsdatei fuer ein Wurzelverzeichnis anlegen bzw. ergaenzen.
    /// Findet die erste .slnx/.sln im Verzeichnis und stellt sicher, dass eine rules.json
    /// existiert (Default-Inhalt bei Fehlen). Fuer Subprozess-Fixtures ohne TempDir-Handle.</summary>
    public static void EnsureDefinitionsFile(string rootPath)
    {
        var solution = Directory.GetFiles(rootPath, "*.slnx")
            .Concat(Directory.GetFiles(rootPath, "*.sln"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Keine Solution in {rootPath} gefunden.");
        var rulesPath = Path.Combine(rootPath, "rules.json");
        if (!File.Exists(rulesPath))
        {
            File.WriteAllText(rulesPath, DefaultRulesContent);
        }
        File.WriteAllText(
            Path.Combine(rootPath, "ainetlinter.project.json"),
            JsonSerializer.Serialize(new { solution = Path.GetFileName(solution), rules = "rules.json" }));
    }

    /// <summary>Projektroot mit Solution-, Regeldatei- und Definitionsdatei-Stubs anlegen.</summary>
    public static string CreateProjectRoot(
        TestTempDirectory tempDir,
        string name,
        string solutionFile = "app.slnx",
        string rulesRelative = "rules.json",
        string rulesContent = DefaultRulesContent)
    {
        var root = Path.Combine(tempDir.DirectoryPath, name);
        tempDir.CreateFile(Path.Combine(name, solutionFile), string.Empty);
        if (!string.IsNullOrEmpty(rulesRelative))
        {
            tempDir.CreateFile(
                Path.Combine(name, rulesRelative.Replace('/', Path.DirectorySeparatorChar)),
                rulesContent);
        }
        tempDir.CreateFile(
            Path.Combine(name, "ainetlinter.project.json"),
            JsonSerializer.Serialize(new { solution = solutionFile, rules = rulesRelative }));
        return root;
    }

    /// <summary>Registry nur fuer Tool-Schema-/Registrierungs-Inspektion: Die Fabrik wird nie
    /// aufgerufen, weil kein Lease geoeffnet wird.</summary>
    public static ProjectRegistry CreateInspectionRegistry() =>
        Create(_ => throw new InvalidOperationException("Inspections-Registry erzeugt keine Instanzen."));

    public static ProjectRegistry Create(
        Func<AiNetLinter.Mcp.Projects.ProjectDefinition, ProjectInstanceCreation>? factory = null,
        TimeProvider? clock = null,
        int maxProjects = 4,
        TimeSpan? idleTtl = null) =>
        new(new ProjectRegistryOptions(
            factory ?? (_ => throw new InvalidOperationException("Keine Instanz-Fabrik konfiguriert.")),
            clock ?? TimeProvider.System,
            maxProjects,
            idleTtl ?? default));
}
