#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Commands;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// Unit-Tests fuer die Verdrahtung des <c>--mcp-log</c>-Flags in <see cref="McpServerCommand"/>:
/// verifiziert die Pfad-Aufloesung (absolut vs. relativ zum Solution-Verzeichnis) und
/// dass bei nicht gesetztem Flag kein <see cref="AiNetLinter.Mcp.McpCallLog"/> instanziiert wird.
/// Diese Tests beruehren bewusst nur die statische <see cref="McpServerCommand.TryCreateCallLog"/>-
/// Helfermethode, ohne einen Subprozess zu spawnen.
/// </summary>
public sealed class McpServerCommandCallLogTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void TryCreateCallLog_PathNotSet_ReturnsNull()
    {
        var solutionPath = Path.Combine(Path.GetTempPath(), "non-existent-fake.slnx");

        var result = McpServerCommand.TryCreateCallLog(null, solutionPath);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryCreateCallLog_WhitespacePath_ReturnsNull()
    {
        var solutionPath = Path.Combine(Path.GetTempPath(), "non-existent-fake.slnx");

        var result = McpServerCommand.TryCreateCallLog("   ", solutionPath);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TryCreateCallLog_RelativePath_CreatesLogFileRelativeToSolutionDir()
    {
        var solutionDir = Path.Combine(Path.GetTempPath(), "mcp-log-rel-" + Guid.NewGuid().ToString("N"));
        var solutionPath = Path.Combine(solutionDir, "Only.slnx");
        var relativeLog = ".mcp-log/calls.log";
        try
        {
            Directory.CreateDirectory(solutionDir);
            File.WriteAllText(solutionPath, "");

            await using var log = McpServerCommand.TryCreateCallLog(relativeLog, solutionPath);

            Assert.NotNull(log);
            // ResolveMcpLogPath muss den erwarteten absoluten Pfad liefern. Pfad-Vergleich
            // normalisiert auf Path.Combine (Backslashes), der Input nutzt Forward-Slashes.
            var expected = McpServerCommand.ResolveMcpLogPath(relativeLog, solutionPath);
            var expectedNormalized = Path.Combine(solutionDir, ".mcp-log", "calls.log");
            Assert.Equal(expectedNormalized, expected);
        }
        finally
        {
            TryDelete(solutionDir);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TryCreateCallLog_AbsolutePath_CreatesLogFileAtGivenPath()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "mcp-log-abs-" + Guid.NewGuid().ToString("N") + ".log");
        var fakeSolution = Path.Combine(Path.GetTempPath(), "fake.slnx");
        try
        {
            await using var log = McpServerCommand.TryCreateCallLog(tempFile, fakeSolution);

            Assert.NotNull(log);
            var expected = McpServerCommand.ResolveMcpLogPath(tempFile, fakeSolution);
            Assert.Equal(tempFile, expected);
        }
        finally
        {
            TryDelete(tempFile);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveMcpLogPath_AbsolutePath_ReturnsAsIs()
    {
        var absolute = Path.Combine("C:", "tmp", "x.log");
        var result = McpServerCommand.ResolveMcpLogPath(absolute, Path.Combine("D:", "sol", "x.slnx"));
        Assert.Equal(absolute, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveMcpLogPath_RelativePath_ResolvedAgainstSolutionDirectory()
    {
        var solutionDir = Path.Combine("D:", "sol");
        var solutionPath = Path.Combine(solutionDir, "My.slnx");
        var result = McpServerCommand.ResolveMcpLogPath(".mcp-log/calls.log", solutionPath);
        var expected = Path.Combine(solutionDir, ".mcp-log", "calls.log");
        Assert.Equal(expected, result);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
