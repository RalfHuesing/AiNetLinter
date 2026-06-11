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

        Assert.StartsWith("# AiNetLinter · 1 violations", result);
        Assert.Contains("Minimaler Diff", result);
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
        var lines = result.Split('\n');

        Assert.Equal("src/Foo.cs:5 EnforceSealedClasses | Nicht sealed", lines[3]);
        Assert.Equal("src/Foo.cs:20 EnforceSealedClasses | Nicht sealed", lines[4]);
        Assert.Equal("src/Zoo.cs:10 MaxLineCount | Zu lang", lines[5]);
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
