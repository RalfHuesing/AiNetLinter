#nullable enable

using System;
using System.IO;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.IntegrationTests.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Commands;

/// <summary>
/// Tests für <see cref="SyncAgentRulesCommand"/>.
/// </summary>
[Collection("ConsoleTestCollection")]
[Trait("Category", "Integration")]
public sealed class SyncAgentRulesFileIntegrationTests
{
    [Fact]
    public void Run_WriteMode_CreatesFile()
    {
        using var tempDir = TestTempDirectory.Create("SyncAgentRulesTest_");
        var rulesPath = Path.Combine(SolutionRootLocator.Find(), "rules.json");

        var args = new LinterArgs
        {
            TargetPath = tempDir.DirectoryPath,
            ConfigPath = rulesPath,
            Verbose = false,
            SyncAgentRules = true,
        };

        var result = SyncAgentRulesCommand.Run(args);
        Assert.Equal(0, result);

        var mdcPath = Path.Combine(tempDir.DirectoryPath, ".agents", "rules", "AiNetLinter.mdc");
        Assert.True(File.Exists(mdcPath), "Die .mdc-Datei sollte erstellt worden sein.");
    }
}
