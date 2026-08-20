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
}
