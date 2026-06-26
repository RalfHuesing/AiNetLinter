#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace AiNetLinter.Tests.Cli;

// @covers SourceFileCatalog
// @covers NamespaceFilter
// @covers SkeletonSyntaxWalker
// @covers SkeletonMapBuilder
[Collection("ConsoleTestCollection")]
public sealed class FilterCliIntegrationTests
{
    private readonly string _rootDir;
    private readonly string _linterDllPath;
    private readonly string _configPath;
    private readonly string _slnPath;

    public FilterCliIntegrationTests()
    {
        _rootDir       = FindSolutionRoot();
        _linterDllPath = FindLinterDll(_rootDir);
        _configPath    = Path.Combine(_rootDir, "rules.json");
        _slnPath       = Path.Combine(_rootDir, "AiNetLinter.slnx");
    }

    // ─── --exclude-tests ────────────────────────────────────────────────────────

    [Fact]
    public void SkeletonMap_ExcludeTests_OutputContainsNoTestTypes()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --exclude-tests");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        // Testklassen-Namensräume dürfen nicht im Output erscheinen
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
        // Produktionstypen müssen vorhanden sein
        Assert.Contains("AiNetLinter", output, StringComparison.Ordinal);
    }

    [Fact]
    public void VocabularyMap_ExcludeTests_OutputContainsNoTestSuffix()
    {
        var (output, _, exitCode) = Run($"--path \"{_slnPath}\" --map vocabulary --exclude-tests");

        Assert.Equal(0, exitCode);
        // Tests-only Dateipfade dürfen nicht erscheinen
        Assert.DoesNotContain(".Tests/", output, StringComparison.Ordinal);
        Assert.DoesNotContain(".Tests\\", output, StringComparison.Ordinal);
    }

    // ─── --tests-only ───────────────────────────────────────────────────────────

    [Fact]
    public void SkeletonMap_TestsOnly_OutputContainsOnlyTestNamespaces()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --tests-only");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        // Alle Types müssen im Tests-Namensraum liegen
        Assert.Contains("AiNetLinter.Tests", output, StringComparison.Ordinal);
        // Rein-produktive Klassen dürfen nicht im Output stehen
        Assert.DoesNotContain("namespace AiNetLinter.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace AiNetLinter.Commands", output, StringComparison.Ordinal);
    }

    // ─── --project ──────────────────────────────────────────────────────────────

    [Fact]
    public void SkeletonMap_ProjectFilter_OutputContainsOnlyMatchingProject()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --project AiNetLinter");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        // Nur das Hauptprojekt sollte enthalten sein, nicht das Tests-Projekt
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SkeletonMap_ProjectGlobFilter_WildcardMatchesTests()
    {
        var (output, _, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --project \"*.Tests\"");

        Assert.Equal(0, exitCode);
        // Nur Tests-Typen sollen enthalten sein
        Assert.Contains("AiNetLinter.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace AiNetLinter.Commands", output, StringComparison.Ordinal);
    }

    // ─── --exclude-project ──────────────────────────────────────────────────────

    [Fact]
    public void SkeletonMap_ExcludeProjectByGlob_OutputExcludesTests()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --exclude-project \"*.Tests\"");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
        Assert.Contains("AiNetLinter", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SkeletonMap_ExcludeProjectByExactName_OutputExcludesProject()
    {
        // Exakter Projektname ohne Glob
        var (output, _, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --exclude-project AiNetLinter");

        Assert.Equal(0, exitCode);
        // AiNetLinter-Projekt ausgeschlossen → nur AiNetLinter.Tests kann enthalten sein
        Assert.DoesNotContain("namespace AiNetLinter.Commands", output, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace AiNetLinter.Core;", output, StringComparison.Ordinal);
    }

    // ─── --namespace ────────────────────────────────────────────────────────────

    [Fact]
    public void SkeletonMap_NamespaceFilter_OutputContainsOnlyCliNamespace()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --namespace AiNetLinter.Cli");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        // Nur CLI-Typen sollen enthalten sein
        Assert.Contains("LinterArgs", output, StringComparison.Ordinal);
        Assert.Contains("CliOptionFactory", output, StringComparison.Ordinal);
        // Andere Namespaces dürfen nicht enthalten sein
        Assert.DoesNotContain("LinterEngine", output, StringComparison.Ordinal);
        Assert.DoesNotContain("SkeletonMapBuilder", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SkeletonMap_NamespaceGlobFilter_MatchesSubnamespaces()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --namespace \"AiNetLinter.Maps*\"");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        // Maps-Typen müssen enthalten sein
        Assert.Contains("SkeletonMapBuilder", output, StringComparison.Ordinal);
        // Nicht-Maps-Typen dürfen nicht enthalten sein
        Assert.DoesNotContain("LinterArgs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("LinterEngine", output, StringComparison.Ordinal);
    }

    // ─── --exclude-namespace ────────────────────────────────────────────────────

    [Fact]
    public void SkeletonMap_ExcludeNamespace_OutputExcludesNamespace()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --exclude-namespace AiNetLinter.Cli");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        // Cli-Typen dürfen nicht enthalten sein
        Assert.DoesNotContain("LinterArgs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("CliOptionFactory", output, StringComparison.Ordinal);
        // Andere Typen müssen weiterhin enthalten sein
        Assert.Contains("LinterEngine", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SkeletonMap_ExcludeNamespaceGlob_ExcludesAllTestNamespaces()
    {
        var (output, _, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --exclude-namespace \"AiNetLinter.Tests*\"");

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
    }

    // ─── --public-only ──────────────────────────────────────────────────────────

    [Fact]
    public void SkeletonMap_PublicOnly_OutputExcludesPrivateMethods()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --namespace AiNetLinter.Cli --public-only");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        // Private-Methoden dürfen nicht im Output erscheinen
        Assert.DoesNotContain("private static", output, StringComparison.Ordinal);
        Assert.DoesNotContain("private ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SkeletonMap_WithoutPublicOnly_OutputContainsPrivateMembers()
    {
        var (output, _, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --namespace AiNetLinter.Cli");

        Assert.Equal(0, exitCode);
        // Ohne --public-only müssen private Member enthalten sein
        Assert.Contains("private ", output, StringComparison.Ordinal);
    }

    // ─── Kombinationen ──────────────────────────────────────────────────────────

    [Fact]
    public void SkeletonMap_ExcludeTestsAndPublicOnly_ShowsOnlyPublicProductionTypes()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --exclude-tests --public-only");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        // Keine Test-Namespaces
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
        // Keine privaten Member
        Assert.DoesNotContain("private ", output, StringComparison.Ordinal);
        // Produktionstypen vorhanden
        Assert.Contains("AiNetLinter", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SkeletonMap_ProjectAndNamespaceFilter_NarrowsOutputFurther()
    {
        var (output, error, exitCode) = Run(
            $"--path \"{_slnPath}\" --map skeleton --project AiNetLinter --namespace AiNetLinter.Cli");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        Assert.Contains("LinterArgs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain("LinterEngine", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SkeletonMap_TestsOnlyAndNamespaceFilter_ShowsOnlyMatchingTestNamespace()
    {
        var (output, error, exitCode) = Run(
            $"--path \"{_slnPath}\" --map skeleton --tests-only --namespace \"AiNetLinter.Tests.Cli\"");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        Assert.Contains("AiNetLinter.Tests.Cli", output, StringComparison.Ordinal);
        // Andere Test-Namespaces dürfen nicht enthalten sein
        Assert.DoesNotContain("AiNetLinter.Tests.Commands", output, StringComparison.Ordinal);
        Assert.DoesNotContain("AiNetLinter.Tests.Maps", output, StringComparison.Ordinal);
    }

    // ─── Grenzfälle & Fehler ────────────────────────────────────────────────────

    [Fact]
    public void SkeletonMap_UnknownProject_ReturnsEmptyOutputWithoutError()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --project \"NonExistentProject\"");

        // Kein Projekt passt → leere (aber erfolgreiche) Ausgabe
        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        // Output-Header kann leer sein oder hat keine Klassen-Definitionen
        Assert.DoesNotContain("```csharp", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SkeletonMap_UnknownNamespace_ReturnsEmptyOutputWithoutError()
    {
        var (output, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --namespace \"NonExistent.Namespace\"");

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        Assert.DoesNotContain("```csharp", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SkeletonMap_ExcludeTestsAndTestsOnly_ExcludeTestsTakesPrecedence()
    {
        // Wenn beide Flags angegeben werden, darf der Linter nicht abstürzen
        // (einer macht alle projekts leer durch die Kombination → leere oder fast-leere Ausgabe)
        var (_, error, exitCode) = Run($"--path \"{_slnPath}\" --map skeleton --exclude-tests --tests-only");

        Assert.Equal(0, exitCode);
        // Kein Crash, kein stderr
        Assert.Empty(error);
    }

    // ─── Andere Map-Typen mit Filtern ───────────────────────────────────────────

    [Fact]
    public void VocabularyMap_ExcludeProject_ExcludesProjectFromOutput()
    {
        var (output, _, exitCode) = Run($"--path \"{_slnPath}\" --map vocabulary --exclude-project \"*.Tests\"");

        Assert.Equal(0, exitCode);
        // In Vocabulary-Maps erscheint der relative Pfad der Datei
        Assert.DoesNotContain("AiNetLinter.Tests/", output, StringComparison.Ordinal);
    }

    [Fact]
    public void StructureMap_ExcludeTests_ExcludesTestFiles()
    {
        var (output, _, exitCode) = Run($"--path \"{_rootDir}\" --map structure --exclude-project \"*.Tests\"");

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
    }

    // ─── Hilfsinfrastruktur ─────────────────────────────────────────────────────

    private (string Output, string Error, int ExitCode) Run(string arguments)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName              = "dotnet",
            Arguments             = $"\"{_linterDllPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute       = false,
            CreateNoWindow        = true
        };

        using var process = Process.Start(processInfo)
            ?? throw new InvalidOperationException("Konnte den Linter-Prozess nicht starten.");

        var output = process.StandardOutput.ReadToEnd();
        var error  = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (output, error, process.ExitCode);
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
                return currentDir.FullName;
            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }

    private static string FindLinterDll(string rootDir)
    {
        var binDir = Path.Combine(rootDir, "src", "AiNetLinter", "bin");
        if (!Directory.Exists(binDir))
            throw new DirectoryNotFoundException($"Build-Ausgabeverzeichnis nicht gefunden: {binDir}");

        var files = Directory.GetFiles(binDir, "AiNetLinter.dll", SearchOption.AllDirectories);
        if (files.Length == 0)
            throw new FileNotFoundException("'AiNetLinter.dll' in keinem Build-Unterordner gefunden.");

        return files.OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc).First();
    }
}
