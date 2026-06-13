using System.Text.Json;
using System.Text.Json.Serialization;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

namespace AiNetLinter.Output;

/// <summary>
/// Schreibt Regelverstöße im SARIF 2.1.0-Format auf stdout mit relativen Dateipfaden.
/// </summary>
public static class SarifWriter
{
    /// <summary>
    /// Serialisiert die Verstöße als SARIF-JSON und gibt sie auf stdout aus.
    /// </summary>
    public static void Write(
        IReadOnlyCollection<RuleViolation> violations,
        string outputRoot,
        LinterConfig? config = null)
    {
        var doc = new SarifDocument();
        var run = new SarifRun();
        doc.Runs.Add(run);

        foreach (var violation in violations)
        {
            var metadata = config == null
                ? new RuleMetadataEntry()
                : RuleMetadataRegistry.Resolve(violation.RuleName ?? "UnknownRule", config);
            var result = new SarifResult
            {
                RuleId = violation.RuleName ?? "UnknownRule",
                Level = RuleMetadataRegistry.ToSarifLevel(metadata.Severity),
            };
            result.Message.Text = $"{violation.Details} Guidance: {violation.Guidance}";

            if (!string.IsNullOrEmpty(metadata.Intent))
            {
                result.Properties.Tags.Add(metadata.Intent);
            }

            var loc = new SarifLocation();
            loc.PhysicalLocation.ArtifactLocation.Uri =
                PathNormalizer.ToRelative(outputRoot, violation.FilePath);
            loc.PhysicalLocation.Region.StartLine = violation.LineNumber;
            result.Locations.Add(loc);

            run.Results.Add(result);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(doc, options);
        Console.WriteLine(json);
    }

    private sealed class SarifDocument
    {
        [JsonPropertyName("$schema")]
        public string Schema => "https://schemastore.azurewebsites.net/schemas/json/sarif-2.1.0-rtm.5.json";
        public string Version => "2.1.0";
        public List<SarifRun> Runs { get; } = new();
    }

    private sealed class SarifRun
    {
        public SarifTool Tool { get; } = new();
        public List<SarifResult> Results { get; } = new();
    }

    private sealed class SarifTool
    {
        public SarifDriver Driver { get; } = new();
    }

    private sealed class SarifDriver
    {
        public string Name => "AiNetLinter";
        public string Version => "1.0.0";
    }

    private sealed class SarifResult
    {
        public string RuleId { get; set; } = "";
        public string Level { get; set; } = "error";
        public SarifMessage Message { get; } = new();
        public List<SarifLocation> Locations { get; } = new();
        public SarifProperties Properties { get; } = new();
    }

    private sealed class SarifProperties
    {
        public List<string> Tags { get; } = new();
    }

    private sealed class SarifMessage
    {
        public string Text { get; set; } = "";
    }

    private sealed class SarifLocation
    {
        public SarifPhysicalLocation PhysicalLocation { get; } = new();
    }

    private sealed class SarifPhysicalLocation
    {
        public SarifArtifactLocation ArtifactLocation { get; } = new();
        public SarifRegion Region { get; } = new();
    }

    private sealed class SarifArtifactLocation
    {
        public string Uri { get; set; } = "";
    }

    private sealed class SarifRegion
    {
        public int StartLine { get; set; }
    }
}
