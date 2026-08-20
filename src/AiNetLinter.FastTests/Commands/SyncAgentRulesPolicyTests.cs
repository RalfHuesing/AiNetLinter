#nullable enable

using System;
using System.IO;
using AiNetLinter.Generators;
using Xunit;

namespace AiNetLinter.FastTests.Commands;

[Trait("Category", "Unit")]
public sealed class SyncAgentRulesPolicyTests
{
    [Fact]
    public void ResolveBaseDirectory_ExistingDirectory_ReturnsSame()
    {
        using var tempDir = TestTempDirectory.Create("SyncBaseDir_");
        var result = AgentRulesGenerator.ResolveBaseDirectory(tempDir.DirectoryPath);
        Assert.Equal(tempDir.DirectoryPath, result);
    }

    [Fact]
    public void ResolveBaseDirectory_ExistingFile_ReturnsParentDirectory()
    {
        using var tempDir = TestTempDirectory.Create("SyncBaseFile_");
        var tmpFile = tempDir.CreateFile("rules.json", "{}");
        var result = AgentRulesGenerator.ResolveBaseDirectory(tmpFile);
        Assert.Equal(tempDir.DirectoryPath, result);
    }

    [Fact]
    public void ResolveAgentRulesPath_CustomPathAsDirectory_AppendsDefaultFileName()
    {
        using var tempDir = TestTempDirectory.Create("ResolveCustomDir_");
        var result = AgentRulesGenerator.ResolveAgentRulesPath(tempDir.DirectoryPath, tempDir.DirectoryPath);
        Assert.Equal(Path.Combine(tempDir.DirectoryPath, "AiNetLinter.mdc"), result);
    }

    [Fact]
    public void ResolveAgentRulesPath_CustomPathAsMdcFile_ReturnsSame()
    {
        using var tempDir = TestTempDirectory.Create("ResolveCustomFile_");
        var customPath = tempDir.GetPath("my_custom.mdc");
        var result = AgentRulesGenerator.ResolveAgentRulesPath(tempDir.DirectoryPath, customPath);
        Assert.Equal(customPath, result);
    }

    [Fact]
    public void ResolveAgentRulesPath_Guessing_DefaultsToAgentsRules()
    {
        using var tempDir = TestTempDirectory.Create("ResolveGuess_");
        var result = AgentRulesGenerator.ResolveAgentRulesPath(tempDir.DirectoryPath);
        Assert.Equal(Path.Combine(tempDir.DirectoryPath, ".agents", "rules", "AiNetLinter.mdc"), result);
    }

    [Fact]
    public void DetectBaselineUsage_NoBaselineFileOrArg_ReturnsFalse()
    {
        using var tempDir = TestTempDirectory.Create("DetectBaseline_False_");
        var result = AgentRulesGenerator.DetectBaselineUsage(tempDir.DirectoryPath);
        Assert.False(result);
    }

    [Fact]
    public void DetectBaselineUsage_BaselineJsonExists_ReturnsTrue()
    {
        using var tempDir = TestTempDirectory.Create("DetectBaseline_True_");
        tempDir.CreateFile("baseline.json", "{}");
        var result = AgentRulesGenerator.DetectBaselineUsage(tempDir.DirectoryPath);
        Assert.True(result);
    }

    [Fact]
    public void DetectBaselineUsage_BaselinePathArgExists_ReturnsTrue()
    {
        using var tempDir = TestTempDirectory.Create("DetectBaseline_Arg_");
        var customBaseline = tempDir.CreateFile("my_custom_baseline.json", "{}");
        var result = AgentRulesGenerator.DetectBaselineUsage(tempDir.DirectoryPath, customBaseline);
        Assert.True(result);
    }

    [Fact]
    public void GenerateContent_WithHasBaselineTrue_IncludesBaselineSection()
    {
        var config = new AiNetLinter.Configuration.Config
        {
            Global = new AiNetLinter.Configuration.GlobalConfig(),
            Metrics = new AiNetLinter.Configuration.MetricsConfig(),
        };
        var content = AgentRulesGenerator.GenerateContent(config, "rules.json", hasBaseline: true);
        Assert.Contains("## Baseline-Mechanik (Inkrementelle Analyse)", content);
        Assert.Contains("--create-baseline", content);
    }

    [Fact]
    public void GenerateContent_WithHasBaselineFalse_OmitsBaselineSection()
    {
        var config = new AiNetLinter.Configuration.Config
        {
            Global = new AiNetLinter.Configuration.GlobalConfig(),
            Metrics = new AiNetLinter.Configuration.MetricsConfig(),
        };
        var content = AgentRulesGenerator.GenerateContent(config, "rules.json", hasBaseline: false);
        Assert.DoesNotContain("## Baseline-Mechanik", content);
    }

    [Fact]
    public void GenerateContent_WithAbsolutePath_UsesOnlyFileNameInHeader()
    {
        var config = new AiNetLinter.Configuration.Config
        {
            Global = new AiNetLinter.Configuration.GlobalConfig(),
            Metrics = new AiNetLinter.Configuration.MetricsConfig(),
        };
        var fullPath = @"C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\San.smart.Planner.Platform.Tests.Logic\AiNetLinter\rules\platform-default.rules.json";
        var content = AgentRulesGenerator.GenerateContent(config, fullPath);

        Assert.Contains("aus `platform-default.rules.json`.", content);
        Assert.DoesNotContain(@"C:\Daten\", content);
    }
}
