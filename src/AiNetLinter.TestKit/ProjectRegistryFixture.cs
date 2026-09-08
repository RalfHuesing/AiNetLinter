#nullable enable

using System;
using System.IO;
using AiNetLinter.Mcp.Projects;

namespace AiNetLinter.TestKit;

/// <summary>
/// Gemeinsame Infrastruktur fuer Registry-Wiring-Tests: legt konkrete Solution-Ziele
/// mit optionaler benachbarter <c>ainetlinter-rules.json</c> an und baut Registries
/// mit injizierbaren Server-Fabriken bzw. reine Inspektions-Registries ohne Fabrik-Aufruf.
/// </summary>
internal static class ProjectRegistryFixture
{
    internal const string DefaultRulesContent = "{ \"Global\": {}, \"Metrics\": {} }";

    /// <summary>Projektroot mit konkreter Solution und optionaler benachbarter MCP-Regeldatei anlegen.</summary>
    public static string CreateProjectRoot(
        TestTempDirectory tempDir,
        string name,
        string solutionFile = "app.slnx",
        string rulesRelative = "ainetlinter-rules.json",
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
