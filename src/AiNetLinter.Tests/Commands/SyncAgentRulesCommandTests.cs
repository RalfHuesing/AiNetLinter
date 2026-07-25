#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Generators;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// Tests für <see cref="SyncAgentRulesCommand"/>.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class SyncAgentRulesCommandTests
{
    [Fact]
    public void Run_CheckMode_WhenFileNotExists_ReturnsOne()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "SyncAgentRulesTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);

        // Erstelle ein minimales rules.json damit Config geladen werden kann
        var rulesPath = Path.Combine(tmpDir, "rules.json");
        File.WriteAllText(rulesPath, "{}", Encoding.UTF8);

        var args = new LinterArgs
        {
            TargetPath = tmpDir,
            ConfigPath = rulesPath,
            Verbose = false,
            Check = true,
            SyncAgentRules = true,
        };

        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);
        try
        {
            var result = SyncAgentRulesCommand.Run(args);
            Assert.Equal(1, result);
        }
        finally
        {
            Console.SetError(originalError);
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WriteMode_CreatesFile()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "SyncAgentRulesTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);

        // Traversiere von AppContext.BaseDirectory bis rules.json gefunden wird
        var rulesPath = FindRulesJson();
        if (rulesPath == null)
        {
            Directory.Delete(tmpDir, recursive: true);
            return; // Kein Fehler im CI ohne rules.json
        }

        var args = new LinterArgs
        {
            TargetPath = tmpDir,
            ConfigPath = rulesPath,
            Verbose = false,
            Check = false,
            SyncAgentRules = true,
        };

        try
        {
            var result = SyncAgentRulesCommand.Run(args);
            Assert.Equal(0, result);

            var mdcPath = Path.Combine(tmpDir, ".agents", "rules", "AiNetLinter.mdc");
            Assert.True(File.Exists(mdcPath), "Die .mdc-Datei sollte erstellt worden sein.");
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    private static string? FindRulesJson()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "rules.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void ResolveBaseDirectory_ExistingDirectory_ReturnsSame()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "SyncBaseDir_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var result = SyncAgentRulesCommand.ResolveBaseDirectory(tmpDir);
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
            var result = SyncAgentRulesCommand.ResolveBaseDirectory(tmpFile);
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
}
