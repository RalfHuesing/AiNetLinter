#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Cli;

[Trait("Category", "Dogfood")]
public sealed class CliRepositoryDogfoodTests
{
    [Fact]
    public async Task RunLinterCli_OnWholeSolution_ReturnsSuccess()
    {
        var rootDir = SolutionRootLocator.Find();
        var linterDllPath = CliProcessRunner.FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "rules.json");
        var targetPath = rootDir;

        Assert.True(File.Exists(linterDllPath), $"Linter-DLL nicht gefunden unter: {linterDllPath}");
        Assert.True(File.Exists(configPath), $"Konfigurationsdatei nicht gefunden unter: {configPath}");
        Assert.True(Directory.Exists(targetPath), $"Zielverzeichnis nicht gefunden unter: {targetPath}");

        var result = await CliProcessRunner.RunLinterAsync($"--config \"{configPath}\" --path \"{targetPath}\"");

        Assert.Null(result.Error == "" ? null : result.Error);
        Assert.True(result.ExitCode == 0, $"Linter schlug mit Exit-Code {result.ExitCode} fehl. Output:\n{result.Output}\nError:\n{result.Error}");
    }

    [Fact]
    public async Task GeneratePlaybook_ForSolution_GeneratesAndUpdatesPlaybook()
    {
        var rootDir = SolutionRootLocator.Find();
        var configPath = Path.Combine(rootDir, "rules.json");
        var playbookFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");

        Assert.True(File.Exists(configPath), $"Konfigurationsdatei nicht gefunden unter: {configPath}");

        try
        {
            var args = new LinterArgs
            {
                TargetPath = rootDir,
                Verbose = false,
                ConfigPath = configPath,
                PlaybookPath = playbookFile,
            };
            var console = new RecordingLintConsole();
            var exitCode = await AuditCommand.RunAsync(args, default, console);

            Assert.True(exitCode == 0, $"Linter schlug mit Exit-Code {exitCode} fehl. Output:\n{console.OutputText}\nError:\n{console.ErrorText}");
            Assert.True(File.Exists(playbookFile), $"Playbook-Datei wurde nicht erzeugt unter: {playbookFile}");

            var content = File.ReadAllText(playbookFile);
            Assert.Contains("Auto-generiert durch AiNetLinter", content);
            Assert.Contains("AI Repository Playbook (Auto-Generated)", content);
        }
        finally
        {
            if (File.Exists(playbookFile))
            {
                File.Delete(playbookFile);
            }
        }
    }

    [Fact]
    public async Task SyncAgentRulesAndPlaybook_Combined_GeneratesBoth()
    {
        var rootDir = SolutionRootLocator.Find();
        var configPath = Path.Combine(rootDir, "rules.json");
        var tempPlaybookPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");

        Assert.True(File.Exists(configPath), $"Config nicht gefunden: {configPath}");

        try
        {
            var args = new LinterArgs
            {
                TargetPath = rootDir,
                Verbose = false,
                ConfigPath = configPath,
                SyncAgentRules = true,
                PlaybookPath = tempPlaybookPath,
            };
            var console = new RecordingLintConsole();
            var exitCode = await AuditCommand.RunAsync(args, default, console);

            Assert.True(exitCode == 0,
                $"Kombinierter Aufruf fehlgeschlagen (Exit {exitCode}).\nOutput: {console.OutputText}\nError: {console.ErrorText}");
            Assert.True(File.Exists(tempPlaybookPath),
                $"Playbook wurde nicht erzeugt (P0-Bug). Output: {console.OutputText}");
            var content = File.ReadAllText(tempPlaybookPath);
            Assert.Contains("AI Repository Playbook", content);
        }
        finally
        {
            if (File.Exists(tempPlaybookPath)) File.Delete(tempPlaybookPath);
        }
    }
}
