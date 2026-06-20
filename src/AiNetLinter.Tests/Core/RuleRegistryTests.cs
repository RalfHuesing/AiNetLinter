#nullable enable

using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Commands;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Tests.Output;
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
    public void AllRules_HaveNonEmptyWarumAndCursorHint()
    {
        foreach (var rule in RuleRegistry.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Warum),
                $"Warum darf nicht leer sein fuer {rule.RuleId}.");
            Assert.False(string.IsNullOrWhiteSpace(rule.CursorHint),
                $"CursorHint darf nicht leer sein fuer {rule.RuleId}.");
            Assert.NotEmpty(rule.Alternativen);
        }
    }

    [Fact]
    public void AllRules_HaveUniqueRuleIds()
    {
        var ids = RuleRegistry.All.Select(r => r.RuleId).ToList();
        var distinct = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(distinct.Count, ids.Count);
    }

    [Fact]
    public void AllRules_SeverityIsKnownValue()
    {
        var valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "error", "warning", "info" };
        foreach (var rule in RuleRegistry.All)
            Assert.True(valid.Contains(rule.Severity), $"{rule.RuleId} hat unbekannte Severity '{rule.Severity}'.");
    }

    [Fact]
    public void ListRulesCommand_OutputContainsAllRuleIds()
    {
        var console = new TestLintConsole();
        var result = ListRulesCommand.ListAll(console);

        Assert.Equal(0, result);
        var output = string.Join("\n", console.Output);
        foreach (var rule in RuleRegistry.All)
            Assert.Contains(rule.RuleId, output);
    }

    [Fact]
    public void ListRulesCommand_DescribeOne_ContainsWarumAndAlternativen()
    {
        var console = new TestLintConsole();
        var result = ListRulesCommand.DescribeOne("EnforceSealedClasses", console);

        Assert.Equal(0, result);
        var output = string.Join("\n", console.Output);
        Assert.Contains("Warum", output);
        Assert.Contains("Alternativen", output);
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

    [Fact]
    public void BuildAll_ContainsAllExpectedRuleIds()
    {
        var ids = RuleRegistry.All.Select(r => r.RuleId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("MaxLineCount", ids);
        Assert.Contains("MaxMethodLineCount", ids);
        Assert.Contains("MaxMethodParameterCount", ids);
        Assert.Contains("MaxCyclomaticComplexity", ids);
        Assert.Contains("MaxCognitiveComplexity", ids);
        Assert.Contains("MaxInheritanceDepth", ids);
        Assert.Contains("MaxMethodOverloads", ids);
        Assert.Contains("MaxConstructorDependencies", ids);
        Assert.Contains("AIContextFootprint", ids);
        Assert.Contains("MaxDirectoryDepth", ids);
        Assert.Contains("MaxDirectoryChildren", ids);
        Assert.Contains("MaxBoolParameterCount", ids);
        Assert.Contains("MaxPartialClassFiles", ids);
        Assert.Contains("MaxPublicMembersPerType", ids);
        Assert.Contains("EnforceNoSilentCatch", ids);
        Assert.Contains("BanAsyncVoid", ids);
        Assert.Contains("BanBlockingTaskAccess", ids);
        Assert.Contains("EnforceNamespaceDirectoryMapping", ids);
        Assert.Contains("DetectAndBanPhantomDependencies", ids);
        Assert.Contains("StaticTestSentinel", ids);
        Assert.Contains("EnforceSealedClasses", ids);
        Assert.Contains("EnforcePascalCase", ids);
        Assert.Contains("EnforceNullableEnable", ids);
        Assert.Contains("EnforceSemanticNaming", ids);
        Assert.Contains("EnforceValueObjectContracts", ids);
    }
}
