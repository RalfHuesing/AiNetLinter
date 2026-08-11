#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Maps.Skeleton;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Output;
using Xunit;

namespace AiNetLinter.Tests.Cli;

// @covers SourceFileCatalog
// @covers NamespaceFilter
// @covers SkeletonSyntaxWalker
// @covers SkeletonMapBuilder
[Trait("Category", "Integration")]
public sealed class FilterCliIntegrationTests
{
    private readonly string _rootDir;
    private readonly string _slnPath;

    public FilterCliIntegrationTests()
    {
        _rootDir = CliProcessRunner.FindSolutionRoot();
        _slnPath = Path.Combine(_rootDir, "AiNetLinter.slnx");
    }

    // ─── --exclude-tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task SkeletonMap_ExcludeTests_OutputContainsNoTestTypes()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, ExcludeTests = true
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        // Testklassen-Namensräume dürfen nicht im Output erscheinen
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
        // Produktionstypen müssen vorhanden sein
        Assert.Contains("AiNetLinter", output, StringComparison.Ordinal);
    }

    // ─── --tests-only ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SkeletonMap_TestsOnly_OutputContainsOnlyTestNamespaces()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, TestsOnly = true
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        // Alle Types müssen im Tests-Namensraum liegen
        Assert.Contains("AiNetLinter.Tests", output, StringComparison.Ordinal);
        // Rein-produktive Klassen dürfen nicht im Output stehen
        Assert.DoesNotContain("namespace AiNetLinter.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace AiNetLinter.Commands", output, StringComparison.Ordinal);
    }

    // ─── --project ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SkeletonMap_ProjectFilter_OutputContainsOnlyMatchingProject()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, IncludeProjects = new[] { "AiNetLinter" }
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        // Nur das Hauptprojekt sollte enthalten sein, nicht das Tests-Projekt
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ProjectGlobFilter_WildcardMatchesTests()
    {
        var (output, _, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, IncludeProjects = new[] { "*.Tests" }
        });

        Assert.Equal(0, exitCode);
        // Nur Tests-Typen sollen enthalten sein
        Assert.Contains("AiNetLinter.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace AiNetLinter.Commands", output, StringComparison.Ordinal);
    }

    // ─── --exclude-project ──────────────────────────────────────────────────────

    [Fact]
    public async Task SkeletonMap_ExcludeProjectByGlob_OutputExcludesTests()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, ExcludeProjects = new[] { "*.Tests" }
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
        Assert.Contains("AiNetLinter", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ExcludeProjectByExactName_OutputExcludesProject()
    {
        // Exakter Projektname ohne Glob
        var (output, _, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, ExcludeProjects = new[] { "AiNetLinter" }
        });

        Assert.Equal(0, exitCode);
        // AiNetLinter-Projekt ausgeschlossen → nur AiNetLinter.Tests kann enthalten sein
        Assert.DoesNotContain("namespace AiNetLinter.Commands", output, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace AiNetLinter.Core;", output, StringComparison.Ordinal);
    }

    // ─── --namespace ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SkeletonMap_NamespaceFilter_OutputContainsOnlyCliNamespace()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, IncludeNamespaces = new[] { "AiNetLinter.Cli" }
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        // Nur CLI-Typen sollen enthalten sein
        Assert.Contains("LinterArgs", output, StringComparison.Ordinal);
        Assert.Contains("CliOptionFactory", output, StringComparison.Ordinal);
        // Andere Namespaces dürfen nicht enthalten sein
        Assert.DoesNotContain("LinterEngine", output, StringComparison.Ordinal);
        Assert.DoesNotContain("SkeletonMapBuilder", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_NamespaceGlobFilter_MatchesSubnamespaces()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, IncludeNamespaces = new[] { "AiNetLinter.Maps*" }
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        // Maps-Typen müssen enthalten sein
        Assert.Contains("SkeletonMapBuilder", output, StringComparison.Ordinal);
        // Namespace-Abschnitte anderer Namespaces dürfen nicht enthalten sein
        // (Typnamen können als Methodenparameter im Output erscheinen – daher Abschnitt prüfen)
        Assert.DoesNotContain("## AiNetLinter.Cli", output, StringComparison.Ordinal);
        Assert.DoesNotContain("## AiNetLinter.Core", output, StringComparison.Ordinal);
    }

    // ─── --exclude-namespace ────────────────────────────────────────────────────

    [Fact]
    public async Task SkeletonMap_ExcludeNamespace_OutputExcludesNamespace()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, ExcludeNamespaces = new[] { "AiNetLinter.Cli" }
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        // Der Namespace-Abschnitt 'AiNetLinter.Cli' darf nicht erscheinen
        // (Typnamen können als Methodenparameter in anderen Namespaces erscheinen – daher Abschnitt prüfen)
        Assert.DoesNotContain("## AiNetLinter.Cli", output, StringComparison.Ordinal);
        Assert.DoesNotContain("CliOptionFactory", output, StringComparison.Ordinal);
        // Andere Namespace-Abschnitte müssen weiterhin enthalten sein
        Assert.Contains("## AiNetLinter.Core", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ExcludeNamespaceGlob_ExcludesAllTestNamespaces()
    {
        var (output, _, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, ExcludeNamespaces = new[] { "AiNetLinter.Tests*" }
        });

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
    }

    // ─── --public-only ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SkeletonMap_PublicOnly_OutputExcludesPrivateMethods()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false,
            IncludeNamespaces = new[] { "AiNetLinter.Cli" }, PublicOnly = true
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        // Private-Methoden dürfen nicht im Output erscheinen
        Assert.DoesNotContain("private static", output, StringComparison.Ordinal);
        Assert.DoesNotContain("private ", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_WithoutPublicOnly_OutputContainsPrivateMembers()
    {
        var (output, _, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, IncludeNamespaces = new[] { "AiNetLinter.Cli" }
        });

        Assert.Equal(0, exitCode);
        // Ohne --public-only müssen private Member enthalten sein
        Assert.Contains("private ", output, StringComparison.Ordinal);
    }

    // ─── Kombinationen ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SkeletonMap_ExcludeTestsAndPublicOnly_ShowsOnlyPublicProductionTypes()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, ExcludeTests = true, PublicOnly = true
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        // Keine Test-Namespaces
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
        // Keine privaten Member
        Assert.DoesNotContain("private ", output, StringComparison.Ordinal);
        // Produktionstypen vorhanden
        Assert.Contains("AiNetLinter", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ProjectAndNamespaceFilter_NarrowsOutputFurther()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false,
            IncludeProjects = new[] { "AiNetLinter" }, IncludeNamespaces = new[] { "AiNetLinter.Cli" }
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        Assert.Contains("LinterArgs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("AiNetLinter.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain("LinterEngine", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_TestsOnlyAndNamespaceFilter_ShowsOnlyMatchingTestNamespace()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false,
            TestsOnly = true, IncludeNamespaces = new[] { "AiNetLinter.Tests.Cli" }
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        Assert.Contains("AiNetLinter.Tests.Cli", output, StringComparison.Ordinal);
        // Andere Test-Namespaces dürfen nicht enthalten sein
        Assert.DoesNotContain("AiNetLinter.Tests.Commands", output, StringComparison.Ordinal);
        Assert.DoesNotContain("AiNetLinter.Tests.Maps", output, StringComparison.Ordinal);
    }

    // ─── Grenzfälle & Fehler ────────────────────────────────────────────────────

    [Fact]
    public async Task SkeletonMap_UnknownProject_ReturnsEmptyOutputWithoutError()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, IncludeProjects = new[] { "NonExistentProject" }
        });

        // Kein Projekt passt → leere (aber erfolgreiche) Ausgabe
        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        // Output-Header kann leer sein oder hat keine Klassen-Definitionen
        Assert.DoesNotContain("```csharp", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_UnknownNamespace_ReturnsEmptyOutputWithoutError()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, IncludeNamespaces = new[] { "NonExistent.Namespace" }
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.Errors);
        Assert.DoesNotContain("```csharp", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ExcludeTestsAndTestsOnly_ExcludeTestsTakesPrecedence()
    {
        // Wenn beide Flags angegeben werden, darf der Linter nicht abstürzen
        // (einer macht alle projekts leer durch die Kombination → leere oder fast-leere Ausgabe)
        var (_, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = _slnPath, Verbose = false, ExcludeTests = true, TestsOnly = true
        });

        Assert.Equal(0, exitCode);
        // Kein Crash, kein stderr
        Assert.Empty(console.Errors);
    }

    // ─── Hilfsinfrastruktur ─────────────────────────────────────────────────────

    private static async Task<(string Output, TestLintConsole Console, int ExitCode)> RunSkeletonAsync(LinterArgs args)
    {
        var console = new TestLintConsole();
        var config = new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig()
        };
        var exitCode = await SkeletonMapBuilder.BuildAsync(args.TargetPath, config, console, args);
        return (console.OutputText, console, exitCode);
    }
}
