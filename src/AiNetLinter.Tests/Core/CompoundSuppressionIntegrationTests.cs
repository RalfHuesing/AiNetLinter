#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;

namespace AiNetLinter.Tests.Core;

public sealed class CompoundSuppressionIntegrationTests
{
    private static string GenerateMethodCode(int lineCount, int parameterCount, int cc)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public class TestClass {");
        
        var parameters = string.Join(", ", Enumerable.Range(1, parameterCount).Select(i => $"int p{i}"));
        sb.AppendLine($"    public void TestMethod({parameters}) {{");
        
        // Add if branches to raise CC. Each if adds 1 to CC.
        for (int i = 1; i < cc; i++)
        {
            sb.AppendLine($"        if (p1 > {i}) {{ var temp{i} = {i}; }}");
        }
        
        // Fill up to method lines
        int currentMethodBodyLines = cc - 1;
        int targetMethodBodyLines = lineCount - 2; // -1 for signature, -1 for closing brace
        int fill = targetMethodBodyLines - currentMethodBodyLines;
        for (int i = 0; i < fill; i++)
        {
            sb.AppendLine($"        var fill{i} = {i};");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    [Fact]
    public void ScenarioA_SuppressionActive_RelaxedLimitMet()
    {
        // 80 lines, CC=1, 2 params. MaxMethodLineCount = 60 but CompoundRelaxedLimit = 150.
        var code = GenerateMethodCode(80, 2, 1);
        var (tree, model) = TestHelper.ParseCode(code);
        
        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxMethodLineCount = 60,
                CompoundSuppressions = new List<CompoundSuppression>
                {
                    new()
                    {
                        TargetRule = "MaxMethodLineCount",
                        WhenAllOf = new List<MetricCondition>
                        {
                            new() { Metric = "CyclomaticComplexity", AtMost = 3 },
                            new() { Metric = "CognitiveComplexity", AtMost = 5 }
                        },
                        RelaxedLimit = 150,
                        Reason = "Test reason"
                    }
                }
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodLineCount"));
    }

    [Fact]
    public void ScenarioB_SuppressionActive_RelaxedLimitExceeded()
    {
        // 160 lines, CC=1, 2 params. MaxMethodLineCount = 60 but CompoundRelaxedLimit = 150.
        var code = GenerateMethodCode(160, 2, 1);
        var (tree, model) = TestHelper.ParseCode(code);
        
        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxMethodLineCount = 60,
                CompoundSuppressions = new List<CompoundSuppression>
                {
                    new()
                    {
                        TargetRule = "MaxMethodLineCount",
                        WhenAllOf = new List<MetricCondition>
                        {
                            new() { Metric = "CyclomaticComplexity", AtMost = 3 },
                            new() { Metric = "CognitiveComplexity", AtMost = 5 }
                        },
                        RelaxedLimit = 150,
                        Reason = "Test reason"
                    }
                }
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        var violation = violations.FirstOrDefault(v => v.RuleName == "MaxMethodLineCount");
        
        Assert.NotNull(violation);
        Assert.Contains("Compound-Limit: 150", violation.Details);
        Assert.Contains("Standard: 60", violation.Details);
        Assert.Contains("CyclomaticComplexity=1 ≤ 3 ✓", violation.Details);
        Assert.Contains("CognitiveComplexity=0 ≤ 5 ✓", violation.Details);
        Assert.Contains("Compound-Bedingungen erfüllt, aber relaxiertes Limit ebenfalls überschritten", violation.Guidance);
    }

    [Fact]
    public void ScenarioC_SuppressionConfigured_ButInactive()
    {
        // 80 lines, CC=5, 2 params. MaxMethodLineCount = 60, CompoundRelaxedLimit = 150 when CC <= 3.
        var code = GenerateMethodCode(80, 2, 5);
        var (tree, model) = TestHelper.ParseCode(code);
        
        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxMethodLineCount = 60,
                CompoundSuppressions = new List<CompoundSuppression>
                {
                    new()
                    {
                        TargetRule = "MaxMethodLineCount",
                        WhenAllOf = new List<MetricCondition>
                        {
                            new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                        },
                        RelaxedLimit = 150,
                        Reason = "Test reason"
                    }
                }
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        var violation = violations.FirstOrDefault(v => v.RuleName == "MaxMethodLineCount");
        
        Assert.NotNull(violation);
        Assert.Contains("Compound-Suppression inaktiv: CyclomaticComplexity=5 ≤ 3 ✗", violation.Details);
        Assert.Contains("Optionen: (1) Komplexität senken", violation.Guidance);
    }

    [Fact]
    public void ScenarioD_NoSuppressionConfigured()
    {
        // 80 lines, CC=1. MaxMethodLineCount = 60. No compound suppressions.
        var code = GenerateMethodCode(80, 2, 1);
        var (tree, model) = TestHelper.ParseCode(code);
        
        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxMethodLineCount = 60,
                CompoundSuppressions = new List<CompoundSuppression>()
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        var violation = violations.FirstOrDefault(v => v.RuleName == "MaxMethodLineCount");
        
        Assert.NotNull(violation);
        Assert.DoesNotContain("Compound-Suppression", violation.Details);
        Assert.Contains("Lagere logische Abschnitte in kleinere Hilfsmethoden aus", violation.Guidance);
    }

    [Fact]
    public void ScenarioE_FullSuppression()
    {
        // 300 lines, CC=1. MaxMethodLineCount = 60. RelaxedLimit = null (full suppression).
        var code = GenerateMethodCode(300, 2, 1);
        var (tree, model) = TestHelper.ParseCode(code);
        
        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxMethodLineCount = 60,
                CompoundSuppressions = new List<CompoundSuppression>
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
                }
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodLineCount"));
    }

    [Fact]
    public void ScenarioF_ParameterCountSuppressionActive_RelaxedLimitMet()
    {
        // Method has 8 parameters, CC=1. MaxMethodParameterCount = 4 but CompoundRelaxedLimit = 10.
        var code = GenerateMethodCode(20, 8, 1);
        var (tree, model) = TestHelper.ParseCode(code);
        
        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxMethodParameterCount = 4,
                CompoundSuppressions = new List<CompoundSuppression>
                {
                    new()
                    {
                        TargetRule = "MaxMethodParameterCount",
                        WhenAllOf = new List<MetricCondition>
                        {
                            new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                        },
                        RelaxedLimit = 10
                    }
                }
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    private static string GenerateClassCode(int publicMemberCount, int constructorDependencies)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public class TestClass {");
        
        var parameters = string.Join(", ", Enumerable.Range(1, constructorDependencies).Select(i => $"int p{i}"));
        sb.AppendLine($"    public TestClass({parameters}) {{}}");
        
        for (int i = 0; i < publicMemberCount; i++)
        {
            sb.AppendLine($"    public int Prop{i} {{ get; set; }}");
        }
        
        sb.AppendLine("}");
        return sb.ToString();
    }

    [Fact]
    public void ScenarioG_MaxPublicMembersPerType_Suppressed_WhenConstructorDepsLow()
    {
        var code = GenerateClassCode(18, 2);
        var (tree, model) = TestHelper.ParseCode(code);

        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxPublicMembersPerType = 15,
                CompoundSuppressions = new List<CompoundSuppression>
                {
                    new()
                    {
                        TargetRule = "MaxPublicMembersPerType",
                        WhenAllOf = new List<MetricCondition>
                        {
                            new() { Metric = "ConstructorDependencies", AtMost = 2 }
                        },
                        RelaxedLimit = null // full suppression
                    }
                }
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.Empty(violations.Where(v => v.RuleName == "MaxPublicMembersPerType"));
    }

    [Fact]
    public void ScenarioH_MaxPublicMembersPerType_NotSuppressed_WhenConstructorDepsHigh()
    {
        var code = GenerateClassCode(18, 6);
        var (tree, model) = TestHelper.ParseCode(code);

        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxPublicMembersPerType = 15,
                CompoundSuppressions = new List<CompoundSuppression>
                {
                    new()
                    {
                        TargetRule = "MaxPublicMembersPerType",
                        WhenAllOf = new List<MetricCondition>
                        {
                            new() { Metric = "ConstructorDependencies", AtMost = 2 }
                        },
                        RelaxedLimit = null
                    }
                }
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.NotEmpty(violations.Where(v => v.RuleName == "MaxPublicMembersPerType"));
    }

    [Fact]
    public void ScenarioI_SeverityOverride_WhenRelaxedLimitExceeded_ViolationIsWarning()
    {
        // 160 lines, CC=1. RelaxedLimit=150, SeverityOverride="warning"
        var code = GenerateMethodCode(160, 2, 1);
        var (_, model) = TestHelper.ParseCode(code);

        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxMethodLineCount = 60,
                CompoundSuppressions = new List<CompoundSuppression>
                {
                    new()
                    {
                        TargetRule = "MaxMethodLineCount",
                        WhenAllOf = new List<MetricCondition>
                        {
                            new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                        },
                        RelaxedLimit = 150,
                        SeverityOverride = "warning"
                    }
                }
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        var violation = violations.FirstOrDefault(v => v.RuleName == "MaxMethodLineCount");

        Assert.NotNull(violation);
        Assert.Equal("warning", violation.EffectiveSeverity);
        Assert.Contains("Severity auf 'warning' herabgestuft", violation.Guidance);
    }

    [Fact]
    public void ScenarioJ_SeverityOverride_WhenRelaxedLimitMet_NoViolation()
    {
        // 80 lines, CC=1. RelaxedLimit=150, SeverityOverride="warning" — unter Limit → kein Verstoß
        var code = GenerateMethodCode(80, 2, 1);
        var (_, model) = TestHelper.ParseCode(code);

        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxMethodLineCount = 60,
                CompoundSuppressions = new List<CompoundSuppression>
                {
                    new()
                    {
                        TargetRule = "MaxMethodLineCount",
                        WhenAllOf = new List<MetricCondition>
                        {
                            new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                        },
                        RelaxedLimit = 150,
                        SeverityOverride = "warning"
                    }
                }
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodLineCount"));
    }

    [Fact]
    public void ScenarioK_SeverityOverride_ConditionsNotMet_ViolationIsError()
    {
        // 80 lines, CC=5 (> 3). RelaxedLimit=150, SeverityOverride="warning"
        // Bedingungen nicht erfüllt → normale error-Violation
        var code = GenerateMethodCode(80, 2, 5);
        var (_, model) = TestHelper.ParseCode(code);

        var config = TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxMethodLineCount = 60,
                CompoundSuppressions = new List<CompoundSuppression>
                {
                    new()
                    {
                        TargetRule = "MaxMethodLineCount",
                        WhenAllOf = new List<MetricCondition>
                        {
                            new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                        },
                        RelaxedLimit = 150,
                        SeverityOverride = "warning"
                    }
                }
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        var violation = violations.FirstOrDefault(v => v.RuleName == "MaxMethodLineCount");

        Assert.NotNull(violation);
        Assert.Null(violation.EffectiveSeverity); // keine Override → default error
    }

    [Fact]
    public void ScenarioL_SeverityOverride_OnlyWarnViolations_ExitCodeZero()
    {
        // Alle Violations sind warnings → HasErrorSeverity == false
        var violation = new RuleViolation
        {
            FilePath = "X.cs", LineNumber = 1,
            RuleName = "MaxMethodLineCount", Details = "...", Guidance = "...",
            EffectiveSeverity = "warning"
        };
        var config = TestHelper.CreateDefaultConfig();
        Assert.False(RuleMetadataRegistry.HasErrorSeverity(new[] { violation }, config));
    }
}
