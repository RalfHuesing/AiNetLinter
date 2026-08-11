using Xunit;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Output;

namespace AiNetLinter.Tests.Cli;

// @covers LinterArgs
/// <summary>
/// Die meisten Tests rufen die jeweilige Command-Klasse (<see cref="AuditCommand"/>,
/// <see cref="PlaybookCheckCommand"/>, <see cref="SyncAgentRulesCommand"/>) direkt in-process
/// auf statt einen echten <c>dotnet AiNetLinter.dll</c>-Subprozess zu starten — das spart die
/// Subprozess-Start-/Konkurrenz-Overhead, der diese Testklasse frueher zur dominanten
/// Laufzeitquelle gemacht hat. <c>RunLinterCli_WithInvalidConfig_ReturnsErrorExitCode</c> leitet
/// dafuer <see cref="System.Console.Error"/> um (der Config-Ladefehler wird von
/// <c>ConfigLoader</c> direkt auf die echte Konsole geschrieben, nicht ueber die injizierbare
/// <c>ILintConsole</c>) — parallel laufende Tests wuerden sich die globale Konsolenumleitung
/// gegenseitig ueberschreiben, daher die <see cref="ConsoleTestCollection"/>-Zugehoerigkeit.
/// </summary>
[Collection("ConsoleTestCollection")]
[Trait("Category", "Integration")]
public sealed class CliIntegrationTests
{
    /// <summary>
    /// Bewusst als echter Subprozess-Test belassen (nicht auf in-process konvertiert): dies ist
    /// der einzige Test, der beweist, dass die gebaute <c>AiNetLinter.dll</c> als eigenstaendiges
    /// Artefakt ueberhaupt lauffaehig ist (Packaging-Sanity-Check) — etwas, das In-Process-Tests
    /// strukturell nicht abdecken koennen, da sie direkt gegen den Quellcode statt gegen das
    /// gebaute Artefakt laufen.
    /// </summary>
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
        // ExitCode 0 ist der eigentliche Packaging-Sanity-Beweis (kein Error-severity-Verstoss,
        // siehe AuditCommand.WriteViolationsAndExit) — der literale "OK"-Marker (nur bei
        // violations.Count == 0) ist seit Einfuehrung von DuplicateCode (Severity "info") kein
        // verlaesslicher Dauerzustand mehr: das eigene Repo hat inzwischen legitime,
        // niedrigschwellige Duplicate-Code-Kandidaten, die den Linter nicht scheitern lassen sollen.
        Assert.True(result.ExitCode == 0, $"Linter schlug mit Exit-Code {result.ExitCode} fehl. Output:\n{result.Output}\nError:\n{result.Error}");
    }

    [Fact]
    public async Task GeneratePlaybook_ForSolution_GeneratesAndUpdatesPlaybook()
    {
        var rootDir = CliProcessRunner.FindSolutionRoot();
        var configPath = Path.Combine(rootDir, "rules.json");
        var playbookFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");

        Assert.True(File.Exists(configPath), $"Konfigurationsdatei nicht gefunden unter: {configPath}");

        try
        {
            var args = new LinterArgs
            {
                TargetPath = rootDir,
                Verbose = false,
                ConfigPath = configPath,
                PlaybookPath = playbookFile,
            };
            var console = new TestLintConsole();
            var exitCode = await AuditCommand.RunAsync(args, default, console);

            Assert.True(exitCode == 0, $"Linter schlug mit Exit-Code {exitCode} fehl. Output:\n{console.OutputText}\nError:\n{console.ErrorText}");
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
        var configPath = Path.Combine(rootDir, "rules.json");
        var tempPlaybookPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");

        Assert.True(File.Exists(configPath), $"Config nicht gefunden: {configPath}");

        try
        {
            var args = new LinterArgs
            {
                TargetPath = rootDir,
                Verbose = false,
                ConfigPath = configPath,
                SyncAgentRules = true,
                PlaybookPath = tempPlaybookPath,
            };
            var console = new TestLintConsole();
            var exitCode = await AuditCommand.RunAsync(args, default, console);

            Assert.True(exitCode == 0,
                $"Kombinierter Aufruf fehlgeschlagen (Exit {exitCode}).\nOutput: {console.OutputText}\nError: {console.ErrorText}");
            Assert.True(File.Exists(tempPlaybookPath),
                $"Playbook wurde nicht erzeugt (P0-Bug). Output: {console.OutputText}");
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

        var args = new LinterArgs
        {
            TargetPath = workspace.RootPath,
            Verbose = false,
            ConfigPath = workspace.ConfigPath,
            SyncAgentRules = true,
        };
        var console = new TestLintConsole();
        var exitCode = await AuditCommand.RunAsync(args, default, console);

        Assert.Equal(1, exitCode);
        Assert.Contains("EnforceSealedClasses", console.OutputText);
        Assert.True(File.Exists(expectedMdcPath), $"MDC-Datei wurde nicht erzeugt unter: {expectedMdcPath}. Output:\n{console.OutputText}");
    }

    [Fact]
    public void SyncAgentRulesOnly_WithViolations_ReturnsSuccessAndSyncsRules()
    {
        using var workspace = new Fixtures.BaselineMiniFixtureWorkspace();

        var tempAgentRulesDir = Path.Combine(workspace.RootPath, ".agents", "rules");
        Directory.CreateDirectory(tempAgentRulesDir);
        var expectedMdcPath = Path.Combine(tempAgentRulesDir, "AiNetLinter.mdc");

        var args = new LinterArgs
        {
            TargetPath = workspace.RootPath,
            Verbose = false,
            ConfigPath = workspace.ConfigPath,
            SyncAgentRulesOnly = true,
        };
        var console = new TestLintConsole();
        var exitCode = SyncAgentRulesCommand.Run(args, console);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("EnforceSealedClasses", console.OutputText);
        Assert.True(File.Exists(expectedMdcPath), $"MDC-Datei wurde nicht erzeugt unter: {expectedMdcPath}");
    }

    [Fact]
    public async Task GeneratePlaybook_WithCheckFlag_ReturnsOkWhenUpToDate()
    {
        var rootDir = CliProcessRunner.FindSolutionRoot();
        var configPath = Path.Combine(rootDir, "rules.json");
        var tempPlaybookPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");

        Assert.True(File.Exists(configPath));

        try
        {
            // Erst generieren
            var genArgs = new LinterArgs
            {
                TargetPath = rootDir,
                Verbose = false,
                ConfigPath = configPath,
                PlaybookPath = tempPlaybookPath,
            };
            var genConsole = new TestLintConsole();
            var genExitCode = await AuditCommand.RunAsync(genArgs, default, genConsole);
            Assert.Equal(0, genExitCode);

            Assert.True(File.Exists(tempPlaybookPath));

            // Dann prüfen (--check)
            var checkArgs = new LinterArgs
            {
                TargetPath = rootDir,
                Verbose = false,
                ConfigPath = configPath,
                PlaybookPath = tempPlaybookPath,
                Check = true,
            };
            var checkConsole = new TestLintConsole();
            var checkExitCode = await PlaybookCheckCommand.RunAsync(checkArgs, default, checkConsole);

            Assert.True(checkExitCode == 0,
                $"--playbook --check sollte Exit 0 liefern. Output: {checkConsole.OutputText}\nError: {checkConsole.ErrorText}");
            Assert.Contains("[OK]", checkConsole.OutputText);
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

        var args = new LinterArgs
        {
            TargetPath = rootDir,
            Verbose = false,
            ConfigPath = configPath,
        };

        // ConfigLoader schreibt den Ladefehler direkt auf Console.Error statt ueber die
        // injizierbare ILintConsole — deshalb hier echte Konsolenumleitung (Muster B) statt
        // TestLintConsole.
        var originalError = System.Console.Error;
        using var errorWriter = new StringWriter();
        System.Console.SetError(errorWriter);
        int exitCode;
        try
        {
            exitCode = await AuditCommand.RunAsync(args);
        }
        finally
        {
            System.Console.SetError(originalError);
        }

        Assert.True(exitCode == 1, $"Linter sollte mit Exit-Code 1 fehlschlagen, beendete aber mit {exitCode}.");
        Assert.Contains("[ERROR]", errorWriter.ToString());
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

        var args = new LinterArgs
        {
            TargetPath = rootDir,
            Verbose = false,
            ConfigPath = configPath,
        };
        var console = new TestLintConsole();
        var exitCode = await AuditCommand.RunAsync(args, default, console);

        File.WriteAllText(outputFile, $"ExitCode: {exitCode}\n\n{console.OutputText}\n---STDERR---\n{console.ErrorText}");
    }
}
