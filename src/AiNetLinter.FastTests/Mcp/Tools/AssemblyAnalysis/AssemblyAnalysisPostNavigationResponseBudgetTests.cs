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
        var original = McpToolResults.Text(InspectAssemblyFormatter.FormatText(payload, publicOnly: false), payload);

        var projected = AssemblyAnalysisPostNavigationResponseBudget.ApplyInspect(original, 2_048, publicOnly: false);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(projected.Content)).Text;
        Assert.NotEqual(true, projected.IsError);
        Assert.Contains("API-Typen:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Öffentliche API-Typen:", text, StringComparison.Ordinal);
        Assert.True(McpResponseSize.From(projected).TotalBytes <= 2_048);
    }

    [Fact]
    public void ApplyContext_DropsOnlyWholeOptionalSectionsAndRewritesNavigation()
    {
        var original = McpToolResults.Text("context", new
        {
            contextId = "asm:fixture",
            targetPath = "C:/fixture.dll",
            scope = "root",
            completeness = "complete",
            symbolIdentifier = "T:Fixture.Type",
            identity = new { name = "Fixture" },
            origin = new { canonicalPath = "C:/fixture.dll" },
            assemblyAnalysis = new { totalCount = 1, returnedCount = 1 },
            body = new { results = new[] { new { body = new string('x', 8_000) } } },
            totalCount = 1,
            returnedCount = 1,
            isTruncated = false,
            truncatedBy = Array.Empty<string>(),
            navigation = new
            {
                contractVersion = 2,
                target = new { targetPath = "C:/fixture.dll", analysisRoot = "C:/fixture.dll", origin = "assembly" },
                snapshot = new { fingerprint = "fixture", kind = "assembly", fresh = true },
                status = new { operation = "ok", completeness = "complete", code = (string?)null },
                analysis = new { mode = "metadata", quality = "complete", limitationCodes = Array.Empty<string>() },
                next = new { kind = "none", action = "none" },
            },
        });

        var projected = AssemblyAnalysisPostNavigationResponseBudget.ApplyContext(original, 2_048);

        Assert.NotEqual(true, projected.IsError);
        Assert.True(McpResponseSize.From(projected).TotalBytes <= 2_048);
        var payload = projected.StructuredContent!.Value;
        Assert.False(payload.TryGetProperty("body", out _));
        Assert.Equal("truncated", payload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
        Assert.Contains("responseBudget", payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
    }
}
