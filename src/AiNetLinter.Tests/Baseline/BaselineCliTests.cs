using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Output;
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
            var args = new LinterArgs
            {
                TargetPath = fixtureRoot,
                Verbose = false,
                CreateBaselinePath = baselinePath,
            };
            var console = new TestLintConsole();
            var exitCode = await MaintenanceCommand.TryRunAsync(args, default, console);

            Assert.Equal(0, exitCode);
            Assert.Contains("OK", console.OutputText);
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
            var createArgs = new LinterArgs
            {
                TargetPath = fixtureRoot,
                Verbose = false,
                CreateBaselinePath = baselinePath,
            };
            var createExitCode = await MaintenanceCommand.TryRunAsync(createArgs, default, new TestLintConsole());
            Assert.Equal(0, createExitCode);

            var auditArgs = new LinterArgs
            {
                TargetPath = fixtureRoot,
                Verbose = false,
                ConfigPath = configPath,
                BaselinePath = baselinePath,
            };
            var auditConsole = new TestLintConsole();
            var auditExitCode = await AuditCommand.RunAsync(auditArgs, default, auditConsole);

            Assert.Equal(0, auditExitCode);
            Assert.Contains("OK", auditConsole.OutputText);
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
            var createArgs = new LinterArgs
            {
                TargetPath = workspace.RootPath,
                Verbose = false,
                CreateBaselinePath = baselinePath,
            };
            await MaintenanceCommand.TryRunAsync(createArgs, default, new TestLintConsole());
            var baselineBefore = BaselineReader.Read(baselinePath);
            var relativePath = baselineBefore.Files.Keys.First(k => k.EndsWith("ViolatingClass.cs", StringComparison.OrdinalIgnoreCase));

            File.WriteAllText(workspace.ViolatingClassPath, originalContent + Environment.NewLine);

            var auditArgs = new LinterArgs
            {
                TargetPath = workspace.RootPath,
                Verbose = false,
                ConfigPath = workspace.ConfigPath,
                BaselinePath = baselinePath,
            };
            var auditConsole = new TestLintConsole();
            var auditExitCode = await AuditCommand.RunAsync(auditArgs, default, auditConsole);

            Assert.Equal(1, auditExitCode);
            Assert.Contains("EnforceSealedClasses", auditConsole.OutputText);

            var baselineAfter = BaselineReader.Read(baselinePath);
            Assert.NotEqual(baselineBefore.Files[relativePath], baselineAfter.Files[relativePath]);

            var secondAuditConsole = new TestLintConsole();
            var secondAuditExitCode = await AuditCommand.RunAsync(auditArgs, default, secondAuditConsole);
            Assert.Equal(0, secondAuditExitCode);
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
