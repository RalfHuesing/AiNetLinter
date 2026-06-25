#nullable enable

using System.IO;
using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.Tests.Configuration;

// @covers ConfigLoader
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
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{\"test\": true}");
            var result = ConfigLoader.LoadRulesJsonContent(tempFile);
            Assert.Equal("{\"test\": true}", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
