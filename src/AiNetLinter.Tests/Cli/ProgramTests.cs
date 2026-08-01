using Xunit;
using System.CommandLine;
using AiNetLinter;
using AiNetLinter.Cli;

namespace AiNetLinter.Tests.Cli;

[Collection("ConsoleTestCollection")]
[Trait("Category", "Integration")]
public sealed class ProgramTests
{
    [Fact]
    public async Task Main_WithEmptyArgs_ReturnsExitCodeOne()
    {
        var result = await Program.Main(Array.Empty<string>());
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Main_WithValidArgs_PrintsRunHeaderInTextMode()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var exitCode = await Program.Main(new[]
            {
                "--config", "non-existent-config.json",
                "--path", "."
            });

            var output = writer.ToString();
            Assert.Contains("# Run: ", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

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
}
