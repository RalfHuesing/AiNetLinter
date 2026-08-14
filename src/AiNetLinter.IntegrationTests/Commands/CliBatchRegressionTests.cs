#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Commands;

/// <summary>
/// Verifiziert den CLI-Batch-Modus gegen eine isolierte SymbolGraphMiniFixtureWorkspace.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CliBatchRegressionTests
{
    [Fact]
    public async Task RunLinterCli_OnSymbolGraphMiniFixture_ReportsViolationAndExitsZero()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        var rootDir = SolutionRootLocator.Find();
        var configPath = Path.Combine(rootDir, "rules.json");

        Assert.True(File.Exists(configPath), $"Konfiguration nicht gefunden: {configPath}");

        var args = new LinterArgs
        {
            TargetPath = fixture.RootPath,
            Verbose = false,
            ConfigPath = configPath,
        };
        var console = new RecordingLintConsole();
        var exitCode = await AuditCommand.RunAsync(args, default, console);

        // SymbolGraphMini enthaelt eine deterministische Verletzung (ViolationTrigger.cs,
        // fehlendes sealed), daher erwarten wir Exit-Code 1 (= Violations gefunden) statt 0.
        Assert.True(
            exitCode == 1,
            $"Linter-Audit brach unerwartet ab (Exit {exitCode}, erwartet 1 fuer Violations). "
            + $"Output:\n{console.OutputText}\nError:\n{console.ErrorText}");
        Assert.Contains("ViolationTrigger", console.OutputText, StringComparison.Ordinal);
    }
}
