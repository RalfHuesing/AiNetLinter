#nullable enable

using System.IO;
using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.IntegrationTests.Configuration;

// @covers ConfigLoader
[Trait("Category", "Integration")]
public sealed class ConfigLoaderContentTests
{
    [Fact]
    public void LoadConfigContent_GibtNullZurueck_WennPfadNull()
    {
        var result = ConfigLoader.LoadConfigContent(null);
        Assert.Null(result);
    }

    [Fact]
    public void LoadConfigContent_GibtNullZurueck_WennPfadLeer()
    {
        var result = ConfigLoader.LoadConfigContent("");
        Assert.Null(result);
    }

    [Fact]
    public void LoadConfigContent_GibtNullZurueck_WennDateiNichtExistiert()
    {
        var result = ConfigLoader.LoadConfigContent("nicht_vorhanden.json");
        Assert.Null(result);
    }

    [Fact]
    public void LoadConfigContent_GibtInhaltZurueck_WennDateiExistiert()
    {
        using var tempDir = TestTempDirectory.Create("cfg-rules-");
        var tempFile = tempDir.CreateFile("ainetlinter-rules.json", "{\"test\": true}");

        var result = ConfigLoader.LoadConfigContent(tempFile);
        Assert.Equal("{\"test\": true}", result);
    }
}
