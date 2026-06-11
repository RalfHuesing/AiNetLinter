using AiNetLinter.Baseline;
using AiNetLinter.Models;
using Xunit;

namespace AiNetLinter.Tests.Baseline;

public sealed class BaselineViolationFilterTests
{
    [Fact]
    public void Filter_SuppressesUnchangedFiles()
    {
        var outputRoot = @"C:\repo";
        var violations = new[]
        {
            CreateViolation(@"C:\repo\src\A.cs"),
            CreateViolation(@"C:\repo\src\B.cs"),
        };
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "src/B.cs" };

        var filtered = BaselineViolationFilter.Filter(violations, changed, outputRoot);

        Assert.Single(filtered);
        Assert.Equal(@"C:\repo\src\B.cs", filtered.First().FilePath);
    }

    [Fact]
    public void Filter_NoChangedFiles_ReturnsEmpty()
    {
        var violations = new[] { CreateViolation(@"C:\repo\src\A.cs") };
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var filtered = BaselineViolationFilter.Filter(violations, changed, @"C:\repo");

        Assert.Empty(filtered);
    }

    private static RuleViolation CreateViolation(string filePath)
    {
        return new RuleViolation
        {
            FilePath = filePath,
            LineNumber = 1,
            RuleName = "EnforceSealedClasses",
            Details = "test",
            Guidance = "test",
        };
    }
}
