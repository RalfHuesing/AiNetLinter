using Xunit;
using System.Diagnostics;
using System.IO;

namespace AiNetLinter.Tests.Cli;

// @covers LinterArgs
[Collection("ConsoleTestCollection")]
public sealed class CliIntegrationTests
{
    [Fact]
    public void RunLinterCli_OnWholeSolution_ReturnsSuccess()
    {
        // Arrange
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "rules.json");
        var targetPath = rootDir;

        Assert.True(File.Exists(linterDllPath), $"Linter-DLL nicht gefunden unter: {linterDllPath}");
        Assert.True(File.Exists(configPath), $"Konfigurationsdatei nicht gefunden unter: {configPath}");
        Assert.True(Directory.Exists(targetPath), $"Zielverzeichnis nicht gefunden unter: {targetPath}");

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{configPath}\" --path \"{targetPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        using var process = Process.Start(processInfo);
        Assert.NotNull(process);

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        // Assert
        Assert.Null(error == "" ? null : error);
        Assert.True(process.ExitCode == 0, $"Linter schlug mit Exit-Code {process.ExitCode} fehl. Output:\n{output}\nError:\n{error}");
        Assert.Contains("OK", output);
    }

    [Fact]
    public void GeneratePlaybook_ForSolution_GeneratesAndUpdatesPlaybook()
    {
        // Arrange
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "rules.json");
        var playbookFile = Path.Combine(rootDir, ".cursor", "rules", "playbook.md");

        Assert.True(File.Exists(linterDllPath), $"Linter-DLL nicht gefunden unter: {linterDllPath}");
        Assert.True(File.Exists(configPath), $"Konfigurationsdatei nicht gefunden unter: {configPath}");

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{configPath}\" --path \"{rootDir}\" --playbook \"{playbookFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        using var process = Process.Start(processInfo);
        Assert.NotNull(process);

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        // Assert
        Assert.True(process.ExitCode == 0, $"Linter schlug mit Exit-Code {process.ExitCode} fehl. Output:\n{output}\nError:\n{error}");
        Assert.True(File.Exists(playbookFile), $"Playbook-Datei wurde nicht erzeugt unter: {playbookFile}");

        var content = File.ReadAllText(playbookFile);
        Assert.Contains("Auto-generiert durch AiNetLinter", content);
        Assert.Contains("AI Repository Playbook (Auto-Generated)", content);
    }

    [Fact]
    public void SyncCursorRulesAndPlaybook_Combined_GeneratesBoth()
    {
        // Reproduziert den P0-Bug: --sync-cursor-rules + --playbook im selben Aufruf
        // sollte beide Artefakte erzeugen (früher return verhinderte das Playbook).
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "rules.json");
        var tempPlaybookPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");

        Assert.True(File.Exists(linterDllPath), $"Linter-DLL nicht gefunden: {linterDllPath}");
        Assert.True(File.Exists(configPath), $"Config nicht gefunden: {configPath}");

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{configPath}\" --path \"{rootDir}\" --sync-cursor-rules --playbook \"{tempPlaybookPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(processInfo);
            Assert.NotNull(process);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0,
                $"Kombinierter Aufruf fehlgeschlagen (Exit {process.ExitCode}).\nOutput: {output}\nError: {error}");
            Assert.True(File.Exists(tempPlaybookPath),
                $"Playbook wurde nicht erzeugt (P0-Bug). Output: {output}");
            var content = File.ReadAllText(tempPlaybookPath);
            Assert.Contains("AI Repository Playbook", content);
        }
        finally
        {
            if (File.Exists(tempPlaybookPath)) File.Delete(tempPlaybookPath);
        }
    }

    [Fact]
    public void SyncCursorRules_WithViolations_RunsLintAndReturnsExitCodeOneAndSyncsRules()
    {
        using var workspace = new Fixtures.BaselineMiniFixtureWorkspace();
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);

        var tempCursorRulesDir = Path.Combine(workspace.RootPath, ".cursor", "rules");
        Directory.CreateDirectory(tempCursorRulesDir);
        var expectedMdcPath = Path.Combine(tempCursorRulesDir, "AiNetLinter.mdc");

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --sync-cursor-rules",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        Assert.NotNull(process);
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(1, process.ExitCode);
        Assert.Contains("EnforceSealedClasses", output);
        Assert.True(File.Exists(expectedMdcPath), $"MDC-Datei wurde nicht erzeugt unter: {expectedMdcPath}. Output:\n{output}");
    }

    [Fact]
    public void SyncCursorRulesOnly_WithViolations_ReturnsSuccessAndSyncsRules()
    {
        using var workspace = new Fixtures.BaselineMiniFixtureWorkspace();
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);

        var tempCursorRulesDir = Path.Combine(workspace.RootPath, ".cursor", "rules");
        Directory.CreateDirectory(tempCursorRulesDir);
        var expectedMdcPath = Path.Combine(tempCursorRulesDir, "AiNetLinter.mdc");

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --sync-cursor-rules-only",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        Assert.NotNull(process);
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        Assert.DoesNotContain("EnforceSealedClasses", output);
        Assert.True(File.Exists(expectedMdcPath), $"MDC-Datei wurde nicht erzeugt unter: {expectedMdcPath}");
    }

    [Fact]
    public void GeneratePlaybook_WithCheckFlag_ReturnsOkWhenUpToDate()
    {
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "rules.json");
        var tempPlaybookPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");

        Assert.True(File.Exists(linterDllPath));
        Assert.True(File.Exists(configPath));

        ProcessStartInfo MakeProcess(string extraArgs) => new()
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{configPath}\" --path \"{rootDir}\" --playbook \"{tempPlaybookPath}\" {extraArgs}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            // Erst generieren
            using (var genProcess = Process.Start(MakeProcess("")))
            {
                Assert.NotNull(genProcess);
                genProcess.StandardOutput.ReadToEnd();
                genProcess.WaitForExit();
                Assert.Equal(0, genProcess.ExitCode);
            }

            Assert.True(File.Exists(tempPlaybookPath));

            // Dann prüfen (--check)
            using var checkProcess = Process.Start(MakeProcess("--check"));
            Assert.NotNull(checkProcess);
            string output = checkProcess.StandardOutput.ReadToEnd();
            string error = checkProcess.StandardError.ReadToEnd();
            checkProcess.WaitForExit();

            Assert.True(checkProcess.ExitCode == 0,
                $"--playbook --check sollte Exit 0 liefern. Output: {output}\nError: {error}");
            Assert.Contains("[OK]", output);
        }
        finally
        {
            if (File.Exists(tempPlaybookPath)) File.Delete(tempPlaybookPath);
        }
    }

    [Fact]
    public void RunLinterCli_WithInvalidConfig_ReturnsErrorExitCode()
    {
        // Arrange
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "non-existent-config.json");
        var targetPath = rootDir;

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{configPath}\" --path \"{targetPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        using var process = Process.Start(processInfo);
        Assert.NotNull(process);

        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        // Assert
        Assert.True(process.ExitCode == 1, $"Linter sollte mit Exit-Code 1 fehlschlagen, beendete aber mit {process.ExitCode}.");
        Assert.Contains("[ERROR]", error);
    }

    /// <summary>
    /// Kein Assert — schreibt Linter-Output nach test-output/self-lint.txt (.gitignore'd).
    /// Für Claude Code: nach dotnet test die Datei lesen statt erneut zu testen.
    /// </summary>
    [Fact]
    public void DiagnosticDump_SelfLintOutput_WritesToFile()
    {
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "rules.json");
        var outputDir = Path.Combine(rootDir, "test-output");
        var outputFile = Path.Combine(outputDir, "self-lint.txt");

        Directory.CreateDirectory(outputDir);

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{configPath}\" --path \"{rootDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        File.WriteAllText(outputFile, $"ExitCode: {process.ExitCode}\n\n{output}\n---STDERR---\n{error}");
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }
            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }

    private static string FindLinterDll(string rootDir)
    {
        var binDir = Path.Combine(rootDir, "src", "AiNetLinter", "bin");
        if (!Directory.Exists(binDir))
        {
            throw new DirectoryNotFoundException($"Das Build-Ausgabeverzeichnis existiert nicht: {binDir}");
        }

        // Suche nach AiNetLinter.dll in Debug/Release Ordnern
        var files = Directory.GetFiles(binDir, "AiNetLinter.dll", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            throw new FileNotFoundException("Die Datei 'AiNetLinter.dll' wurde in keinem Build-Unterordner gefunden.");
        }

        // Falls mehrere existieren (z. B. Debug und Release), nimm den aktuellsten
        return files[0];
    }
}
