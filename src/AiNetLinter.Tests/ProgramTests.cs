using Xunit;
using AiNetLinter;

namespace AiNetLinter.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void Main_WithEmptyArgs_ReturnsExitCodeOne()
    {
        var result = Program.Main(Array.Empty<string>());
        Assert.Equal(1, result);
    }
}
