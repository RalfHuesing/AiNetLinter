#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Cli;

/// <summary>
/// CLI-Integrationstests gegen kontrollierte Fixtures.
/// </summary>
[Collection("ConsoleTestCollection")]
[Trait("Category", "Integration")]
public sealed class CliFixtureIntegrationTests
{
    [Fact]
    public async Task SyncAgentRules_WithViolations_RunsLintAndReturnsExitCodeOneAndSyncsRules()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();

        var tempAgentRulesDir = Path.Combine(workspace.RootPath, ".agents", "rules");
        Directory.CreateDirectory(tempAgentRulesDir);
        var expectedMdcPath = Path.Combine(tempAgentRulesDir, "AiNetLinter.mdc");

        var args = new LinterArgs
        {
            TargetPath = workspace.RootPath,
            Verbose = false,
            ConfigPath = workspace.ConfigPath,
            SyncAgentRules = true,
        };
        var console = new RecordingLintConsole();
        var exitCode = await AuditCommand.RunAsync(args, default, console);

        Assert.Equal(1, exitCode);
        Assert.Contains("EnforceSealedClasses", console.OutputText);
        Assert.True(File.Exists(expectedMdcPath), $"MDC-Datei wurde nicht erzeugt unter: {expectedMdcPath}. Output:\n{console.OutputText}");
    }

    [Fact]
    public void SyncAgentRulesOnly_WithViolations_ReturnsSuccessAndSyncsRules()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();

        var tempAgentRulesDir = Path.Combine(workspace.RootPath, ".agents", "rules");
        Directory.CreateDirectory(tempAgentRulesDir);
        var expectedMdcPath = Path.Combine(tempAgentRulesDir, "AiNetLinter.mdc");

        var args = new LinterArgs
        {
            TargetPath = workspace.RootPath,
            Verbose = false,
            ConfigPath = workspace.ConfigPath,
            SyncAgentRulesOnly = true,
        };
        var console = new RecordingLintConsole();
        var exitCode = SyncAgentRulesCommand.Run(args, console);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("EnforceSealedClasses", console.OutputText);
        Assert.True(File.Exists(expectedMdcPath), $"MDC-Datei wurde nicht erzeugt unter: {expectedMdcPath}");
    }


    [Fact]
    public async Task RunLinterCli_WithInvalidConfig_ReturnsErrorExitCode()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var configPath = Path.Combine(workspace.RootPath, "non-existent-config.json");

        var args = new LinterArgs
        {
            TargetPath = workspace.RootPath,
            Verbose = false,
            ConfigPath = configPath,
        };

        var originalError = System.Console.Error;
        using var errorWriter = new StringWriter();
        System.Console.SetError(errorWriter);
        int exitCode;
        try
        {
            exitCode = await AuditCommand.RunAsync(args);
        }
        finally
        {
            System.Console.SetError(originalError);
        }

        Assert.True(exitCode == 1, $"Linter sollte mit Exit-Code 1 fehlschlagen, beendete aber mit {exitCode}.");
        Assert.Contains("[ERROR]", errorWriter.ToString());
    }
}
