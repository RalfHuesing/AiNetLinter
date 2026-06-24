#nullable enable

using AiNetLinter.Models;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

public sealed class ViolationMarkdownFormatterTests
{
    private static readonly string OutputRoot = Path.GetFullPath(@"C:\Projects\MyApp");

    [Fact]
    public void Format_ReturnsEmptyForNoViolations()
    {
        var result = ViolationMarkdownFormatter.Format([], OutputRoot);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_IncludesHeaderAndInstruction()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Klasse 'Bar' nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.StartsWith("# AiNetLinter - 1 violations", result);
        Assert.Contains("## Handlungsanweisung", result);
        Assert.Contains("False-Positive-Prüfung", result);
    }

    [Fact]
    public void Format_IncludesStructuralWarningInInstructionBlock()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "MaxPartialClassFiles", "Auf 3 Dateien verteilt")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("Frage den Nutzer VOR der Umsetzung", result);
    }

    [Fact]
    public void Format_IncludesRegellegendeBeforeViolations()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 10, "MaxLineCount", "Zu lang")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        var legendeIdx = result.IndexOf("## Regellegende", StringComparison.Ordinal);
        var violationsIdx = result.IndexOf("## Violations nach Datei", StringComparison.Ordinal);

        Assert.True(legendeIdx >= 0, "## Regellegende not found");
        Assert.True(violationsIdx > legendeIdx, "## Violations nach Datei must follow ## Regellegende");
        Assert.Contains("### EnforceSealedClasses", result);
        Assert.Contains("### MaxLineCount", result);
        Assert.Contains("**Warum:**", result);
        Assert.Contains("**Fix-Alternativen:**", result);
        Assert.DoesNotContain("## Summary - by rule", result);
        Assert.DoesNotContain("## Summary - by file", result);
    }

    [Fact]
    public void Format_GroupsViolationsByFile()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Zoo.cs", 10, "MaxLineCount", "Zu lang"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 20, "EnforceSealedClasses", "Nicht sealed"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        var fooIdx = result.IndexOf("#### src/Foo.cs", StringComparison.OrdinalIgnoreCase);
        var zooIdx = result.IndexOf("#### src/Zoo.cs", StringComparison.OrdinalIgnoreCase);
        var line5Idx = result.IndexOf("Z.5 EnforceSealedClasses", StringComparison.OrdinalIgnoreCase);
        var line20Idx = result.IndexOf("Z.20 EnforceSealedClasses", StringComparison.OrdinalIgnoreCase);

        Assert.True(fooIdx >= 0, "#### src/Foo.cs not found");
        Assert.True(zooIdx >= 0, "#### src/Zoo.cs not found");
        Assert.True(fooIdx < zooIdx, "Foo.cs must come before Zoo.cs (alphabetical)");
        Assert.True(line5Idx < line20Idx, "Line 5 must appear before line 20 within file section");
        Assert.True(line5Idx > fooIdx, "Z.5 must be inside the Foo.cs section");
    }

    [Fact]
    public void Format_UsesRelativePathsAndCompactLineFormat()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Core\Bar.cs", 12, "MaxMethodParameterCount", "6 Parameter")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("#### src/Core/Bar.cs", result);
        Assert.Contains("Z.12 MaxMethodParameterCount", result);
        Assert.Contains("6 Parameter", result);
    }

    [Fact]
    public void Format_ShowsStructuralViolationsSectionForMaxPartialClassFiles()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 9, "MaxPartialClassFiles", "Auf 5 Dateien verteilt")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("## Strukturelle Verstöße", result);
        Assert.Contains("Nutzer VOR Beginn fragen", result);
        Assert.Contains("### MaxPartialClassFiles", result);
    }

    [Fact]
    public void Format_ShowsStructuralViolationsSectionForAiContextFootprint()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\BigClass.cs", 1, "AIContextFootprint", "BigClass (6000 > 5000)")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("## Strukturelle Verstöße", result);
        Assert.Contains("### AIContextFootprint", result);
    }

    [Fact]
    public void Format_OmitsStructuralSectionWhenNoStructuralViolations()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.DoesNotContain("## Strukturelle Verstöße", result);
    }

    [Fact]
    public void Format_StructuralViolationsAlsoAppearInViolationsByFile()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 9, "MaxPartialClassFiles", "Auf 5 Dateien verteilt")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        var structuralIdx = result.IndexOf("## Strukturelle Verstöße", StringComparison.Ordinal);
        var byFileIdx = result.IndexOf("## Violations nach Datei", StringComparison.Ordinal);
        var fooInByFile = result.IndexOf("#### src/Foo.cs", byFileIdx, StringComparison.OrdinalIgnoreCase);

        Assert.True(structuralIdx < byFileIdx);
        Assert.True(fooInByFile >= 0, "Structural violation must also appear in Violations nach Datei");
        Assert.Contains("MaxPartialClassFiles [→ strukturell]", result);
    }

    [Fact]
    public void Format_MarksAutoFixableViolationsWithTag()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 1, "EnforceNullableEnable", "Nullable fehlt"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "MaxLineCount", "Zu lang")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("EnforceNullableEnable [auto-fix]", result);
        Assert.DoesNotContain("MaxLineCount [auto-fix]", result);
    }

    [Fact]
    public void Format_ShowsAutoFixHintInInstructionBlockWhenApplicable()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 1, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("Auto-Fix verfuegbar", result);
        Assert.Contains("--fix", result);
    }

    [Fact]
    public void Format_OmitsAutoFixHintWhenNoAutoFixableViolations()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "MaxLineCount", "Zu lang")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.DoesNotContain("Auto-Fix verfuegbar", result);
    }

    private static RuleViolation CreateViolation(string filePath, int line, string rule, string details) =>
        new()
        {
            FilePath = filePath,
            LineNumber = line,
            RuleName = rule,
            Details = details,
            Guidance = "Guidance text"
        };

    [Fact]
    public void Format_ShowsSummaryTableAfterHeaderAndBeforeHandlungsanweisung()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        var headerIdx = result.IndexOf("# AiNetLinter - 1 violations", StringComparison.Ordinal);
        var tableIdx = result.IndexOf("| Regel | Gesamt | Prod | Tests |", StringComparison.Ordinal);
        var handlungsIdx = result.IndexOf("## Handlungsanweisung", StringComparison.Ordinal);

        Assert.True(headerIdx >= 0);
        Assert.True(tableIdx > headerIdx);
        Assert.True(handlungsIdx > tableIdx);
    }

    [Fact]
    public void Format_SummaryTable_CountsProdAndTestsSeparately()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed"),
            CreateViolation(@"C:\Projects\MyApp\src\MyApp.Tests\FooTests.cs", 10, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("| EnforceSealedClasses | 2 | 1 | 1 |", result);
    }

    [Fact]
    public void Format_SummaryTable_MarksStructuralRulesWithWarning()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "MaxPartialClassFiles", "Detail"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Detail")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("| Regel | Gesamt | Prod | Tests | Struktur |", result);
        Assert.Contains("| MaxPartialClassFiles | 1 | 1 | 0 | ⚠ |", result);
        Assert.Contains("| EnforceSealedClasses | 1 | 1 | 0 |  |", result);
    }

    [Fact]
    public void Format_SummaryTable_OmitsStructureColumnWhenNoStructuralViolations()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Detail")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("| Regel | Gesamt | Prod | Tests |\n", result);
        Assert.DoesNotContain("| Struktur |", result);
    }

    [Fact]
    public void Format_SplitsProdBeforeTestsInViolationsByFile()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\MyApp.Tests\FooTests.cs", 10, "EnforceSealedClasses", "Nicht sealed"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        var prodIdx = result.IndexOf("### Produktion", StringComparison.Ordinal);
        var testsIdx = result.IndexOf("### Tests", StringComparison.Ordinal);

        Assert.True(prodIdx >= 0);
        Assert.True(testsIdx > prodIdx);
    }

    [Fact]
    public void Format_OmitsTestSectionWhenNoTestViolations()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("### Produktion", result);
        Assert.DoesNotContain("### Tests", result);
    }

    [Fact]
    public void Format_OmitsProdSectionWhenOnlyTestViolations()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\MyApp.Tests\FooTests.cs", 10, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.DoesNotContain("### Produktion", result);
        Assert.Contains("### Tests", result);
    }

    [Fact]
    public void Format_FilesInSectionUseH4Headers()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("#### src/Foo.cs", result);
        Assert.DoesNotContain("\n### src/Foo.cs\n", result);
    }

    [Fact]
    public void Format_StructuralViolationInByFileUsesMarkerTag()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "MaxPartialClassFiles", "Detail")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("MaxPartialClassFiles [→ strukturell] — Detail", result);
    }

    [Fact]
    public void Format_StructuralViolationInByFileShowsOnlyFirstDetailLine()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "MaxPartialClassFiles", "Zeile 1\nZeile 2\nZeile 3")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("MaxPartialClassFiles [→ strukturell] — Zeile 1", result);
        Assert.DoesNotContain("Zeile 2", result.Substring(result.IndexOf("## Violations nach Datei")));
    }

    [Fact]
    public void Format_InstructionBlock_UsesShortExeName()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        var docsIndex = result.IndexOf(" --docs configuration`", StringComparison.Ordinal);
        Assert.True(docsIndex >= 0, "docs configuration command not found");
        var lineStart = result.LastIndexOf('`', docsIndex);
        Assert.True(lineStart >= 0);
        var exeName = result.Substring(lineStart + 1, docsIndex - lineStart - 1);
        Assert.DoesNotContain("\\", exeName);
        Assert.DoesNotContain("/", exeName);
        Assert.DoesNotContain(":", exeName);
    }

    [Fact]
    public void Format_RuleLegend_ShowsConfigKeyHintForMaxPartialClassFiles()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "MaxPartialClassFiles", "Detail")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("**Konfiguration:** `rules.json → Metrics.MaxPartialClassFiles", result);
    }

    [Fact]
    public void Format_RuleLegend_ShowsConfigKeyHintForAiContextFootprint()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "AIContextFootprint", "Detail")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("**Konfiguration:** `rules.json → Metrics.MaxAIContextFootprint", result);
    }

    [Fact]
    public void Format_ViolationWithWarningSeverity_ContainsWarnTag()
    {
        var violation = new RuleViolation
        {
            FilePath = @"C:\Projects\MyApp\src\Foo.cs", LineNumber = 10,
            RuleName = "MaxMethodLineCount", Details = "Methode hat 180 Zeilen", Guidance = "...",
            EffectiveSeverity = "warning"
        };
        var result = ViolationMarkdownFormatter.Format(new[] { violation }, OutputRoot);
        Assert.Contains("[warn]", result);
        Assert.Contains("`[warn]`-Violations sind durch CompoundSuppression herabgestuft", result);
    }

    [Fact]
    public void Format_ViolationWithNullSeverity_NoWarnTag()
    {
        var violation = new RuleViolation
        {
            FilePath = @"C:\Projects\MyApp\src\Foo.cs", LineNumber = 10,
            RuleName = "MaxMethodLineCount", Details = "Methode hat 80 Zeilen", Guidance = "...",
            EffectiveSeverity = null
        };
        var result = ViolationMarkdownFormatter.Format(new[] { violation }, OutputRoot);
        Assert.DoesNotContain("[warn]", result);
        Assert.DoesNotContain("`[warn]`-Violations sind durch CompoundSuppression herabgestuft", result);
    }

    [Fact]
    public void Format_IncludesGuidanceInRegularViolations()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "MaxSwitchArms", "Switch-Statement hat 23 Labels")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("Empfehlung: Guidance text", result);
    }

    [Fact]
    public void Format_IncludesGuidanceInStructuralViolations()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "MaxPartialClassFiles", "Auf 5 Dateien verteilt")
        };

        var result = ViolationMarkdownFormatter.Format(violations, OutputRoot);

        Assert.Contains("Empfehlung: Guidance text", result);
    }
}
