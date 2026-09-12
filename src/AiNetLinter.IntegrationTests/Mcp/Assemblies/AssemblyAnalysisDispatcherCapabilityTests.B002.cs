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
    public async Task AssemblyRoute_FilteredEmptyResultUsesPostNavigationBudgetWithoutGenericWireFields()
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

        var result = await fixture.ExecuteExtensionsAsync(new ExtensionExecutionOptions(
            MaxResponseBytes: 4096,
            ExtensionName: "NoSuchExtension",
            ApplyPostNavigationResponseBudget: true));

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        if (payload.TryGetProperty("extensions", out var extensions))
        {
            Assert.Empty(extensions.EnumerateArray());
        }
        AssertFinalCombinedBudget(result, 4096);
        AssertPartialStatusConsistency(result);
        Assert.Contains(Diagnostics(payload), diagnostic => diagnostic.Contains("B002WireMissingDependency", StringComparison.Ordinal));
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
            ? await fixture.ExecuteInspectAsync(new InspectionExecutionOptions(
                MaxResponseBytes: 16384,
                TypeName: "NoSuchType"))
            : await fixture.ExecuteExtensionsAsync(new ExtensionExecutionOptions(
                MaxResponseBytes: 16384,
                ExtensionName: "NoSuchExtension"));

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        var collectionName = operation == "inspect_assembly" ? "types" : "extensions";
        var totalName = operation == "inspect_assembly" ? "totalTypes" : "totalExtensions";
        Assert.True(payload.TryGetProperty(collectionName, out var items), payload.GetRawText());
        Assert.Empty(items.EnumerateArray());
        Assert.Equal(0, payload.GetProperty(totalName).GetInt32());
        var navigation = payload.GetProperty("navigation");
        AssertPartialStatusConsistency(result);
        Assert.Equal("partial", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal(JsonValueKind.Object, navigation.GetProperty("next").ValueKind);
        Assert.Contains(Diagnostics(payload), diagnostic => diagnostic.Contains("B002MissingDependency00", StringComparison.Ordinal));
        if (operation != "find_assembly_extensions") return;
        var session = Assert.Single(payload.GetProperty("referenceSessions").EnumerateArray());
        Assert.Equal("partial", session.GetProperty("sessionStatus").GetString());
    }
}
