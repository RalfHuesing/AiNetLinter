using System.CommandLine;
using AiNetLinter.Cli;
using Xunit;

namespace AiNetLinter.Tests.Cli;

public sealed class CliCommandBuilderMcpLogTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void McpLog_NotSet_ReturnsNull()
    {
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(new[] { "--mcp-server" });

        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);

        Assert.Null(parsedArgs.McpLog);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void McpLog_Parameterless_ReturnsEmptyString()
    {
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(new[] { "--mcp-server", "--mcp-log" });

        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);

        Assert.NotNull(parsedArgs.McpLog);
        Assert.Equal(string.Empty, parsedArgs.McpLog);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void McpLog_ExplicitPath_ReturnsGivenPath()
    {
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(new[] { "--mcp-server", "--mcp-log", "custom/calls.log" });

        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);

        Assert.Equal("custom/calls.log", parsedArgs.McpLog);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void McpLog_ParameterlessFollowedByPathOption_ReturnsEmptyStringAndParsesPath()
    {
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(new[] { "--mcp-server", "--mcp-log", "--path", "San.smart.Planner.Platform.slnx" });

        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);

        Assert.NotNull(parsedArgs.McpLog);
        Assert.Equal(string.Empty, parsedArgs.McpLog);
        Assert.Equal("San.smart.Planner.Platform.slnx", parsedArgs.TargetPath);
    }
}
