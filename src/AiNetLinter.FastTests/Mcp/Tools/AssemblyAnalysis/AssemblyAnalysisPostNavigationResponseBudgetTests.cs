#nullable enable

using System;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Wire;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Unit")]
public sealed class AssemblyAnalysisPostNavigationResponseBudgetTests
{
    [Fact]
    public void ApplyInspect_PreservesRequestedPublicOnlyValueWhenProjectingAfterNavigation()
    {
        var types = Enumerable.Range(0, 32)
            .Select(index => new AssemblyTypeDto(
                "Fixture",
                $"InternalType{index:D2}",
                "class",
                "internal",
                Array.Empty<AssemblyMemberDto>(),
                Array.Empty<string>()))
            .ToArray();
        var payload = new InspectAssemblyPayload(
            "C:/fixture.dll",
            new AssemblyIdentityDto("Fixture", "1.0.0.0", "neutral", "null"),
            ["Fixture"],
            Array.Empty<AssemblyReferenceDto>(),
            types,
            Array.Empty<string>(),
            "complete",
            Truncated: false,
            TotalTypes: types.Length,
            ShownCount: types.Length,
            TruncatedBy: Array.Empty<string>(),
            TotalCount: types.Length,
            ReturnedCount: types.Length);
        var original = McpToolResults.Text(InspectAssemblyFormatter.FormatText(payload, publicOnly: false));

        var projected = AssemblyAnalysisPostNavigationResponseBudget.ApplyInspect(original, 2_048, publicOnly: false);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(projected.Content)).Text;
        Assert.NotEqual(true, projected.IsError);
        Assert.Contains("API-Typen:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Öffentliche API-Typen:", text, StringComparison.Ordinal);
        Assert.True(McpResponseSize.From(projected).TotalBytes <= 2_048);
    }

    [Fact]
    public void ApplyContext_UsesOnlyTheRenderedContentForItsBudget()
    {
        var original = McpToolResults.Text("context");

        var projected = AssemblyAnalysisPostNavigationResponseBudget.ApplyContext(original, 2_048);

        Assert.NotEqual(true, projected.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(projected.Content)).Text;
        Assert.Equal("context", text);
    }
}
