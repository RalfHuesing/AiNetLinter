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
}
