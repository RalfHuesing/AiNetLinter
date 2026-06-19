using Xunit;
using AiNetLinter;

namespace AiNetLinter.Tests;

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
}
