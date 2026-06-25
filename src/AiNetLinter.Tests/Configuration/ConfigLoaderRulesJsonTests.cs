#nullable enable

using System.IO;
using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.Tests.Configuration;

// @covers LinterConfigLoader
public sealed class LinterConfigLoaderRulesJsonTests
{
    [Fact]
    public void LoadRulesJsonContent_GibtNullZurueck_WennPfadNull()
    {
        var result = LinterConfigLoader.LoadRulesJsonContent(null);
        Assert.Null(result);
    }

    [Fact]
    public void LoadRulesJsonContent_GibtNullZurueck_WennPfadLeer()
    {
        var result = LinterConfigLoader.LoadRulesJsonContent("");
        Assert.Null(result);
    }

    [Fact]
    public void LoadRulesJsonContent_GibtNullZurueck_WennDateiNichtExistiert()
    {
        var result = LinterConfigLoader.LoadRulesJsonContent("nicht_vorhanden.json");
        Assert.Null(result);
    }

    [Fact]
    public void LoadRulesJsonContent_GibtInhaltZurueck_WennDateiExistiert()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{\"test\": true}");
            var result = LinterConfigLoader.LoadRulesJsonContent(tempFile);
            Assert.Equal("{\"test\": true}", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
