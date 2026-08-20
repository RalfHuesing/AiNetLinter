#nullable enable

using AiNetLinter.Models;
using AiNetLinter.Suppression;
using Xunit;

namespace AiNetLinter.IntegrationTests.Suppression;

[Trait("Category", "Integration")]
public sealed class ViolationPathResolverTests
{
    [Fact]
    public void ResolveAbsolutePaths_ReturnsDistinctExistingFiles()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-path-");
        var filePath = tempDir.CreateFile("src/App/Worker.cs", "namespace App;");

        var violations = new[]
        {
            CreateViolation("src/App/Worker.cs"),
            CreateViolation("src/App/Worker.cs"),
        };

        var resolved = ViolationPathResolver.ResolveAbsolutePaths(violations, tempDir.DirectoryPath);

        Assert.Single(resolved);
        Assert.Equal(filePath, resolved[0]);
    }

    private static RuleViolation CreateViolation(string relativePath)
    {
        return new RuleViolation
        {
            FilePath = relativePath,
            LineNumber = 1,
            RuleName = "EnforceSealedClasses",
            Details = "test",
            Guidance = "test",
        };
    }
}
