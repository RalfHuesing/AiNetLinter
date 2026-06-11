using System.Diagnostics;
using AiNetLinter.Suppression;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

public sealed class DisableAllCliTests
{
    [Fact]
    public void AddDisableAll_OnViolatingFixture_InjectOnlyIntoViolatingFiles()
    {
        var fixtureRoot = GetFixtureRoot();
        var configPath = Path.Combine(fixtureRoot, "rules.json");
        var violatingFile = Path.Combine(fixtureRoot, "src", "BaselineMini", "ViolatingClass.cs");
        var originalContent = File.ReadAllText(violatingFile);
        try
        {
            var result = RunLinter(
                $"--config \"{configPath}\" --path \"{fixtureRoot}\" --add-disable-all");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("OK", result.Output);
            Assert.StartsWith("// ainetlinter-disable all", File.ReadAllText(violatingFile));
        }
        finally
        {
            File.WriteAllText(violatingFile, originalContent);
        }
    }

    [Fact]
    public void RemoveDisableAll_OnFixture_RemovesExactDisableAllLine()
    {
        var fixtureRoot = GetFixtureRoot();
        var violatingFile = Path.Combine(fixtureRoot, "src", "BaselineMini", "ViolatingClass.cs");
        var originalContent = File.ReadAllText(violatingFile);
        var injectedContent = DisableAllCommentInjector.PrependDisableAll(originalContent);
        try
        {
            File.WriteAllText(violatingFile, injectedContent);

            var result = RunLinter($"--path \"{fixtureRoot}\" --remove-disable-all");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(originalContent, File.ReadAllText(violatingFile));
        }
        finally
        {
            File.WriteAllText(violatingFile, originalContent);
        }
    }

    [Fact]
    public async Task Main_AddDisableAllWithBaseline_ReturnsExitCodeOne()
    {
        var exitCode = await AiNetLinter.Program.Main(new[]
        {
            "--path", ".",
            "--add-disable-all",
            "--baseline", "out.json",
        });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Main_AddAndRemoveDisableAll_ReturnsExitCodeOne()
    {
        var exitCode = await AiNetLinter.Program.Main(new[]
        {
            "--path", ".",
            "--add-disable-all",
            "--remove-disable-all",
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
