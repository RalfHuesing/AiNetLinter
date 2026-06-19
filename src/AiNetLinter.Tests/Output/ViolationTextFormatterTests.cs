#nullable enable

using AiNetLinter.Models;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

public sealed class ViolationTextFormatterTests
{
    private static readonly string OutputRoot = Path.GetFullPath(@"C:\Projects\MyApp");

    [Fact]
    public void Format_ReturnsEmptyForNoViolations()
    {
        var result = ViolationTextFormatter.Format([], OutputRoot);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_IncludesHeaderAndInstruction()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Klasse 'Bar' nicht sealed")
        };

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

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

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

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

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

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

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

        var fooIdx = result.IndexOf("### src/Foo.cs", StringComparison.OrdinalIgnoreCase);
        var zooIdx = result.IndexOf("### src/Zoo.cs", StringComparison.OrdinalIgnoreCase);
        var line5Idx = result.IndexOf("Z.5 EnforceSealedClasses", StringComparison.OrdinalIgnoreCase);
        var line20Idx = result.IndexOf("Z.20 EnforceSealedClasses", StringComparison.OrdinalIgnoreCase);

        Assert.True(fooIdx >= 0, "### src/Foo.cs not found");
        Assert.True(zooIdx >= 0, "### src/Zoo.cs not found");
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

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

        Assert.Contains("### src/Core/Bar.cs", result);
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

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

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

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

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

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

        Assert.DoesNotContain("## Strukturelle Verstöße", result);
    }

    [Fact]
    public void Format_StructuralViolationsAlsoAppearInViolationsByFile()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 9, "MaxPartialClassFiles", "Auf 5 Dateien verteilt")
        };

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

        var structuralIdx = result.IndexOf("## Strukturelle Verstöße", StringComparison.Ordinal);
        var byFileIdx = result.IndexOf("## Violations nach Datei", StringComparison.Ordinal);
        var fooInByFile = result.IndexOf("### src/Foo.cs", byFileIdx, StringComparison.OrdinalIgnoreCase);

        Assert.True(structuralIdx < byFileIdx);
        Assert.True(fooInByFile >= 0, "Structural violation must also appear in Violations nach Datei");
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
}
