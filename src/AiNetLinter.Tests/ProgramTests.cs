using Xunit;
using System.CommandLine;
using AiNetLinter;
using AiNetLinter.Cli;

namespace AiNetLinter.Tests;

[Collection("ConsoleTestCollection")]
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
    public void CliCommandBuilder_Parses_CursorRulesPath()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--config", "rules.json", "--path", ".", "--cursor-rules-path", "my-rules-dir" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal("my-rules-dir", parsed.CursorRulesPath);
    }

    [Fact]
    public void CliCommandBuilder_Parses_CursorRulesPath_WithAlias()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--config", "rules.json", "--path", ".", "-crp", "my-rules-dir" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal("my-rules-dir", parsed.CursorRulesPath);
    }
}
