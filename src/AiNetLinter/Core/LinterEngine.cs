using System.Collections.Concurrent;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using AiNetLinter.Metrics;

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

        // 1. Syntax-Verstöße für jede Datei analysieren
        Parallel.ForEach(files, file =>
        {
            AnalyzeSingleFile(file, violations);
        });

        // 2. Statische Test-Präsenzprüfung (Sentinel) ausführen
        if (_config.Global.EnableTestSentinel)
        {
            RunTestSentinel(files, violations);
        }

        return violations.ToArray();
    }

    private void AnalyzeSingleFile(string file, ConcurrentBag<RuleViolation> violations)
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
    }

    private static void RunTestSentinel(IEnumerable<string> files, ConcurrentBag<RuleViolation> violations)
    {
        var testClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceClasses = new List<ClassInfo>();

        foreach (var file in files)
        {
            CollectClassesFromFile(file, testClasses, sourceClasses);
        }

        foreach (var srcClass in sourceClasses)
        {
            if (srcClass.MaxCognitiveComplexity > 3)
            {
                CheckTestPresence(srcClass, testClasses, violations);
            }
        }
    }

    private static void CollectClassesFromFile(string file, HashSet<string> testClasses, List<ClassInfo> sourceClasses)
    {
        try
        {
            var content = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(content);
            var collector = new ClassCollector(file);
            collector.Visit(tree.GetRoot());

            AddCollectedClasses(collector.Classes, file, testClasses, sourceClasses);
        }
        catch
        {
            // Ignoriere Parsing-Fehler beim Test Sentinel
        }
    }

    private static void AddCollectedClasses(List<ClassInfo> classes, string file, HashSet<string> testClasses, List<ClassInfo> sourceClasses)
    {
        if (IsTestFile(file))
        {
            foreach (var cls in classes)
            {
                testClasses.Add(cls.Name);
            }
            return;
        }
        sourceClasses.AddRange(classes);
    }

    private static bool IsTestFile(string file)
    {
        if (file.EndsWith("Tests.cs")) return true;
        if (file.EndsWith("Test.cs")) return true;
        return file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}");
    }

    private static void CheckTestPresence(ClassInfo srcClass, HashSet<string> testClasses, ConcurrentBag<RuleViolation> violations)
    {
        string expectedTest1 = $"{srcClass.Name}Tests";
        string expectedTest2 = $"{srcClass.Name}Test";

        if (!testClasses.Contains(expectedTest1) && !testClasses.Contains(expectedTest2))
        {
            violations.Add(new RuleViolation
            {
                FilePath = srcClass.FilePath,
                LineNumber = srcClass.LineNumber,
                RuleName = "StaticTestSentinel",
                Details = $"Die Klasse '{srcClass.Name}' hat eine hohe Relevanz (max. Kognitive Komplexität: {srcClass.MaxCognitiveComplexity}), aber es wurde keine Testklasse '{expectedTest1}' im Test-Projekt gefunden.",
                Guidance = $"Schreibe Unit Tests für '{srcClass.Name}' und lege die Testklasse '{expectedTest1}' im Test-Projekt an."
            });
        }
    }

    /// <summary>
    /// Generiert ein Mermaid-Abhängigkeitsdiagramm basierend auf den Projektreferenzen.
    /// </summary>
    public void GenerateMermaidGraph(string targetPath, string outputPath)
    {
        try
        {
            var projects = FindCsprojFiles(targetPath);
            var graphBuilder = new System.Text.StringBuilder();
            graphBuilder.AppendLine("```mermaid");
            graphBuilder.AppendLine("graph TD");

            foreach (var csproj in projects)
            {
                ParseCsprojReferences(csproj, graphBuilder);
            }

            graphBuilder.AppendLine("```");
            File.WriteAllText(outputPath, graphBuilder.ToString());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Fehler beim Generieren des Graphen: {ex.Message}", ex);
        }
    }

    private static void ParseCsprojReferences(string csproj, System.Text.StringBuilder graphBuilder)
    {
        var sourceName = Path.GetFileNameWithoutExtension(csproj);
        var doc = XDocument.Load(csproj);
        var projectRefs = doc.Descendants("ProjectReference");
        foreach (var pref in projectRefs)
        {
            var include = pref.Attribute("Include")?.Value;
            if (!string.IsNullOrEmpty(include))
            {
                var targetName = Path.GetFileNameWithoutExtension(include);
                graphBuilder.AppendLine($"    {sourceName} --> {targetName}");
            }
        }
    }

    private static IEnumerable<string> FindCsprojFiles(string path)
    {
        if (Directory.Exists(path))
        {
            return Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories);
        }
        if (File.Exists(path))
        {
            return FindCsprojFromFile(path);
        }
        return Array.Empty<string>();
    }

    private static IEnumerable<string> FindCsprojFromFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".csproj")
        {
            return new[] { path };
        }
        return FindCsprojFromSolution(path, ext);
    }

    private static IEnumerable<string> FindCsprojFromSolution(string path, string ext)
    {
        if (ext == ".slnx")
        {
            return ParseSlnx(path);
        }
        if (ext == ".sln")
        {
            return ParseSln(path);
        }
        return Array.Empty<string>();
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
            // Ignoriere XML-Parsing-Fehler
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

    /// <summary>
    /// Hilfs-Walker zum Sammeln aller deklarierten Klassen und deren maximalen Komplexitäten.
    /// </summary>
    private sealed class ClassCollector : CSharpSyntaxWalker
    {
        public List<ClassInfo> Classes { get; } = new();
        private readonly string _filePath;

        public ClassCollector(string filePath)
        {
            _filePath = filePath;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var maxComplexity = 0;
            foreach (var method in node.Members.OfType<MethodDeclarationSyntax>())
            {
                var comp = ComplexityCalculator.GetCognitiveComplexity(method);
                if (comp > maxComplexity)
                {
                    maxComplexity = comp;
                }
            }

            Classes.Add(new ClassInfo
            {
                Name = node.Identifier.Text,
                FilePath = _filePath,
                LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                MaxCognitiveComplexity = maxComplexity
            });

            base.VisitClassDeclaration(node);
        }
    }

    internal sealed class ClassInfo
    {
        public required string Name { get; init; }
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required int MaxCognitiveComplexity { get; init; }
    }
}
