#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies;

public sealed partial class AssemblyAnalysisDispatcherCapabilityTests
{
    [Fact]
    public async Task AssemblyRoute_FilteredEmptyResultReportsActualWireTruncation()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-b002-wire-");
        var references = Enumerable.Range(0, AssemblyAnalysisResponseLimits.MaxReferenceSessions + 8)
            .Select(index => new AssemblyReferenceDto(
                $"B002WireMissingDependency{index:D2}",
                "1.0.0.0",
                "neutral",
                Resolved: true,
                ResolvedPath: Path.Combine(temp.DirectoryPath, $"B002WireMissingDependency{index:D2}.dll")))
            .ToArray();
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            references,
            FailingReferenceFactory);

        var result = await fixture.ExecuteExtensionsAsync(
            maxResponseBytes: 4096,
            extensionName: "NoSuchExtension");

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        if (payload.TryGetProperty("extensions", out var extensions))
        {
            Assert.Empty(extensions.EnumerateArray());
        }
        Assert.True(payload.GetProperty("wireTruncated").GetBoolean(), payload.GetRawText());
        Assert.True(payload.GetProperty("wireBudget").GetProperty("truncated").GetBoolean(), payload.GetRawText());
        Assert.Contains(
            "responseBudget",
            payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("truncated", payload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
    }

    [Theory]
    [InlineData("inspect_assembly")]
    [InlineData("find_assembly_extensions")]
    public async Task AssemblyRoute_FilteredEmptyResultDoesNotInheritReferenceTruncationInNavigation(string operation)
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-b002-");
        var references = Enumerable.Range(0, 1)
            .Select(index => new AssemblyReferenceDto(
                $"B002MissingDependency{index:D2}",
                "1.0.0.0",
                "neutral",
                Resolved: true,
                ResolvedPath: Path.Combine(temp.DirectoryPath, $"B002MissingDependency{index:D2}.dll")))
            .ToArray();
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            references,
            FailingReferenceFactory);

        var result = operation == "inspect_assembly"
            ? await fixture.ExecuteInspectAsync(
                maxResponseBytes: 16384,
                typeName: "NoSuchType")
            : await fixture.ExecuteExtensionsAsync(
                maxResponseBytes: 16384,
                extensionName: "NoSuchExtension");

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        var collectionName = operation == "inspect_assembly" ? "types" : "extensions";
        var totalName = operation == "inspect_assembly" ? "totalTypes" : "totalExtensions";
        Assert.True(payload.TryGetProperty(collectionName, out var items), payload.GetRawText());
        Assert.Empty(items.EnumerateArray());
        Assert.Equal(0, payload.GetProperty(totalName).GetInt32());
        var navigation = payload.GetProperty("navigation");
        Assert.Equal("complete", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal(JsonValueKind.Null, navigation.GetProperty("next").ValueKind);
    }
}
