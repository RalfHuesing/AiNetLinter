using AiNetLinter.Models;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

public sealed class ViolationSummaryBuilderTests
{
    private static readonly string OutputRoot = Path.GetFullPath(@"C:\Projects\MyApp");

    [Fact]
    public void BuildByFile_GroupsMultipleViolationsPerFile()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 1, "EnforceSealedClasses"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 5, "MaxLineCount"),
            CreateViolation(@"C:\Projects\MyApp\src\Bar.cs", 3, "EnforceSealedClasses")
        };

        var result = ViolationSummaryBuilder.BuildByFile(violations, OutputRoot);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].Count);
        Assert.Equal("src/Foo.cs", result[0].RelativePath);
        Assert.Equal(1, result[1].Count);
        Assert.Equal("src/Bar.cs", result[1].RelativePath);
    }

    [Fact]
    public void BuildByFile_SortsDescendingByCountThenAlphabetically()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\Zoo.cs", 1, "MaxLineCount"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 1, "EnforceSealedClasses"),
            CreateViolation(@"C:\Projects\MyApp\src\Foo.cs", 2, "EnforceSealedClasses"),
            CreateViolation(@"C:\Projects\MyApp\src\Bar.cs", 1, "EnforceSealedClasses")
        };

        var result = ViolationSummaryBuilder.BuildByFile(violations, OutputRoot);

        Assert.Equal(3, result.Count);
        Assert.Equal("src/Foo.cs", result[0].RelativePath);
        Assert.Equal(2, result[0].Count);
        Assert.Equal("src/Bar.cs", result[1].RelativePath);
        Assert.Equal("src/Zoo.cs", result[2].RelativePath);
    }

    [Fact]
    public void BuildByRule_GroupsAndSortsDescendingByCount()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\A.cs", 1, "MaxLineCount"),
            CreateViolation(@"C:\Projects\MyApp\src\B.cs", 2, "EnforceSealedClasses"),
            CreateViolation(@"C:\Projects\MyApp\src\C.cs", 3, "EnforceSealedClasses"),
            CreateViolation(@"C:\Projects\MyApp\src\D.cs", 4, "MaxLineCount")
        };

        var result = ViolationSummaryBuilder.BuildByRule(violations);

        Assert.Equal(2, result.Count);
        Assert.Equal("EnforceSealedClasses", result[0].RuleName);
        Assert.Equal(2, result[0].Count);
        Assert.Equal("MaxLineCount", result[1].RuleName);
        Assert.Equal(2, result[1].Count);
    }

    [Fact]
    public void BuildByRule_TieBreaksAlphabeticallyByRuleName()
    {
        var violations = new[]
        {
            CreateViolation(@"C:\Projects\MyApp\src\A.cs", 1, "ZebraRule"),
            CreateViolation(@"C:\Projects\MyApp\src\B.cs", 1, "AlphaRule")
        };

        var result = ViolationSummaryBuilder.BuildByRule(violations);

        Assert.Equal("AlphaRule", result[0].RuleName);
        Assert.Equal("ZebraRule", result[1].RuleName);
    }

    private static RuleViolation CreateViolation(string filePath, int line, string rule) =>
        new()
        {
            FilePath = filePath,
            LineNumber = line,
            RuleName = rule,
            Details = "details",
            Guidance = "guidance"
        };
}
