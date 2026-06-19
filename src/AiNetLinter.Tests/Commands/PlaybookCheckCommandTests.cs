#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using AiNetLinter.Cli;
using AiNetLinter.Commands;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// Tests für <see cref="PlaybookCheckCommand"/>.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class PlaybookCheckCommandTests
{
    [Fact]
    public async Task RunAsync_WhenPlaybookFileNotExists_ReturnsOne()
    {
        // Für diesen Test brauchen wir eine echte Solution-Datei
        var slnxPath = FindSlnxFile();
        if (slnxPath == null)
        {
            // Kein .slnx im Testumfeld gefunden — Test überspringen
            return;
        }

        var args = new LinterArgs
        {
            TargetPath = slnxPath,
            ConfigPath = null,
            Verbose = false,
            Check = true,
            PlaybookPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.md"),
        };

        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);
        try
        {
            var result = await PlaybookCheckCommand.RunAsync(args);
            Assert.Equal(1, result);
            Assert.Contains("[ERROR]", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    private static string? FindSlnxFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var files = dir.GetFiles("*.slnx");
            if (files.Length > 0) return files[0].FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
