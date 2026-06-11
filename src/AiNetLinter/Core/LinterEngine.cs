using System.Collections.Concurrent;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

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
                // Richtlinien-konform: Keine Exceptions lautlos schlucken, sondern mindestens melden.
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

        if (extension is ".csproj" or ".slnx" or ".sln")
        {
            var dir = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(dir) 
                ? Array.Empty<string>() 
                : GetCsFilesFromDirectory(dir);
        }

        return Array.Empty<string>();
    }

    private static IEnumerable<string> GetCsFilesFromDirectory(string directory)
    {
        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !file.Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"));
    }
}
