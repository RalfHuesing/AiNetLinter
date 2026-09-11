#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

public sealed partial class McpLiveRepositoryTests
{
    [Fact]
    public async Task LiveDogfood_FindDuplicates_StructuralMode_ReturnsValidSchema()
    {
        var result = await _fixture.Client.CallToolAsync("find_duplicates", new Dictionary<string, object?>
        {
            ["mode"] = "structural", ["scopeDir"] = "src/AiNetLinter/Mcp/Tools/DeadCode",
            ["minTokens"] = 10, ["maxResults"] = 10,
        });

        Assert.NotEqual(true, result.IsError);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        Assert.True(json.ContainsKey("clusters"), "StructuredContent muss 'clusters' enthalten");
        Assert.True(json.ContainsKey("summary"), "StructuredContent muss 'summary' enthalten");
        Assert.IsType<JsonArray>(json["clusters"]);
        var summary = json["summary"]!.AsObject();
        Assert.True(summary.ContainsKey("mode"), "summary muss 'mode' enthalten");
        Assert.Equal("structural", (string?)summary["mode"]);
        Assert.True(summary.ContainsKey("methodsScanned"), "summary muss 'methodsScanned' enthalten");
        Assert.True((int?)summary["methodsScanned"] >= 0);
    }
}
