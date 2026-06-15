#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core;

/// <summary>
/// Prüft Blazor-Komponenten (.razor) auf erforderliche Begleitdateien (.razor.cs und .razor.css).
/// Arbeitet auf Datei-System-Ebene (nicht Roslyn), da .razor-Dateien nicht im Roslyn-Workspace erscheinen.
/// </summary>
internal static class UiFileSeparationChecker
{
    public static void Run(AnalysisState state, LinterConfig config)
    {
        var uiConfig = config.UiSeparation;
        if (!uiConfig.BlazorRequireCodeBehind && !uiConfig.BlazorRequireCssIsolation)
            return;

        var projectDirs = GetProjectDirectories(state.Solution);
        foreach (var dir in projectDirs)
        {
            ScanDirectory(dir, state.Violations, uiConfig);
        }
    }

    internal static void ScanDirectory(string dir, ConcurrentBag<RuleViolation> violations, UiSeparationConfig config)
    {
        if (!Directory.Exists(dir)) return;

        var razorFiles = Directory.GetFiles(dir, "*.razor", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .Where(f => !IsExcludedFileName(Path.GetFileName(f), config.BlazorExcludeFileNames));

        foreach (var razorFile in razorFiles)
        {
            CheckRazorFile(razorFile, violations, config);
        }
    }

    private static void CheckRazorFile(string razorFile, ConcurrentBag<RuleViolation> violations, UiSeparationConfig config)
    {
        var fileContent = ReadFileSafe(razorFile);

        if (config.BlazorRequireCodeBehind)
        {
            var codeBehindPath = razorFile + ".cs";
            if (!File.Exists(codeBehindPath) && !IsRazorSuppressed(fileContent, "BlazorRequireCodeBehind"))
            {
                violations.Add(CreateViolation(
                    razorFile,
                    "BlazorRequireCodeBehind",
                    $"Die Razor-Komponente '{Path.GetFileName(razorFile)}' hat keine '{Path.GetFileName(codeBehindPath)}'-Begleitdatei.",
                    "Erstelle eine separate '.razor.cs'-Datei mit einer partial class fuer die Komponenten-Logik (MVVM/Code-Behind-Muster). " +
                    "Verschiebe alle '@code { }' Bloecke aus der '.razor'-Datei dorthin. " +
                    "Beispiel: 'public partial class MyComponent : ComponentBase { ... }' in 'MyComponent.razor.cs'." +
                    " Suppression moeglich mit: @* ainetlinter-disable BlazorRequireCodeBehind *@"));
            }
        }

        if (config.BlazorRequireCssIsolation)
        {
            var cssPath = razorFile + ".css";
            if (!File.Exists(cssPath) && !IsRazorSuppressed(fileContent, "BlazorRequireCssIsolation"))
            {
                violations.Add(CreateViolation(
                    razorFile,
                    "BlazorRequireCssIsolation",
                    $"Die Razor-Komponente '{Path.GetFileName(razorFile)}' hat keine '{Path.GetFileName(cssPath)}'-CSS-Isolationsdatei.",
                    "Erstelle eine separate '.razor.css'-Datei fuer komponentenspezifische Styles (Blazor CSS-Isolation). " +
                    "Verschiebe alle '<style>'-Bloecke aus der '.razor'-Datei dorthin. " +
                    "Blazor scoped diese Styles automatisch auf die Komponente." +
                    " Suppression moeglich mit: @* ainetlinter-disable BlazorRequireCssIsolation *@"));
            }
        }
    }

    private static RuleViolation CreateViolation(string filePath, string ruleName, string details, string guidance) =>
        new RuleViolation
        {
            FilePath = filePath,
            LineNumber = 1,
            RuleName = ruleName,
            Details = details,
            Guidance = guidance
        };

    /// <summary>
    /// Prüft, ob die Regel in der Razor-Datei unterdrückt wird.
    /// Blazor-Kommentar-Syntax: @* ainetlinter-disable RuleName *@
    /// Auch C#-Kommentar-Syntax wird unterstützt: // ainetlinter-disable RuleName
    /// </summary>
    internal static bool IsRazorSuppressed(string fileContent, string ruleName)
    {
        if (string.IsNullOrEmpty(fileContent)) return false;

        return fileContent.Contains($"ainetlinter-disable {ruleName}", StringComparison.OrdinalIgnoreCase)
            || fileContent.Contains("ainetlinter-disable all", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInExcludedDirectory(string filePath)
    {
        var sep = Path.DirectorySeparatorChar;
        return filePath.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains($"{sep}bin{sep}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcludedFileName(string fileName, IReadOnlyCollection<string> excludeNames)
    {
        return excludeNames.Contains(fileName, StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadFileSafe(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return string.Empty; }
    }

    private static System.Collections.Generic.IEnumerable<string> GetProjectDirectories(Solution solution)
    {
        return solution.Projects
            .Where(p => !string.IsNullOrEmpty(p.FilePath))
            .Select(p => Path.GetDirectoryName(p.FilePath)!)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);
    }
}
