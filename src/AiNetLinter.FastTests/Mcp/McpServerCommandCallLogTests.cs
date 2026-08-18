#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Commands;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Unit-Tests fuer die Verdrahtung der Observability in <see cref="McpServerCommand"/>:
/// verifiziert die Pfad-Aufloesung (absolut vs. relativ zum Solution-Verzeichnis),
/// Default-Optionen bei null/Whitespace und Deaktivierung bei "off"/"false"/"none"/"disabled".
/// </summary>
[Trait("Category", "Unit")]
public sealed class McpServerCommandCallLogTests
{
    [Fact]
    public void ResolveObservabilityOptions_Null_ReturnsDefaultEnabledOptions()
    {
        var solutionPath = Path.Combine(Path.GetTempPath(), "fake.slnx");

        var options = McpServerCommand.ResolveObservabilityOptions(null, solutionPath);

        Assert.True(options.Enabled);
        Assert.True(options.EnableToolCallLogging);
        Assert.True(options.EnableFeedbackTool);
        Assert.True(options.EnableResponseLogging);
        Assert.Equal("ainetlinter", options.ServerName);
        Assert.NotNull(options.ServerVersion);
        Assert.Null(options.LogDirectory);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("false")]
    [InlineData("disabled")]
    [InlineData("none")]
    [InlineData("OFF")]
    public void ResolveObservabilityOptions_DisabledKeywords_ReturnsDisabledOptions(string keyword)
    {
        var solutionPath = Path.Combine(Path.GetTempPath(), "fake.slnx");

        var options = McpServerCommand.ResolveObservabilityOptions(keyword, solutionPath);

        Assert.False(options.Enabled);
        Assert.False(options.EnableToolCallLogging);
        Assert.False(options.EnableFeedbackTool);
        Assert.False(options.EnableResponseLogging);
        Assert.Equal("ainetlinter", options.ServerName);
    }

    [Fact]
    public void ResolveObservabilityOptions_Whitespace_ReturnsDefaultEnabledOptions()
    {
        var solutionPath = Path.Combine(Path.GetTempPath(), "fake.slnx");

        var options = McpServerCommand.ResolveObservabilityOptions("   ", solutionPath);

        Assert.True(options.Enabled);
        Assert.True(options.EnableToolCallLogging);
        Assert.True(options.EnableFeedbackTool);
        Assert.True(options.EnableResponseLogging);
        Assert.Equal("ainetlinter", options.ServerName);
        Assert.Null(options.LogDirectory);
    }

    [Fact]
    public void ResolveObservabilityOptions_RelativePath_ResolvesRelativeToSolutionDir()
    {
        var solutionDir = Path.Combine(Path.GetTempPath(), "mcp-log-rel-" + Guid.NewGuid().ToString("N"));
        var solutionPath = Path.Combine(solutionDir, "Only.slnx");
        var relativeLog = ".mcp-log";

        var options = McpServerCommand.ResolveObservabilityOptions(relativeLog, solutionPath);

        Assert.True(options.Enabled);
        Assert.True(options.EnableToolCallLogging);
        Assert.True(options.EnableFeedbackTool);
        Assert.True(options.EnableResponseLogging);
        Assert.Equal("ainetlinter", options.ServerName);
        var expected = Path.Combine(solutionDir, ".mcp-log");
        Assert.Equal(expected, options.LogDirectory);
    }

    [Fact]
    public void ResolveObservabilityOptions_AbsolutePath_UsesAbsolutePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mcp-obs-dir-" + Guid.NewGuid().ToString("N"));
        var fakeSolution = Path.Combine(Path.GetTempPath(), "fake.slnx");

        var options = McpServerCommand.ResolveObservabilityOptions(tempDir, fakeSolution);

        Assert.True(options.Enabled);
        Assert.True(options.EnableToolCallLogging);
        Assert.True(options.EnableFeedbackTool);
        Assert.True(options.EnableResponseLogging);
        Assert.Equal("ainetlinter", options.ServerName);
        Assert.Equal(tempDir, options.LogDirectory);
    }

    [Fact]
    public void ResolveMcpLogPath_AbsolutePath_ReturnsAsIs()
    {
        var absolute = Path.Combine("C:", "tmp", "logs");
        var result = McpServerCommand.ResolveMcpLogPath(absolute, Path.Combine("D:", "sol", "x.slnx"));
        Assert.Equal(absolute, result);
    }

    [Fact]
    public void ResolveMcpLogPath_RelativePath_ResolvedAgainstSolutionDirectory()
    {
        var solutionDir = Path.Combine("D:", "sol");
        var solutionPath = Path.Combine(solutionDir, "My.slnx");
        var result = McpServerCommand.ResolveMcpLogPath(".mcp-log", solutionPath);
        var expected = Path.Combine(solutionDir, ".mcp-log");
        Assert.Equal(expected, result);
    }
}
