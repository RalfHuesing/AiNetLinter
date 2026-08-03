using Xunit;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;

namespace AiNetLinter.Tests.Cli;

// @covers LinterArgs
[Trait("Category", "Integration")]
public sealed class CliIntegrationTests
{
    [Fact]
    public async Task RunLinterCli_OnWholeSolution_ReturnsSuccess()
    {
        var rootDir = CliProcessRunner.FindSolutionRoot();
        var linterDllPath = CliProcessRunner.FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "rules.json");
        var targetPath = rootDir;

        Assert.True(File.Exists(linterDllPath), $"Linter-DLL nicht gefunden unter: {linterDllPath}");
        Assert.True(File.Exists(configPath), $"Konfigurationsdatei nicht gefunden unter: {configPath}");
        Assert.True(Directory.Exists(targetPath), $"Zielverzeichnis nicht gefunden unter: {targetPath}");

        var result = await CliProcessRunner.RunLinterAsync($"--config \"{configPath}\" --path \"{targetPath}\"");

        Assert.Null(result.Error == "" ? null : result.Error);
        Assert.True(result.ExitCode == 0, $"Linter schlug mit Exit-Code {result.ExitCode} fehl. Output:\n{result.Output}\nError:\n{result.Error}");
        Assert.Contains("OK", result.Output);
    }

    [Fact]
    public async Task GeneratePlaybook_ForSolution_GeneratesAndUpdatesPlaybook()
    {
        var rootDir = CliProcessRunner.FindSolutionRoot();
        var linterDllPath = CliProcessRunner.FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "rules.json");
        var playbookFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");

        Assert.True(File.Exists(linterDllPath), $"Linter-DLL nicht gefunden unter: {linterDllPath}");
        Assert.True(File.Exists(configPath), $"Konfigurationsdatei nicht gefunden unter: {configPath}");

        try
        {
            var result = await CliProcessRunner.RunLinterAsync(
                $"--config \"{configPath}\" --path \"{rootDir}\" --playbook \"{playbookFile}\"");

            Assert.True(result.ExitCode == 0, $"Linter schlug mit Exit-Code {result.ExitCode} fehl. Output:\n{result.Output}\nError:\n{result.Error}");
            Assert.True(File.Exists(playbookFile), $"Playbook-Datei wurde nicht erzeugt unter: {playbookFile}");

            var content = File.ReadAllText(playbookFile);
            Assert.Contains("Auto-generiert durch AiNetLinter", content);
            Assert.Contains("AI Repository Playbook (Auto-Generated)", content);
        }
        finally
        {
            if (File.Exists(playbookFile))
            {
                File.Delete(playbookFile);
            }
        }
    }

    [Fact]
    public async Task SyncAgentRulesAndPlaybook_Combined_GeneratesBoth()
    {
        // Reproduziert den P0-Bug: --sync-agent-rules + --playbook im selben Aufruf
        // sollte beide Artefakte erzeugen (früher return verhinderte das Playbook).
        var rootDir = CliProcessRunner.FindSolutionRoot();
        var linterDllPath = CliProcessRunner.FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "rules.json");
        var tempPlaybookPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");

        Assert.True(File.Exists(linterDllPath), $"Linter-DLL nicht gefunden: {linterDllPath}");
        Assert.True(File.Exists(configPath), $"Config nicht gefunden: {configPath}");

        try
        {
            var result = await CliProcessRunner.RunLinterAsync(
                $"--config \"{configPath}\" --path \"{rootDir}\" --sync-agent-rules --playbook \"{tempPlaybookPath}\"");

            Assert.True(result.ExitCode == 0,
                $"Kombinierter Aufruf fehlgeschlagen (Exit {result.ExitCode}).\nOutput: {result.Output}\nError: {result.Error}");
            Assert.True(File.Exists(tempPlaybookPath),
                $"Playbook wurde nicht erzeugt (P0-Bug). Output: {result.Output}");
            var content = File.ReadAllText(tempPlaybookPath);
            Assert.Contains("AI Repository Playbook", content);
        }
        finally
        {
            if (File.Exists(tempPlaybookPath)) File.Delete(tempPlaybookPath);
        }
    }

    [Fact]
    public async Task SyncAgentRules_WithViolations_RunsLintAndReturnsExitCodeOneAndSyncsRules()
    {
        using var workspace = new Fixtures.BaselineMiniFixtureWorkspace();

        var tempAgentRulesDir = Path.Combine(workspace.RootPath, ".agents", "rules");
        Directory.CreateDirectory(tempAgentRulesDir);
        var expectedMdcPath = Path.Combine(tempAgentRulesDir, "AiNetLinter.mdc");

        var result = await CliProcessRunner.RunLinterAsync(
            $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --sync-agent-rules");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("EnforceSealedClasses", result.Output);
        Assert.True(File.Exists(expectedMdcPath), $"MDC-Datei wurde nicht erzeugt unter: {expectedMdcPath}. Output:\n{result.Output}");
    }

    [Fact]
    public async Task SyncAgentRulesOnly_WithViolations_ReturnsSuccessAndSyncsRules()
    {
        using var workspace = new Fixtures.BaselineMiniFixtureWorkspace();

        var tempAgentRulesDir = Path.Combine(workspace.RootPath, ".agents", "rules");
        Directory.CreateDirectory(tempAgentRulesDir);
        var expectedMdcPath = Path.Combine(tempAgentRulesDir, "AiNetLinter.mdc");

        var result = await CliProcessRunner.RunLinterAsync(
            $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --sync-agent-rules-only");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("EnforceSealedClasses", result.Output);
        Assert.True(File.Exists(expectedMdcPath), $"MDC-Datei wurde nicht erzeugt unter: {expectedMdcPath}");
    }

    [Fact]
    public async Task GeneratePlaybook_WithCheckFlag_ReturnsOkWhenUpToDate()
    {
        var rootDir = CliProcessRunner.FindSolutionRoot();
        var linterDllPath = CliProcessRunner.FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "rules.json");
        var tempPlaybookPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");

        Assert.True(File.Exists(linterDllPath));
        Assert.True(File.Exists(configPath));

        string BuildArguments(string extraArgs) =>
            $"--config \"{configPath}\" --path \"{rootDir}\" --playbook \"{tempPlaybookPath}\" {extraArgs}";

        try
        {
            // Erst generieren
            var genResult = await CliProcessRunner.RunLinterAsync(BuildArguments(""));
            Assert.Equal(0, genResult.ExitCode);

            Assert.True(File.Exists(tempPlaybookPath));

            // Dann prüfen (--check)
            var checkResult = await CliProcessRunner.RunLinterAsync(BuildArguments("--check"));

            Assert.True(checkResult.ExitCode == 0,
                $"--playbook --check sollte Exit 0 liefern. Output: {checkResult.Output}\nError: {checkResult.Error}");
            Assert.Contains("[OK]", checkResult.Output);
        }
        finally
        {
            if (File.Exists(tempPlaybookPath)) File.Delete(tempPlaybookPath);
        }
    }

    [Fact]
    public async Task RunLinterCli_WithInvalidConfig_ReturnsErrorExitCode()
    {
        var rootDir = CliProcessRunner.FindSolutionRoot();
        var configPath = Path.Combine(rootDir, "non-existent-config.json");
        var targetPath = rootDir;

        var result = await CliProcessRunner.RunLinterAsync($"--config \"{configPath}\" --path \"{targetPath}\"");

        Assert.True(result.ExitCode == 1, $"Linter sollte mit Exit-Code 1 fehlschlagen, beendete aber mit {result.ExitCode}.");
        Assert.Contains("[ERROR]", result.Error);
    }

    /// <summary>
    /// Kein Assert — schreibt Linter-Output nach test-output/self-lint.txt (.gitignore'd).
    /// Für LLM-Agenten: nach dotnet test die Datei lesen statt erneut zu testen.
    /// </summary>
    [Fact]
    public async Task DiagnosticDump_SelfLintOutput_WritesToFile()
    {
        var rootDir = CliProcessRunner.FindSolutionRoot();
        var configPath = Path.Combine(rootDir, "rules.json");
        var outputDir = Path.Combine(rootDir, "test-output");
        var outputFile = Path.Combine(outputDir, "self-lint.txt");

        Directory.CreateDirectory(outputDir);

        var result = await CliProcessRunner.RunLinterAsync($"--config \"{configPath}\" --path \"{rootDir}\"");

        File.WriteAllText(outputFile, $"ExitCode: {result.ExitCode}\n\n{result.Output}\n---STDERR---\n{result.Error}");
    }
}
