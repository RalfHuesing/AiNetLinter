#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using AiNetLinter.Cli;
using AiNetLinter.Commands;

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
}
