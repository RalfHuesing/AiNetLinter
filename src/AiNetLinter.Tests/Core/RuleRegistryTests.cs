#nullable enable

using System;
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using Xunit;

namespace AiNetLinter.Tests.Core;

public sealed class RuleRegistryTests
{
    [Fact]
    public void AllRules_HaveValidProperties()
    {
        Assert.NotEmpty(RuleRegistry.All);

        foreach (var rule in RuleRegistry.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.RuleId), "RuleId should not be empty.");
            Assert.False(string.IsNullOrWhiteSpace(rule.DisplayName), $"DisplayName should not be empty for {rule.RuleId}.");
            Assert.False(string.IsNullOrWhiteSpace(rule.Intent), $"Intent should not be empty for {rule.RuleId}.");
            Assert.False(string.IsNullOrWhiteSpace(rule.Severity), $"Severity should not be empty for {rule.RuleId}.");

            var fakeConfig = new LinterConfig
            {
                Global = new GlobalConfig(),
                Metrics = new MetricsConfig()
            };

            var shortDesc = rule.GetShortDescription(fakeConfig);
            Assert.False(string.IsNullOrWhiteSpace(shortDesc), $"ShortDescription should not be empty for {rule.RuleId}.");

            if (rule.IsMetric)
            {
                Assert.NotNull(rule.GetMetricLimit);
                var limit = rule.GetMetricLimit!(fakeConfig);
                Assert.True(limit >= 0, $"Metric limit should be >= 0 for {rule.RuleId}.");
            }
        }
    }

    [Fact]
    public void Resolve_ReturnsCorrectRule_OrThrows()
    {
        var rule = RuleRegistry.Resolve("EnforceSealedClasses");
        Assert.NotNull(rule);
        Assert.Equal("EnforceSealedClasses", rule.RuleId);

        Assert.Throws<KeyNotFoundException>(() => RuleRegistry.Resolve("NonExistentRule"));
    }

    [Fact]
    public void TryResolve_ReturnsNull_ForUnknownRule()
    {
        var rule = RuleRegistry.TryResolve("NonExistentRule");
        Assert.Null(rule);
    }

    [Fact]
    public void ByIntent_FiltersCorrectly()
    {
        var generalRules = RuleRegistry.ByIntent("general").ToList();
        Assert.NotEmpty(generalRules);
        Assert.All(generalRules, r => Assert.Equal("general", r.Intent, ignoreCase: true));
    }
}
