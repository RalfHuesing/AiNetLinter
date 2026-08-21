#nullable enable

using AiNetLinter.Cli;

namespace AiNetLinter.FastTests.Cli;

[Trait("Category", "Unit")]
public sealed class CliCommandBuilderAnalyzeMcpLogTests
{
    [Fact]
    public void AnalyzeMcpLog_ParsesPathAndFormat()
    {
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(["--analyze-mcp-log", "logs", "--format", "json"]);

        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);

        Assert.Equal("logs", parsedArgs.AnalyzeMcpLog);
        Assert.Equal("json", parsedArgs.Format);
        Assert.True(parsedArgs.FormatSpecified);
    }

    [Fact]
    public void AnalyzeMcpLog_IsStandaloneWithoutSolutionPath()
    {
        var args = new LinterArgs
        {
            TargetPath = string.Empty,
            Verbose = false,
            AnalyzeMcpLogPath = "logs",
        };

        Assert.Null(args.Validate());
    }

    [Fact]
    public void FormatWithoutAnalyzeMcpLogIsRejected()
    {
        var args = new LinterArgs
        {
            TargetPath = "solution.slnx",
            Verbose = false,
            McpLogFormatSpecified = true,
        };

        Assert.Contains("--format erfordert --analyze-mcp-log", args.Validate());
    }
}
