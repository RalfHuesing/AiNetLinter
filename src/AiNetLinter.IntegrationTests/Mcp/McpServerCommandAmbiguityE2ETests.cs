#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// E2E-Test fuer: ein Zielverzeichnis
/// mit dem verbotenen Legacy-Argument <c>--path</c> auf eine Datei fuehrt
/// zu einem deterministischen Server-Start-Abbruch mit klarer Fehlermeldung auf stderr.
/// beweist die Helper-Logik; dieser Test beweist das Verhalten des realen Server-Subprozesses.
///
/// Da der Server bei Mehrdeutigkeit bereits vor <c>McpServer.Create</c> abbricht, ist
/// kein <c>McpClient</c>-Connect moeglich — der Test startet den Subprozess direkt
/// und liest stderr (analog <c>McpServerCommandErrorHandlingTests.cs</c> aus.
///
/// A3-Pfad: wenn <c>FindSolutionCandidates</c> in <c>McpServerCommand</c> durch
/// <c>SourceFileCatalog.FindSolutionFile</c> ersetzt wird (das <c>files[0]</c> silent
/// zurueckliefert), dann wuerde der Server die erste Solution laden, der Exit-Code
/// waere 0, und der Test schlaegt fehl.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerCommandAmbiguityE2ETests
{
    [Fact]
    public async Task RunAsync_McpServerWithPath_AbortsWithHardCutError()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-ambiguity-");
        tempDir.CreateFile("First.slnx", "");
        tempDir.CreateFile("Second.slnx", "");

        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        Assert.True(File.Exists(exePath), $"AiNetLinter.exe nicht gefunden: {exePath}");

        var processInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--mcp-server --path \"{tempDir.DirectoryPath}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var result = await McpProcessRunner.RunAsync(processInfo, TimeSpan.FromSeconds(10));

        Assert.True(
            !result.TimedOut,
            "Server-Prozess hat nicht innerhalb 10s beendet — vermutlich blockiert er im MCP-Wartemodus.");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--path ist im MCP-Modus", result.Error, StringComparison.Ordinal);
    }
}
