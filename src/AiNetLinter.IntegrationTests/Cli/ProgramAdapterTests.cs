#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using Xunit;

namespace AiNetLinter.IntegrationTests.Cli;

/// <summary>
/// <c>Main_WithValidArgs_PrintsRunHeaderInTextMode</c> leitet <c>Console.Out</c> via
/// <c>Console.SetOut</c> auf einen <c>StringWriter</c> um, um die Textmodus-Kopfzeile
/// ("# Run: ") zu pruefen — parallel laufende Tests wuerden sich die globale
/// Konsolenumleitung gegenseitig ueberschreiben.
/// </summary>
[Collection("ConsoleTestCollection")]
[Trait("Category", "Integration")]
public sealed class ProgramAdapterTests
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
