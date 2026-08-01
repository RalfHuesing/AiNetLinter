#nullable enable

using AiNetLinter.Commands;
using AiNetLinter.Tests.Output;
using Xunit;

namespace AiNetLinter.Tests.Evals;

public sealed class ListEvalsCommandTests
{
    [Fact]
    public void Run_OutputContainsAllEvalNames()
    {
        var console = new TestLintConsole();
        ListEvalsCommand.Run(console);
        Assert.Contains("naming-drift", console.OutputText);
        Assert.Contains("architecture-intent", console.OutputText);
    }

    [Fact]
    public void Run_ReturnsExitCodeZero()
    {
        var console = new TestLintConsole();
        Assert.Equal(0, ListEvalsCommand.Run(console));
    }
}
