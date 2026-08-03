using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Baseline;

[Trait("Category", "Integration")]
public sealed class BaselineCliTests
{
    [Fact]
    public async Task CreateBaseline_WithoutConfig_WritesJsonAndReturnsSuccess()
    {
        var fixtureRoot = GetFixtureRoot();
        var baselinePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-baseline-{Guid.NewGuid():N}.json");
        try
        {
            var result = await CliProcessRunner.RunLinterAsync(
                $"--path \"{fixtureRoot}\" --create-baseline \"{baselinePath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("OK", result.Output);
            Assert.True(File.Exists(baselinePath));

            var baseline = BaselineReader.Read(baselinePath);
            Assert.NotEmpty(baseline.Files);
        }
        finally
        {
            DeleteIfExists(baselinePath);
        }
    }

    [Fact]
    public async Task AuditWithBaseline_UnchangedFiles_ReturnsSuccess()
    {
        var fixtureRoot = GetFixtureRoot();
        var baselinePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-baseline-{Guid.NewGuid():N}.json");
        var configPath = Path.Combine(fixtureRoot, "rules.json");
        try
        {
            var createResult = await CliProcessRunner.RunLinterAsync(
                $"--path \"{fixtureRoot}\" --create-baseline \"{baselinePath}\"");
            Assert.Equal(0, createResult.ExitCode);

            var auditResult = await CliProcessRunner.RunLinterAsync(
                $"--config \"{configPath}\" --path \"{fixtureRoot}\" --baseline \"{baselinePath}\"");

            Assert.Equal(0, auditResult.ExitCode);
            Assert.Contains("OK", auditResult.Output);
        }
        finally
        {
            DeleteIfExists(baselinePath);
        }
    }

    [Fact]
    public async Task AuditWithBaseline_ChangedFile_ReportsViolationsAndUpdatesBaseline()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var baselinePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-baseline-{Guid.NewGuid():N}.json");
        var originalContent = File.ReadAllText(workspace.ViolatingClassPath);
        try
        {
            await CliProcessRunner.RunLinterAsync($"--path \"{workspace.RootPath}\" --create-baseline \"{baselinePath}\"");
            var baselineBefore = BaselineReader.Read(baselinePath);
            var relativePath = baselineBefore.Files.Keys.First(k => k.EndsWith("ViolatingClass.cs", StringComparison.OrdinalIgnoreCase));

            File.WriteAllText(workspace.ViolatingClassPath, originalContent + Environment.NewLine);

            var auditResult = await CliProcessRunner.RunLinterAsync(
                $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --baseline \"{baselinePath}\"");

            Assert.Equal(1, auditResult.ExitCode);
            Assert.Contains("EnforceSealedClasses", auditResult.Output);

            var baselineAfter = BaselineReader.Read(baselinePath);
            Assert.NotEqual(baselineBefore.Files[relativePath], baselineAfter.Files[relativePath]);

            var secondAudit = await CliProcessRunner.RunLinterAsync(
                $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --baseline \"{baselinePath}\"");
            Assert.Equal(0, secondAudit.ExitCode);
        }
        finally
        {
            DeleteIfExists(baselinePath);
        }
    }

    [Fact]
    public async Task Main_ConflictingBaselineFlags_ReturnsExitCodeOne()
    {
        var exitCode = await AiNetLinter.Program.Main(new[]
        {
            "--path", ".",
            "--create-baseline", "out.json",
            "--baseline", "out.json",
        });

        Assert.Equal(1, exitCode);
    }

    private static string GetFixtureRoot()
    {
        var root = CliProcessRunner.FindSolutionRoot();
        return Path.Combine(root, "tests", "Fixtures", "BaselineMini");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
