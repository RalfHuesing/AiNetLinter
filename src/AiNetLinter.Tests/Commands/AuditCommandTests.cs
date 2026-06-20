#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using AiNetLinter.Cli;
using AiNetLinter.Commands;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// Tests für <see cref="AuditCommand"/>.
/// </summary>
public sealed class AuditCommandTests
{
    [Fact]
    public async Task RunAsync_WithInvalidConfig_ReturnsOne()
    {
        // Config-Datei existiert nicht → LinterConfigLoader gibt null zurück → Exit 1
        var args = new LinterArgs
        {
            TargetPath = ".",
            ConfigPath = "non-existent-config-12345.json",
            Verbose = false,
        };

        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var outWriter = new StringWriter();
        using var errorWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errorWriter);
        try
        {
            var result = await AuditCommand.RunAsync(args);
            Assert.Equal(1, result);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

}
