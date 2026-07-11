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
/// Tests für <see cref="SyncCursorRulesCommand"/>.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class SyncCursorRulesCommandTests
{
    [Fact]
    public void Run_CheckMode_WhenFileNotExists_ReturnsOne()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "SyncCursorRulesTest_" + Guid.NewGuid());
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
            SyncCursorRules = true,
        };

        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);
        try
        {
            var result = SyncCursorRulesCommand.Run(args);
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
        var tmpDir = Path.Combine(Path.GetTempPath(), "SyncCursorRulesTest_" + Guid.NewGuid());
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
            SyncCursorRules = true,
        };

        try
        {
            var result = SyncCursorRulesCommand.Run(args);
            Assert.Equal(0, result);

            var mdcPath = Path.Combine(tmpDir, ".cursor", "rules", "AiNetLinter.mdc");
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
            var result = SyncCursorRulesCommand.ResolveBaseDirectory(tmpDir);
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
            var result = SyncCursorRulesCommand.ResolveBaseDirectory(tmpFile);
            Assert.Equal(Path.GetDirectoryName(tmpFile), result);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void ResolveCursorRulesPath_CustomPathAsDirectory_AppendsDefaultFileName()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "ResolveCustomDir_" + Guid.NewGuid());
        Directory.CreateDirectory(tmpDir);
        try
        {
            var result = CursorRulesGenerator.ResolveCursorRulesPath(tmpDir, tmpDir);
            Assert.Equal(Path.Combine(tmpDir, "AiNetLinter.mdc"), result);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveCursorRulesPath_CustomPathAsMdcFile_ReturnsSame()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ResolveCustomFile_" + Guid.NewGuid());
        var customPath = Path.Combine(baseDir, "my_custom.mdc");
        var result = CursorRulesGenerator.ResolveCursorRulesPath(baseDir, customPath);
        Assert.Equal(customPath, result);
    }

    [Fact]
    public void ResolveCursorRulesPath_Guessing_PrefersAgentsRulesIfItExists()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ResolveGuess_" + Guid.NewGuid());
        var agentsDir = Path.Combine(baseDir, ".agents", "rules");
        var cursorDir = Path.Combine(baseDir, ".cursor", "rules");
        
        Directory.CreateDirectory(agentsDir);
        Directory.CreateDirectory(cursorDir);
        
        try
        {
            var result = CursorRulesGenerator.ResolveCursorRulesPath(baseDir);
            Assert.Equal(Path.Combine(agentsDir, "AiNetLinter.mdc"), result);
        }
        finally
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveCursorRulesPath_Guessing_FallsBackToCursorRulesIfOnlyItExists()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ResolveGuess_" + Guid.NewGuid());
        var cursorDir = Path.Combine(baseDir, ".cursor", "rules");
        
        Directory.CreateDirectory(cursorDir);
        
        try
        {
            var result = CursorRulesGenerator.ResolveCursorRulesPath(baseDir);
            Assert.Equal(Path.Combine(cursorDir, "AiNetLinter.mdc"), result);
        }
        finally
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveCursorRulesPath_Guessing_DefaultsToCursorRulesIfNeitherExists()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ResolveGuess_" + Guid.NewGuid());
        // Neither directory exists
        var result = CursorRulesGenerator.ResolveCursorRulesPath(baseDir);
        Assert.Equal(Path.Combine(baseDir, ".cursor", "rules", "AiNetLinter.mdc"), result);
    }
}
