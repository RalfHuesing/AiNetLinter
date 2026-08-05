#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Commands;
using AiNetLinter.Tests.Output;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// Unit-Tests fuer die Verdrahtung des <c>--mcp-log</c>-Flags in <see cref="McpServerCommand"/>:
/// verifiziert die Pfad-Aufloesung (absolut vs. relativ zum Solution-Verzeichnis), die
/// Default-Pfad-Konvention bei Whitespace-Wert und dass bei nicht gesetztem Flag kein
/// <see cref="AiNetLinter.Mcp.McpCallLog"/> instanziiert wird.
/// Diese Tests beruehren bewusst nur die statischen <see cref="McpServerCommand.TryCreateCallLog"/>-
/// und <see cref="McpServerCommand.BuildDefaultLogPath"/>-Helfermethoden, ohne einen Subprozess zu spawnen.
/// </summary>
public sealed class McpServerCommandCallLogTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void TryCreateCallLog_PathNotSet_ReturnsNull()
    {
        var solutionPath = Path.Combine(Path.GetTempPath(), "non-existent-fake.slnx");
        var console = new TestLintConsole();
        var exeDir = MakeExeDir();

        var result = McpServerCommand.TryCreateCallLog(null, solutionPath, exeDir, console);

        Assert.Null(result);
        Assert.Empty(console.Errors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TryCreateCallLog_RelativePath_CreatesLogFileRelativeToSolutionDir()
    {
        var solutionDir = Path.Combine(Path.GetTempPath(), "mcp-log-rel-" + Guid.NewGuid().ToString("N"));
        var solutionPath = Path.Combine(solutionDir, "Only.slnx");
        var relativeLog = ".mcp-log/calls.log";
        var exeDir = MakeExeDir();
        var console = new TestLintConsole();
        try
        {
            Directory.CreateDirectory(solutionDir);
            File.WriteAllText(solutionPath, "");

            await using var log = McpServerCommand.TryCreateCallLog(relativeLog, solutionPath, exeDir, console);

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
        var exeDir = MakeExeDir();
        var console = new TestLintConsole();
        try
        {
            await using var log = McpServerCommand.TryCreateCallLog(tempFile, fakeSolution, exeDir, console);

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
    public async Task TryCreateCallLog_WhitespacePath_CreatesDefaultLog()
    {
        var solutionDir = Path.Combine(Path.GetTempPath(), "mcp-log-default-" + Guid.NewGuid().ToString("N"));
        var solutionPath = Path.Combine(solutionDir, "Only.slnx");
        var exeDir = MakeExeDir();
        var console = new TestLintConsole();
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        try
        {
            Directory.CreateDirectory(solutionDir);
            File.WriteAllText(solutionPath, "");

            await using var log = McpServerCommand.TryCreateCallLog("   ", solutionPath, exeDir, console);

            Assert.NotNull(log);
            Assert.Empty(console.Errors);
            var expected = Path.Combine(exeDir, "logs", "Only", today, "calls.jsonl");
            Assert.Equal(expected, log!.LogPath);
        }
        finally
        {
            TryDelete(solutionDir);
            TryDelete(Path.Combine(exeDir, "logs"));
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull()
    {
        var exeDir = MakeExeDir();
        var console = new TestLintConsole();

        var result = McpServerCommand.TryCreateCallLog("   ", null, exeDir, console);

        Assert.Null(result);
        var error = Assert.Single(console.Errors);
        Assert.Contains("[ERROR]:", error);
        Assert.Contains("RESOURCE_NOT_FOUND", error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildDefaultLogPath_WithSolution_IncludesSolutionName()
    {
        var console = new TestLintConsole();
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        var result = McpServerCommand.BuildDefaultLogPath(
            Path.Combine("repo", "MyApp.slnx"),
            Path.Combine("opt", "ainet"),
            console);

        Assert.NotNull(result);
        Assert.Empty(console.Errors);
        var expected = Path.Combine("opt", "ainet", "logs", "MyApp", today, "calls.jsonl");
        Assert.Equal(expected, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildDefaultLogPath_DateIsLocal()
    {
        var console = new TestLintConsole();
        var localToday = DateTime.Now.ToString("yyyy-MM-dd");

        var result = McpServerCommand.BuildDefaultLogPath(
            Path.Combine("repo", "MyApp.slnx"),
            Path.Combine("opt", "ainet"),
            console);

        Assert.NotNull(result);
        Assert.Empty(console.Errors);
        var expectedDateSegment = Path.Combine("logs", "MyApp", localToday, "calls.jsonl");
        Assert.EndsWith(expectedDateSegment, result);
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

    private static string MakeExeDir()
    {
        return Path.Combine(Path.GetTempPath(), "mcp-log-exe-" + Guid.NewGuid().ToString("N"));
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
