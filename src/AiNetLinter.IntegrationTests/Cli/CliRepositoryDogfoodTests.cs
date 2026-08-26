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
    public void SyncAgentRules_CommittedRules_AreInSyncWithRulesJson()
    {
        var rootDir = SolutionRootLocator.Find();
        var configPath = Path.Combine(rootDir, "rules.json");

        Assert.True(File.Exists(configPath), $"Config nicht gefunden: {configPath}");

        var args = new LinterArgs
        {
            TargetPath = rootDir,
            Verbose = false,
            ConfigPath = configPath,
            SyncAgentRulesOnly = true,
        };
        var console = new RecordingLintConsole();

        var exitCode = SyncAgentRulesCommand.Run(args, console);

        Assert.True(exitCode == 0,
            $"Sync schlug fehl (Exit {exitCode}). Output: {console.OutputText} Error: {console.ErrorText}");
    }

    [Fact]
    public void SyncAgentRules_WithoutConfigPath_DiscoversRulesJsonInTargetDirectory()
    {
        var rootDir = SolutionRootLocator.Find();

        var args = new LinterArgs
        {
            TargetPath = rootDir,
            Verbose = false,
            ConfigPath = null,
            SyncAgentRulesOnly = true,
        };
        var console = new RecordingLintConsole();

        var exitCode = SyncAgentRulesCommand.Run(args, console);

        Assert.True(exitCode == 0,
            $"Sync ohne --config schlug fehl (Exit {exitCode}). Output: {console.OutputText} Error: {console.ErrorText}");
        Assert.DoesNotContain("CONFIG_REQUIRED", console.ErrorText, StringComparison.Ordinal);
    }
}
