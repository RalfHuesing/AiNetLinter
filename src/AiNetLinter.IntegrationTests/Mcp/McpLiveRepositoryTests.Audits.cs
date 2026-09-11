#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

public sealed partial class McpLiveRepositoryTests
{
    [Fact]
    public async Task LiveDogfood_AuditDump_WritesReport()
    {
        var resultPrivateInternal = await _fixture.Client.CallToolGetTextAsync(
            "find_dead_code",
            new Dictionary<string, object?>
            {
                ["accessibility"] = "private_internal",
                ["confidence"] = "both",
                ["mode"] = "both",
                ["maxResults"] = 200
            });

        var resultDeadCodeMagic = await _fixture.Client.CallToolGetTextAsync(
            "find_magic_values",
            new Dictionary<string, object?>
            {
                ["scopeFilter"] = "src/AiNetLinter/Mcp/Tools/DeadCode",
                ["maxResults"] = 100
            });

        var resultDeadCodeDuplicates = await _fixture.Client.CallToolGetTextAsync(
            "find_duplicates",
            new Dictionary<string, object?>
            {
                ["scopeDir"] = "DeadCode",
                ["maxResults"] = 50
            });

        var outDir = Path.Combine(AppContext.BaseDirectory, "../../../../src/test-output");
        Directory.CreateDirectory(outDir);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== FIND_DEAD_CODE (private_internal, both) ===");
        sb.AppendLine(resultPrivateInternal);
        sb.AppendLine();
        sb.AppendLine("=== FIND_MAGIC_VALUES (DeadCode) ===");
        sb.AppendLine(resultDeadCodeMagic);
        sb.AppendLine();
        sb.AppendLine("=== FIND_DUPLICATES (DeadCode) ===");
        sb.AppendLine(resultDeadCodeDuplicates);
        File.WriteAllText(Path.Combine(outDir, "dead-code-audit.txt"), sb.ToString());

        Assert.Contains("# Dead-Code-Analyse", resultPrivateInternal, StringComparison.Ordinal);
        Assert.Contains("Magic-Value-Audit", resultDeadCodeMagic, StringComparison.Ordinal);
        Assert.Contains("Duplikat-Cluster", resultDeadCodeDuplicates, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveDogfood_FindMagicValues_ReturnsCandidateContractOverWire()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_magic_values",
            new Dictionary<string, object?>
            {
                ["scopeFilter"] = "src/AiNetLinter/Mcp/Tools/DeadCode",
                ["minOccurrences"] = 1,
                ["maxResults"] = 20,
            });

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("resultType=candidate", text, StringComparison.Ordinal);
        Assert.NotNull(result.StructuredContent);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        Assert.Equal("candidate", (string?)json["resultType"]);
        Assert.Equal(7, json["categories"]!.AsArray().Count);
        Assert.NotNull(json["summary"]!["status"]);
    }
}
