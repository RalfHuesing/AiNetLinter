#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using AiNetLinter.Commands;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// Tests für <see cref="ReadmeCommand"/>.
/// </summary>
public sealed class ReadmeCommandTests
{
    [Fact]
    public void Run_ReturnsZero()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var result = ReadmeCommand.Run();
            Assert.Equal(0, result);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Run_WritesReadmeContentToOutput()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            ReadmeCommand.Run();
            var output = writer.ToString();
            // README.md starts with a heading
            Assert.False(string.IsNullOrWhiteSpace(output));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
