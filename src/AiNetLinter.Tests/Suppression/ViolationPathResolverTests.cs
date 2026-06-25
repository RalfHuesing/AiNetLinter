using AiNetLinter.Models;
using AiNetLinter.Suppression;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

public sealed class ViolationPathResolverTests
{
    [Fact]
    public void ResolveAbsolutePaths_ReturnsDistinctExistingFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ainetlinter-path-{Guid.NewGuid():N}");
        var nestedDir = Path.Combine(tempDir, "src", "App");
        Directory.CreateDirectory(nestedDir);
        var filePath = Path.Combine(nestedDir, "Worker.cs");
        File.WriteAllText(filePath, "namespace App;");

        try
        {
            var violations = new[]
            {
                CreateViolation("src/App/Worker.cs"),
                CreateViolation("src/App/Worker.cs"),
            };

            var resolved = ViolationPathResolver.ResolveAbsolutePaths(violations, tempDir);

            Assert.Single(resolved);
            Assert.Equal(filePath, resolved[0]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
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
