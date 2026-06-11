using System.Diagnostics;
using AiNetLinter.Suppression;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

public sealed class DisableAllCliTests
{
    [Fact]
    public void AddDisableAll_OnViolatingFixture_InjectOnlyIntoViolatingFiles()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();

        var result = RunLinter(
            $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --add-disable-all");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("OK", result.Output);
        Assert.StartsWith("// ainetlinter-disable all", File.ReadAllText(workspace.ViolatingClassPath));
    }

    [Fact]
    public void RemoveDisableAll_OnFixture_RemovesExactDisableAllLine()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var originalContent = File.ReadAllText(workspace.ViolatingClassPath);
        File.WriteAllText(workspace.ViolatingClassPath, DisableAllCommentInjector.PrependDisableAll(originalContent));

        var result = RunLinter($"--path \"{workspace.RootPath}\" --remove-disable-all");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(originalContent, File.ReadAllText(workspace.ViolatingClassPath));
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
