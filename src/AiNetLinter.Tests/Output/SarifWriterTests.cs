using System.Text.Json;
using AiNetLinter.Models;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

[Collection("ConsoleTestCollection")]
public sealed class SarifWriterTests
{
    private static readonly string OutputRoot = Path.GetFullPath(@"C:\Projects\MyApp");

    [Fact]
    public void Write_UsesRelativeUriInArtifactLocation()
    {
        var violations = new[]
        {
            new RuleViolation
            {
                FilePath = Path.Combine(OutputRoot, "src", "Foo.cs"),
                LineNumber = 7,
                RuleName = "EnforceSealedClasses",
                Details = "Klasse nicht sealed",
                Guidance = "sealed hinzufuegen"
            }
        };

        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            SarifWriter.Write(violations, OutputRoot);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        using var doc = JsonDocument.Parse(writer.ToString());
        var uri = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("results")[0]
            .GetProperty("locations")[0]
            .GetProperty("physicalLocation")
            .GetProperty("artifactLocation")
            .GetProperty("uri")
            .GetString();

        Assert.Equal("src/Foo.cs", uri);
    }
}
