#nullable enable

using System.Collections.Generic;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

public sealed class CompoundSuppressionEvaluatorTests
{
    private const int NoSuppressionSentinel = -1;

    [Fact]
    public void NoSuppressions_ReturnsNoSuppression()
    {
        var metrics = new Dictionary<string, int> { { "CyclomaticComplexity", 2 } };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", null, metrics);
        Assert.Equal(NoSuppressionSentinel, result);

        result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", new List<CompoundSuppression>(), metrics);
        Assert.Equal(NoSuppressionSentinel, result);
    }

    [Fact]
    public void WrongTargetRule_ReturnsNoSuppression()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                },
                RelaxedLimit = 150
            }
        };
        var metrics = new Dictionary<string, int> { { "CyclomaticComplexity", 2 } };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodParameterCount", suppressions, metrics);
        Assert.Equal(NoSuppressionSentinel, result);
    }

    [Fact]
    public void SingleAtMost_Met_ReturnsRelaxedLimit()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                },
                RelaxedLimit = 150
            }
        };
        var metrics = new Dictionary<string, int> { { "CyclomaticComplexity", 2 } };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", suppressions, metrics);
        Assert.Equal(150, result);
    }

    [Fact]
    public void SingleAtMost_NotMet_ReturnsNoSuppression()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                },
                RelaxedLimit = 150
            }
        };
        var metrics = new Dictionary<string, int> { { "CyclomaticComplexity", 5 } };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", suppressions, metrics);
        Assert.Equal(NoSuppressionSentinel, result);
    }

    [Fact]
    public void MultipleConditions_AllMet_ReturnsRelaxedLimit()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "CyclomaticComplexity", AtMost = 3 },
                    new() { Metric = "CognitiveComplexity", AtMost = 5 }
                },
                RelaxedLimit = 150
            }
        };
        var metrics = new Dictionary<string, int>
        {
            { "CyclomaticComplexity", 2 },
            { "CognitiveComplexity", 4 }
        };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", suppressions, metrics);
        Assert.Equal(150, result);
    }

    [Fact]
    public void MultipleConditions_OneFails_ReturnsNoSuppression()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "CyclomaticComplexity", AtMost = 3 },
                    new() { Metric = "CognitiveComplexity", AtMost = 5 }
                },
                RelaxedLimit = 150
            }
        };
        var metrics = new Dictionary<string, int>
        {
            { "CyclomaticComplexity", 2 },
            { "CognitiveComplexity", 8 }
        };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", suppressions, metrics);
        Assert.Equal(NoSuppressionSentinel, result);
    }

    [Fact]
    public void NullRelaxedLimit_ReturnsZero()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                },
                RelaxedLimit = null
            }
        };
        var metrics = new Dictionary<string, int> { { "CyclomaticComplexity", 2 } };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", suppressions, metrics);
        Assert.Equal(0, result);
    }

    [Fact]
    public void UnknownMetricName_ReturnsFalse()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "NonExistent", AtMost = 3 }
                },
                RelaxedLimit = 150
            }
        };
        var metrics = new Dictionary<string, int> { { "CyclomaticComplexity", 2 } };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", suppressions, metrics);
        Assert.Equal(NoSuppressionSentinel, result);
    }

    [Fact]
    public void AtLeast_Met()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "CyclomaticComplexity", AtLeast = 3 }
                },
                RelaxedLimit = 150
            }
        };
        var metrics = new Dictionary<string, int> { { "CyclomaticComplexity", 5 } };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", suppressions, metrics);
        Assert.Equal(150, result);
    }

    [Fact]
    public void AtLeast_NotMet()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "CyclomaticComplexity", AtLeast = 3 }
                },
                RelaxedLimit = 150
            }
        };
        var metrics = new Dictionary<string, int> { { "CyclomaticComplexity", 1 } };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", suppressions, metrics);
        Assert.Equal(NoSuppressionSentinel, result);
    }

    [Fact]
    public void FirstMatchingSuppressionWins()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                },
                RelaxedLimit = 150
            },
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                {
                    new() { Metric = "CyclomaticComplexity", AtMost = 5 }
                },
                RelaxedLimit = 200
            }
        };
        var metrics = new Dictionary<string, int> { { "CyclomaticComplexity", 2 } };
        var result = CompoundSuppressionEvaluator.Evaluate("MaxMethodLineCount", suppressions, metrics);
        Assert.Equal(150, result);
    }

    [Fact]
    public void GetActiveSeverityOverride_NoSuppression_ReturnsNull()
    {
        var metrics = new Dictionary<string, int> { ["CyclomaticComplexity"] = 2 };
        var result = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
            "MaxMethodLineCount", null, metrics);
        Assert.Null(result);
    }

    [Fact]
    public void GetActiveSeverityOverride_ConditionsNotMet_ReturnsNull()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                    { new() { Metric = "CyclomaticComplexity", AtMost = 3 } },
                RelaxedLimit = 150,
                SeverityOverride = "warning"
            }
        };
        var metrics = new Dictionary<string, int> { ["CyclomaticComplexity"] = 5 }; // > 3
        var result = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
            "MaxMethodLineCount", suppressions, metrics);
        Assert.Null(result);
    }

    [Fact]
    public void GetActiveSeverityOverride_ConditionsMet_NoOverrideConfigured_ReturnsNull()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                    { new() { Metric = "CyclomaticComplexity", AtMost = 3 } },
                RelaxedLimit = 150
                // SeverityOverride nicht gesetzt
            }
        };
        var metrics = new Dictionary<string, int> { ["CyclomaticComplexity"] = 2 };
        var result = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
            "MaxMethodLineCount", suppressions, metrics);
        Assert.Null(result);
    }

    [Fact]
    public void GetActiveSeverityOverride_ConditionsMet_WithOverride_ReturnsOverride()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                    { new() { Metric = "CyclomaticComplexity", AtMost = 3 } },
                RelaxedLimit = 150,
                SeverityOverride = "warning"
            }
        };
        var metrics = new Dictionary<string, int> { ["CyclomaticComplexity"] = 2 };
        var result = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
            "MaxMethodLineCount", suppressions, metrics);
        Assert.Equal("warning", result);
    }

    [Fact]
    public void GetActiveSeverityOverride_WrongRule_ReturnsNull()
    {
        var suppressions = new List<CompoundSuppression>
        {
            new()
            {
                TargetRule = "MaxMethodLineCount",
                WhenAllOf = new List<MetricCondition>
                    { new() { Metric = "CyclomaticComplexity", AtMost = 3 } },
                RelaxedLimit = 150,
                SeverityOverride = "warning"
            }
        };
        var metrics = new Dictionary<string, int> { ["CyclomaticComplexity"] = 2 };
        var result = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
            "MaxMethodParameterCount", suppressions, metrics); // andere Regel
        Assert.Null(result);
    }
}
