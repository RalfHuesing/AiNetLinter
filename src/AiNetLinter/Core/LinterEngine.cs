using System.Collections.Concurrent;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

[assembly: InternalsVisibleTo("AiNetLinter.Tests")]

namespace AiNetLinter.Core;

/// <summary>
/// Koordiniert die Auffindung und parallele Analyse von C#-Dateien.
/// </summary>
public sealed class LinterEngine
{
    private readonly LinterConfig _config;

    public LinterEngine(LinterConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Führt die Analyse auf dem angegebenen Pfad aus und liefert alle Regelverstöße.
    /// </summary>
    public IReadOnlyCollection<RuleViolation> Run(string path)
    {
        var files = ResolveFiles(path);
        var violations = new ConcurrentBag<RuleViolation>();

        Parallel.ForEach(files, file =>
        {
            try
            {
                var content = File.ReadAllText(file);
                var fileViolations = LinterAnalyzer.Analyze(file, content, _config);
                foreach (var violation in fileViolations)
                {
                    violations.Add(violation);
                }
            }
            catch (Exception ex)
            {
                violations.Add(new RuleViolation
                {
                    FilePath = file,
                    LineNumber = 1,
                    RuleName = "System.IO.Exception",
                    Details = $"Fehler beim Lesen der Datei: {ex.Message}",
                    Guidance = "Stelle sicher, dass die Datei zugreifbar ist und die Berechtigungen stimmen."
                });
            }
        });

        return violations.ToArray();
    }

    private static IEnumerable<string> ResolveFiles(string path)
    {
        if (Directory.Exists(path))
        {
            return GetCsFilesFromDirectory(path);
        }

        if (!File.Exists(path))
        {
            return Array.Empty<string>();
        }

        return ResolveSingleFile(path);
    }

    private static IEnumerable<string> ResolveSingleFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".cs")
        {
            return new[] { path };
        }

        return ResolveProjectOrSolution(path, extension);
    }

    private static IEnumerable<string> ResolveProjectOrSolution(string path, string extension)
    {
        if (extension == ".csproj")
        {
            return ParseCsproj(path);
        }

        if (extension == ".slnx")
        {
            return ParseSlnx(path).SelectMany(ParseCsproj);
        }

        if (extension == ".sln")
        {
            return ParseSln(path).SelectMany(ParseCsproj);
        }

        return Array.Empty<string>();
    }

    internal static IEnumerable<string> ParseSlnx(string slnxPath)
    {
        var dir = Path.GetDirectoryName(slnxPath) ?? "";
        var projects = new List<string>();

        try
        {
            var content = File.ReadAllText(slnxPath);
            var doc = XDocument.Parse(content);
            var projectElements = doc.Descendants("Project");
            foreach (var elem in projectElements)
            {
                var relPath = elem.Attribute("Path")?.Value;
                if (!string.IsNullOrEmpty(relPath))
                {
                    projects.Add(Path.GetFullPath(Path.Combine(dir, relPath)));
                }
            }
        }
        catch
        {
            throw;
        }

        return projects;
    }

    internal static IEnumerable<string> ParseSln(string slnPath)
    {
        var dir = Path.GetDirectoryName(slnPath) ?? "";
        var projects = new List<string>();

        try
        {
            var lines = File.ReadLines(slnPath);
            foreach (var line in lines)
            {
                var projectPath = ExtractProjectPathFromSlnLine(line);
                if (projectPath != null)
                {
                    projects.Add(Path.GetFullPath(Path.Combine(dir, projectPath)));
                }
            }
        }
        catch
        {
            throw;
        }

        return projects;
    }

    private static string? ExtractProjectPathFromSlnLine(string line)
    {
        if (!line.StartsWith("Project("))
        {
            return null;
        }

        var parts = line.Split(',');
        if (parts.Length < 2)
        {
            return null;
        }

        var pathPart = parts[1].Trim(' ', '"', '\\', '/');
        return pathPart.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    internal static IEnumerable<string> ParseCsproj(string csprojPath)
    {
        var projectDir = Path.GetDirectoryName(csprojPath) ?? "";
        var allFiles = GetCsFilesFromDirectory(projectDir);
        var excludedFiles = GetExcludedFiles(csprojPath, projectDir);

        return allFiles.Where(file => !excludedFiles.Contains(file));
    }

    internal static HashSet<string> GetExcludedFiles(string csprojPath, string projectDir)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var content = File.ReadAllText(csprojPath);
            var doc = XDocument.Parse(content);
            var compileElements = doc.Descendants("Compile");

            foreach (var elem in compileElements)
            {
                AddExcludedPath(elem, projectDir, excluded);
            }
        }
        catch
        {
            // Ignoriere XML-Parsing-Fehler oder leere Dateien
        }
        return excluded;
    }

    private static void AddExcludedPath(XElement elem, string projectDir, HashSet<string> excluded)
    {
        var removePath = elem.Attribute("Remove")?.Value ?? elem.Attribute("Exclude")?.Value;
        if (!string.IsNullOrEmpty(removePath))
        {
            var fullPath = Path.GetFullPath(Path.Combine(projectDir, removePath));
            excluded.Add(fullPath);
        }
    }

    private static IEnumerable<string> GetCsFilesFromDirectory(string directory)
    {
        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !file.Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"));
    }
}
