#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.IntegrationTests.Fixtures;
using Xunit;

namespace AiNetLinter.IntegrationTests.Commands;

/// <summary>
/// Tests für <see cref="PlaybookCheckCommand"/>. Leitet <see cref="Console.Error"/> via
/// <see cref="Console.SetError"/> um, um die "[ERROR]"-Meldung bei fehlender Playbook-Datei
/// zu prüfen — parallel laufende Tests würden sich die globale Konsolenumleitung gegenseitig
/// überschreiben.
/// </summary>
[Collection("ConsoleTestCollection")]
[Trait("Category", "Integration")]
public sealed class PlaybookCheckCommandTests
{
    [Fact]
    public async Task RunAsync_WhenPlaybookFileNotExists_ReturnsOne()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var slnxPath = Path.Combine(workspace.RootPath, "BaselineMini.slnx");

        var args = new LinterArgs
        {
            TargetPath = slnxPath,
            ConfigPath = null,
            Verbose = false,
            Check = true,
            PlaybookPath = Path.Combine(TestTempDirectory.RootTempDirectory, $"nonexistent-{Guid.NewGuid()}.md"),
        };

        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);
        try
        {
            var result = await PlaybookCheckCommand.RunAsync(args);
            Assert.Equal(1, result);
            Assert.Contains("[ERROR]", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
