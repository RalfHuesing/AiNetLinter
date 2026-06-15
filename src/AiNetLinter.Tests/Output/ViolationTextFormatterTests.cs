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
    public void Format_IncludesSummarySectionsBeforeViolations()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 10, "MaxLineCount", "Zu lang")
        };

        var result = ViolationTextFormatter.Format(violations, OutputRoot);
        var summaryByFileIndex = result.IndexOf("## Summary - by file", StringComparison.Ordinal);
        var summaryByRuleIndex = result.IndexOf("## Summary - by rule", StringComparison.Ordinal);
        var violationsIndex = result.IndexOf("## Violations", StringComparison.Ordinal);
        var detailIndex = result.IndexOf("src/Foo.cs:5 EnforceSealedClasses", StringComparison.Ordinal);

        Assert.True(summaryByFileIndex >= 0);
        Assert.True(summaryByRuleIndex > summaryByFileIndex);
        Assert.True(violationsIndex > summaryByRuleIndex);
        Assert.True(detailIndex > violationsIndex);
        Assert.Contains("2 src/Foo.cs", result);
        Assert.Contains("| EnforceSealedClasses | 1 |", result);
        Assert.Contains("| MaxLineCount | 1 |", result);
    }

    [Fact]
    public void Format_SortsByFilePathThenLineNumber()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Zoo.cs", 10, "MaxLineCount", "Zu lang"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 20, "EnforceSealedClasses", "Nicht sealed"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Nicht sealed")
        };

        var result = ViolationTextFormatter.Format(violations, OutputRoot);
        var violationsSection = result[(result.IndexOf("## Violations", StringComparison.Ordinal) + "## Violations".Length)..]
            .TrimStart('\n');
        var lines = violationsSection.Split('\n');

        Assert.Equal("src/Foo.cs:5 EnforceSealedClasses | Nicht sealed -> Guidance text", lines[0]);
        Assert.Equal("src/Foo.cs:20 EnforceSealedClasses | Nicht sealed -> Guidance text", lines[1]);
        Assert.Equal("src/Zoo.cs:10 MaxLineCount | Zu lang -> Guidance text", lines[2]);
    }

    [Fact]
    public void Format_UsesRelativePathsAndCompactLineFormat()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Core\Bar.cs", 12, "MaxMethodParameterCount", "6 Parameter")
        };

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

        Assert.Contains("src/Core/Bar.cs:12 MaxMethodParameterCount | 6 Parameter", result);
    }

    [Fact]
    public void Format_IncludesDynamicRuleInstructions()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "EnforceSealedClasses", "Klasse 'Bar' nicht sealed")
        };

        var result = ViolationTextFormatter.Format(violations, OutputRoot);

        Assert.Contains("-> EnforceSealedClasses: Konkrete Klassen muessen 'sealed' sein.", result);
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
