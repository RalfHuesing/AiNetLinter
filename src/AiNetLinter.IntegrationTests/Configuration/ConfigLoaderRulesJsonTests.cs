#nullable enable

using System.IO;
using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.IntegrationTests.Configuration;

// @covers ConfigLoader
[Trait("Category", "Integration")]
public sealed class ConfigLoaderRulesJsonTests
{
    [Fact]
    public void LoadRulesJsonContent_GibtNullZurueck_WennPfadNull()
    {
        var result = ConfigLoader.LoadRulesJsonContent(null);
        Assert.Null(result);
    }

    [Fact]
    public void LoadRulesJsonContent_GibtNullZurueck_WennPfadLeer()
    {
        var result = ConfigLoader.LoadRulesJsonContent("");
        Assert.Null(result);
    }

    [Fact]
    public void LoadRulesJsonContent_GibtNullZurueck_WennDateiNichtExistiert()
    {
        var result = ConfigLoader.LoadRulesJsonContent("nicht_vorhanden.json");
        Assert.Null(result);
    }

    [Fact]
    public void LoadRulesJsonContent_GibtInhaltZurueck_WennDateiExistiert()
    {
        using var tempDir = TestTempDirectory.Create("cfg-rules-");
        var tempFile = tempDir.CreateFile("rules.json", "{\"test\": true}");

        var result = ConfigLoader.LoadRulesJsonContent(tempFile);
        Assert.Equal("{\"test\": true}", result);
    }
}
