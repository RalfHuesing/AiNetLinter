#nullable enable

using System;
using System.IO;
using System.Text;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.IntegrationTests.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Commands;

/// <summary>
/// Tests für <see cref="SyncAgentRulesCommand"/>. <c>Run_CheckMode_WhenFileNotExists_ReturnsOne</c>
/// leitet <see cref="Console.Error"/> via <see cref="Console.SetError"/> um, um die Fehlerausgabe
/// im Check-Modus ohne vorhandene .mdc-Datei zu prüfen — parallel laufende Tests würden sich die
/// globale Konsolenumleitung gegenseitig überschreiben.
/// </summary>
[Collection("ConsoleTestCollection")]
[Trait("Category", "Integration")]
public sealed class SyncAgentRulesFileIntegrationTests
{
    [Fact]
    public void Run_CheckMode_WhenFileNotExists_ReturnsOne()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "SyncAgentRulesTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);

        // Erstelle ein minimales rules.json damit Config geladen werden kann
        var rulesPath = Path.Combine(tmpDir, "rules.json");
        File.WriteAllText(rulesPath, "{}", Encoding.UTF8);

        var args = new LinterArgs
        {
            TargetPath = tmpDir,
            ConfigPath = rulesPath,
            Verbose = false,
            Check = true,
            SyncAgentRules = true,
        };

        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);
        try
        {
            var result = SyncAgentRulesCommand.Run(args);
            Assert.Equal(1, result);
        }
        finally
        {
            Console.SetError(originalError);
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WriteMode_CreatesFile()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "SyncAgentRulesTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);

        var rulesPath = Path.Combine(SolutionRootLocator.Find(), "rules.json");

        var args = new LinterArgs
        {
            TargetPath = tmpDir,
            ConfigPath = rulesPath,
            Verbose = false,
            Check = false,
            SyncAgentRules = true,
        };

        try
        {
            var result = SyncAgentRulesCommand.Run(args);
            Assert.Equal(0, result);

            var mdcPath = Path.Combine(tmpDir, ".agents", "rules", "AiNetLinter.mdc");
            Assert.True(File.Exists(mdcPath), "Die .mdc-Datei sollte erstellt worden sein.");
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }
}
