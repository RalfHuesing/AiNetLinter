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
        var tmpDir = Path.Combine(Path.GetTempPath(), "SyncBaseDir_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var result = AgentRulesGenerator.ResolveBaseDirectory(tmpDir);
            Assert.Equal(tmpDir, result);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveBaseDirectory_ExistingFile_ReturnsParentDirectory()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var result = AgentRulesGenerator.ResolveBaseDirectory(tmpFile);
            Assert.Equal(Path.GetDirectoryName(tmpFile), result);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void ResolveAgentRulesPath_CustomPathAsDirectory_AppendsDefaultFileName()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "ResolveCustomDir_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var result = AgentRulesGenerator.ResolveAgentRulesPath(tmpDir, tmpDir);
            Assert.Equal(Path.Combine(tmpDir, "AiNetLinter.mdc"), result);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveAgentRulesPath_CustomPathAsMdcFile_ReturnsSame()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ResolveCustomFile_" + Guid.NewGuid());
        var customPath = Path.Combine(baseDir, "my_custom.mdc");
        var result = AgentRulesGenerator.ResolveAgentRulesPath(baseDir, customPath);
        Assert.Equal(customPath, result);
    }

    [Fact]
    public void ResolveAgentRulesPath_Guessing_DefaultsToAgentsRules()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ResolveGuess_" + Guid.NewGuid());
        var result = AgentRulesGenerator.ResolveAgentRulesPath(baseDir);
        Assert.Equal(Path.Combine(baseDir, ".agents", "rules", "AiNetLinter.mdc"), result);
    }

    [Fact]
    public void DetectBaselineUsage_NoBaselineFileOrArg_ReturnsFalse()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "DetectBaseline_False_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var result = AgentRulesGenerator.DetectBaselineUsage(tmpDir);
            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void DetectBaselineUsage_BaselineJsonExists_ReturnsTrue()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "DetectBaseline_True_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);
        var baselineFile = Path.Combine(tmpDir, "baseline.json");
        File.WriteAllText(baselineFile, "{}");
        try
        {
            var result = AgentRulesGenerator.DetectBaselineUsage(tmpDir);
            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void DetectBaselineUsage_BaselinePathArgExists_ReturnsTrue()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "DetectBaseline_Arg_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);
        var customBaseline = Path.Combine(tmpDir, "my_custom_baseline.json");
        File.WriteAllText(customBaseline, "{}");
        try
        {
            var result = AgentRulesGenerator.DetectBaselineUsage(tmpDir, customBaseline);
            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
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
