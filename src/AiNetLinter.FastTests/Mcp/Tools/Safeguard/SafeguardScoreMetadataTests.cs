#nullable enable

using System;
using AiNetLinter.FastTests;
using AiNetLinter.Mcp.Tools.Safeguard;
using AiNetLinter.Models;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Safeguard;

[Trait("Category", "Component")]
public sealed class SafeguardScoreMetadataTests
{
    [Fact]
    public void BuildScoreResult_ReportsTotalAndShownViolationCounts()
    {
        var violations = new[]
        {
            new RuleViolation
            {
                FilePath = @"C:\Solution\A.cs",
                LineNumber = 1,
                RuleName = "FirstRule",
                Details = "first",
                Guidance = "fix first",
                EffectiveSeverity = "error",
            },
            new RuleViolation
            {
                FilePath = @"C:\Solution\B.cs",
                LineNumber = 2,
                RuleName = "SecondRule",
                Details = "second",
                Guidance = "fix second",
                EffectiveSeverity = "warning",
            },
        };

        var result = SafeguardScanner.BuildScoreResult(new BuildScoreResultParameters(
            Violations: violations,
            Classes: Array.Empty<ScannedClass>(),
            Config: TestHelper.CreateDefaultConfig(),
            Threshold: 8.0,
            MaxRemediationEntries: 1,
            SolutionDir: @"C:\Solution"));

        Assert.Equal(2, result.TotalViolationCount);
        Assert.Equal(1, result.ShownViolationCount);
        Assert.True(result.ViolationsTruncated);
        Assert.Contains("1 von 2 Verstößen", result.Summary, StringComparison.Ordinal);
        Assert.Contains("maxViolations", result.Summary, StringComparison.Ordinal);
    }
}
