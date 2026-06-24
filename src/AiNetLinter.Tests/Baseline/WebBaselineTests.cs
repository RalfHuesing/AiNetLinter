#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using AiNetLinter.Baseline;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Baseline;

[Collection("ConsoleTestCollection")]
public sealed class WebBaselineTests
{
    [Fact]
    public void CreateBaseline_WithWebEnabled_IncludesWebFiles()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var baselinePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-web-baseline-{Guid.NewGuid():N}.json");

        try
        {
            // 1. Enable Web features in rules.json
            var rulesJson = File.ReadAllText(workspace.ConfigPath);
            rulesJson = rulesJson.Replace("\"IsEnabled\": false", "\"IsEnabled\": true");
            File.WriteAllText(workspace.ConfigPath, rulesJson);

            // 2. Create some web files in the workspace project directory
            var projectDir = Path.Combine(workspace.RootPath, "src", "BaselineMini");
            var cssPath = Path.Combine(projectDir, "styles.css");
            var razorPath = Path.Combine(projectDir, "Component.razor");

            File.WriteAllText(cssPath, "body { color: red; }");
            File.WriteAllText(razorPath, "<h3>Component</h3>");

            // 3. Create baseline using CLI
            var createResult = RunLinter(
                $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --create-baseline \"{baselinePath}\"");

            Assert.Equal(0, createResult.ExitCode);
            Assert.True(File.Exists(baselinePath));

            var baseline = BaselineReader.Read(baselinePath);

            // 4. Verify web files are in baseline files list
            var relativeCss = Path.GetRelativePath(workspace.RootPath, cssPath).Replace('\\', '/');
            var relativeRazor = Path.GetRelativePath(workspace.RootPath, razorPath).Replace('\\', '/');

            Assert.True(baseline.Files.ContainsKey(relativeCss), $"Baseline should contain CSS file: {relativeCss}");
            Assert.True(baseline.Files.ContainsKey(relativeRazor), $"Baseline should contain Razor file: {relativeRazor}");
        }
        finally
        {
            DeleteIfExists(baselinePath);
        }
    }

    [Fact]
    public void AuditWithBaseline_ChangedWebFile_ReportsViolationsAndUpdatesBaseline()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var baselinePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-web-baseline-{Guid.NewGuid():N}.json");

        try
        {
            // 1. Enable Web features in rules.json and set MaxCssLineCount to a small value (e.g., 2)
            var rulesJson = File.ReadAllText(workspace.ConfigPath);
            rulesJson = rulesJson.Replace("\"IsEnabled\": false", "\"IsEnabled\": true");
            rulesJson = rulesJson.Replace("\"MaxCssLineCount\": 300", "\"MaxCssLineCount\": 2");
            File.WriteAllText(workspace.ConfigPath, rulesJson);

            // 2. Create web files in the workspace project directory
            var projectDir = Path.Combine(workspace.RootPath, "src", "BaselineMini");
            var cssPath = Path.Combine(projectDir, "styles.css");
            
            // CSS has 1 line initially (no violation)
            File.WriteAllText(cssPath, ".btn { color: blue; }");

            // 3. Create baseline using CLI
            var createResult = RunLinter(
                $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --create-baseline \"{baselinePath}\"");
            Assert.Equal(0, createResult.ExitCode);

            // 4. Modify css file to violate the MaxCssLineCount rule (3 lines > 2 limit)
            File.WriteAllText(cssPath, ".btn {" + Environment.NewLine + "  color: blue;" + Environment.NewLine + "}");

            // 5. Audit - should report the CSS violation on the changed file
            var auditResult = RunLinter(
                $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --baseline \"{baselinePath}\"");

            Assert.Equal(1, auditResult.ExitCode);
            Assert.Contains("CSS_MaxCssLineCount", auditResult.Output);

            // 6. Baseline should have been updated with the new checksum
            var baselineAfter = BaselineReader.Read(baselinePath);
            
            // 7. Run audit again - should pass because the baseline has been updated to include the change
            var secondAuditResult = RunLinter(
                $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --baseline \"{baselinePath}\"");
            Assert.Equal(0, secondAuditResult.ExitCode);
        }
        finally
        {
            DeleteIfExists(baselinePath);
        }
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
