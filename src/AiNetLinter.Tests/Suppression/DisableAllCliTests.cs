using System.Diagnostics;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

public sealed class DisableAllCliTests
{
    [Fact]
    public void AddDisableAll_OnDirectory_WritesCommentAndSkipsOnSecondRun()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ainetlinter-disable-{Guid.NewGuid():N}");
        var sourceFile = Path.Combine(tempDir, "Legacy.cs");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(sourceFile, "namespace Legacy;");
        try
        {
            var firstRun = RunLinter($"--path \"{tempDir}\" --add-disable-all");
            Assert.Equal(0, firstRun.ExitCode);
            Assert.Contains("OK", firstRun.Output);
            Assert.StartsWith("// ainetlinter-disable all", File.ReadAllText(sourceFile));

            var secondRun = RunLinter($"--path \"{tempDir}\" --add-disable-all");
            Assert.Equal(0, secondRun.ExitCode);
            Assert.Equal(1, CountDisableAllMarkers(File.ReadAllText(sourceFile)));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
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

    private static int CountDisableAllMarkers(string content)
    {
        const string marker = "// ainetlinter-disable all";
        int count = 0;
        int index = 0;
        while ((index = content.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += marker.Length;
        }

        return count;
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
