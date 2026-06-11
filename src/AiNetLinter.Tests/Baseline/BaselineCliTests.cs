using System.Diagnostics;
using AiNetLinter.Baseline;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Baseline;

public sealed class BaselineCliTests
{
    [Fact]
    public void CreateBaseline_WithoutConfig_WritesJsonAndReturnsSuccess()
    {
        var fixtureRoot = GetFixtureRoot();
        var baselinePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-baseline-{Guid.NewGuid():N}.json");
        try
        {
            var result = RunLinter(
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
    public void AuditWithBaseline_UnchangedFiles_ReturnsSuccess()
    {
        var fixtureRoot = GetFixtureRoot();
        var baselinePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-baseline-{Guid.NewGuid():N}.json");
        var configPath = Path.Combine(fixtureRoot, "rules.json");
        try
        {
            var createResult = RunLinter(
                $"--path \"{fixtureRoot}\" --create-baseline \"{baselinePath}\"");
            Assert.Equal(0, createResult.ExitCode);

            var auditResult = RunLinter(
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
    public void AuditWithBaseline_ChangedFile_ReportsViolationsAndUpdatesBaseline()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var baselinePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-baseline-{Guid.NewGuid():N}.json");
        var originalContent = File.ReadAllText(workspace.ViolatingClassPath);
        try
        {
            RunLinter($"--path \"{workspace.RootPath}\" --create-baseline \"{baselinePath}\"");
            var baselineBefore = BaselineReader.Read(baselinePath);
            var relativePath = baselineBefore.Files.Keys.First(k => k.EndsWith("ViolatingClass.cs", StringComparison.OrdinalIgnoreCase));

            File.WriteAllText(workspace.ViolatingClassPath, originalContent + Environment.NewLine);

            var auditResult = RunLinter(
                $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --baseline \"{baselinePath}\"");

            Assert.Equal(1, auditResult.ExitCode);
            Assert.Contains("EnforceSealedClasses", auditResult.Output);

            var baselineAfter = BaselineReader.Read(baselinePath);
            Assert.NotEqual(baselineBefore.Files[relativePath], baselineAfter.Files[relativePath]);

            var secondAudit = RunLinter(
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

    private static (int ExitCode, string Output, string Error) RunLinter(string arguments)
    {
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(processInfo);
        Assert.NotNull(process);

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output, error);
    }

    private static string GetFixtureRoot()
    {
        var root = FindSolutionRoot();
        return Path.Combine(root, "tests", "Fixtures", "BaselineMini");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Solution root not found.");
    }

    private static string FindLinterDll(string rootDir)
    {
        var binDir = Path.Combine(rootDir, "src", "AiNetLinter", "bin");
        var files = Directory.GetFiles(binDir, "AiNetLinter.dll", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            throw new FileNotFoundException("AiNetLinter.dll not found.");
        }

        return files.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }
}
