#nullable enable

using System.Collections.Generic;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

namespace AiNetLinter.Tests.Configuration;

public sealed class RuleMetadataRegistryTests
{
    [Fact]
    public void HasErrorSeverity_ViolationWithNullSeverity_UsesConfigDefault()
    {
        var violations = new[] { new RuleViolation
        {
            FilePath = "X.cs", LineNumber = 1,
            RuleName = "MaxMethodLineCount", Details = "", Guidance = "",
            EffectiveSeverity = null // use config default
        }};
        var config = TestHelper.CreateDefaultConfig();
        // MaxMethodLineCount defaults to "error" in RuleRegistry
        Assert.True(RuleMetadataRegistry.HasErrorSeverity(violations, config));
    }

    [Fact]
    public void HasErrorSeverity_ViolationWithWarningSeverity_ReturnsFalse()
    {
        var violations = new[] { new RuleViolation
        {
            FilePath = "X.cs", LineNumber = 1,
            RuleName = "MaxMethodLineCount", Details = "", Guidance = "",
            EffectiveSeverity = "warning" // compound-override
        }};
        var config = TestHelper.CreateDefaultConfig();
        Assert.False(RuleMetadataRegistry.HasErrorSeverity(violations, config));
    }

    [Fact]
    public void HasErrorSeverity_MixedViolations_TrueWhenAnyError()
    {
        var violations = new[]
        {
            new RuleViolation { FilePath = "X.cs", LineNumber = 1,
                RuleName = "MaxMethodLineCount", Details = "", Guidance = "",
                EffectiveSeverity = "warning" }, // downgraded
            new RuleViolation { FilePath = "X.cs", LineNumber = 2,
                RuleName = "MaxCyclomaticComplexity", Details = "", Guidance = "",
                EffectiveSeverity = null }  // normal error
        };
        var config = TestHelper.CreateDefaultConfig();
        Assert.True(RuleMetadataRegistry.HasErrorSeverity(violations, config));
    }
}
