#nullable enable

using System.CommandLine;
using AiNetLinter.Cli;
using Xunit;

namespace AiNetLinter.FastTests.Cli;

[Trait("Category", "Unit")]
public sealed class ProgramParsingTests
{
    [Fact]
    public void CliCommandBuilder_Parses_AgentRulesPath()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--config", "rules.json", "--path", ".", "--agent-rules-path", "my-rules-dir" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal("my-rules-dir", parsed.AgentRulesPath);
    }

    [Fact]
    public void CliCommandBuilder_Parses_AgentRulesPath_WithAlias()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--config", "rules.json", "--path", ".", "-arp", "my-rules-dir" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal("my-rules-dir", parsed.AgentRulesPath);
    }

    [Fact]
    public void CliCommandBuilder_Parses_SyncAgentRulesOnly()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--config", "rules.json", "--path", ".", "--sync-agent-rules-only" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.True(parsed.SyncAgentRulesOnly);
    }

    [Fact]
    public void CliCommandBuilder_Parses_SyncAgentRulesOnly_WithAlias()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--config", "rules.json", "--path", ".", "-saro" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.True(parsed.SyncAgentRulesOnly);
    }

    [Fact]
    public void CliCommandBuilder_Parses_ParentPid()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server", "--parent-pid", "1234" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal(1234, parsed.ParentPid);
    }

    [Fact]
    public void CliCommandBuilder_Parses_ProjectTtl_AsDecimalInvariant()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server", "--mcp-project-ttl-minutes", "0.05" });
        Assert.Empty(result.Errors);

        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal(0.05m, parsed.McpProjectTtlMinutes);
    }

    [Fact]
    public void CliCommandBuilder_WithoutProjectFlags_RegistryDefaultsRemainEffective()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server" });
        var parsed = CliCommandBuilder.Parse(result, options);

        Assert.Null(parsed.McpProjectTtlMinutes);
        Assert.Null(parsed.McpMaxProjects);

        var args = new LinterArgs
        {
            McpServer = parsed.McpServer,
            McpProjectTtlMinutes = parsed.McpProjectTtlMinutes,
            McpMaxProjects = parsed.McpMaxProjects,
            Verbose = false,
        };
        Assert.Null(args.Validate());
    }

    [Fact]
    public void CliCommandBuilder_Parses_MaxProjects()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server", "--mcp-max-projects", "7" });
        Assert.Empty(result.Errors);

        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal(7, parsed.McpMaxProjects);
    }

    [Theory]
    [InlineData("--mcp-project-ttl-minutes", "abc")]
    [InlineData("--mcp-project-ttl-minutes", "1,5")]
    [InlineData("--mcp-max-projects", "vier")]
    public void CliCommandBuilder_InvalidFlagValues_AreHardParseErrors(string flag, string value)
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server", flag, value });

        Assert.NotEmpty(result.Errors);
    }
}
