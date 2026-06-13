using System.Text.Json;
using AiNetLinter.Models;
using AiNetLinter.Output;
using AiNetLinter.Configuration;

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

    [Fact]
    public void Write_IncludesIntentTagsInProperties()
    {
        var violations = new[]
        {
            new RuleViolation
            {
                FilePath = Path.Combine(OutputRoot, "src", "Foo.cs"),
                LineNumber = 7,
                RuleName = "MaxLineCount",
                Details = "Too long",
                Guidance = "Shorten it"
            }
        };

        var config = new LinterConfig
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig(),
            RuleMetadata = new Dictionary<string, RuleMetadataEntry>
            {
                ["MaxLineCount"] = new() { Severity = "error", Intent = "test-intent" }
            }
        };

        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            SarifWriter.Write(violations, OutputRoot, config);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        using var doc = JsonDocument.Parse(writer.ToString());
        var tags = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("results")[0]
            .GetProperty("properties")
            .GetProperty("tags");

        Assert.Equal(1, tags.GetArrayLength());
        Assert.Equal("test-intent", tags[0].GetString());
    }
}
