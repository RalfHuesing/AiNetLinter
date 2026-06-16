#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            if (RazorHasInlineCode(fileContent) && !File.Exists(codeBehindPath) && !IsRazorSuppressed(fileContent, "BlazorRequireCodeBehind"))
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
            var needsCss = !config.BlazorCssIsolationOnlyWhenStylesNeeded || RazorNeedsCss(fileContent);
            if (needsCss && !File.Exists(cssPath) && !IsRazorSuppressed(fileContent, "BlazorRequireCssIsolation"))
            {
                violations.Add(CreateViolation(
                    razorFile,
                    "BlazorRequireCssIsolation",
                    $"Die Razor-Komponente '{Path.GetFileName(razorFile)}' enthaelt native HTML-Elemente oder class=/style=-Attribute, aber keine '{Path.GetFileName(cssPath)}'-CSS-Isolationsdatei.",
                    "Erstelle eine separate '.razor.css'-Datei fuer komponentenspezifische Styles (Blazor CSS-Isolation). " +
                    "Verschiebe alle '<style>'-Bloecke aus der '.razor'-Datei dorthin. " +
                    "Blazor scoped diese Styles automatisch auf die Komponente. " +
                    "Reine Komponenten-Komposition (nur PascalCase-Tags wie <MudButton>) benoetigt keine CSS-Datei." +
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

    /// <summary>
    /// Erkennt ob eine Razor-Datei tatsächlich CSS-Isolation benötigt.
    /// Kriterien: native HTML-Elemente (lowercase Tags wie &lt;div&gt;, &lt;span&gt;)
    /// oder class=/style=-Attribute (lowercase = HTML, PascalCase = Blazor-Props).
    /// Reine Komponenten-Komposition (nur PascalCase-Tags) braucht keine CSS-Datei.
    /// </summary>
    internal static bool RazorNeedsCss(string fileContent)
    {
        if (string.IsNullOrEmpty(fileContent)) return false;

        // HTML class/style-Attribute (lowercase = native HTML, nicht Blazor-Component-Props wie Class= oder Style=)
        if (fileContent.Contains("class=\"", StringComparison.Ordinal)) return true;
        if (fileContent.Contains("class='", StringComparison.Ordinal)) return true;
        if (fileContent.Contains("class=@", StringComparison.Ordinal)) return true;
        if (fileContent.Contains("style=\"", StringComparison.Ordinal)) return true;
        if (fileContent.Contains("style='", StringComparison.Ordinal)) return true;
        if (fileContent.Contains("style=@", StringComparison.Ordinal)) return true;

        // Native HTML-Elemente: <tagname  wo tagname mit Kleinbuchstaben beginnt
        return NativeHtmlTagPattern.IsMatch(fileContent);
    }

    /// <summary>
    /// Erkennt ob eine Razor-Datei inline C#-Code enthält (@code-Block oder @functions-Block).
    /// Nur wenn Inline-Code vorhanden ist, wird BlazorRequireCodeBehind geprüft.
    /// Reine Template-Dateien (nur Markup, keine Logik) lösen keine Verletzung aus.
    /// </summary>
    internal static bool RazorHasInlineCode(string fileContent)
    {
        if (string.IsNullOrEmpty(fileContent)) return false;
        return fileContent.Contains("@code", StringComparison.Ordinal)
            || fileContent.Contains("@functions", StringComparison.Ordinal);
    }

    // Matcht <div, <span, <p, <h1, <input etc. — aber nicht <MudButton, <Component etc.
    private static readonly Regex NativeHtmlTagPattern =
        new Regex(@"<[a-z][a-zA-Z0-9]*[\s>/]", RegexOptions.Compiled);

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
            .Where(p => !TestProjectDetector.IsTestProject(p))
            .Select(p => Path.GetDirectoryName(p.FilePath)!)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);
    }
}
