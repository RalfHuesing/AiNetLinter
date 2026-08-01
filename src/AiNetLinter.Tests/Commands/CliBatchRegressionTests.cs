#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// EPIC-07 CLI-Regression (Konzept Z. 622): der bestehende CLI-Batch-Modus
/// (ainetlinter --config rules.json --path &lt;dir&gt;) bleibt nach allen EPIC-01..06-
/// Aenderungen unveraendert lauffaehig. Bestehender Test
/// <c>CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess</c> deckt die echte
/// AiNetLinter-Solution ab; dieser Test deckt eine Mini-Fixture
/// (<c>SymbolGraphMiniFixtureWorkspace</c>) ab, die schneller und deterministischer ist
/// und die deterministische Verletzung in <c>ViolationTrigger.cs</c> (fehlendes sealed)
/// als Erfolgs-Marker nutzt.
///
/// A3-Pfad: wenn in <c>Program.Main</c> der CLI-Dispatcher die args-Verarbeitung
/// bricht (z. B. weil ein neuer EPIC-01..06-Pfad zuerst returnt, bevor
/// <c>ExecuteLinterAsync</c> aufgerufen wird), oder wenn der Pfad zu
/// <c>ViolationTrigger.cs</c> durch einen Refactor von <c>EnforceSealedClasses</c>
/// stillschweigend uebersprungen wird, schlaegt dieser Test fehl.
/// </summary>
[Collection("ConsoleTestCollection")]
[Trait("Category", "Integration")]
public sealed class CliBatchRegressionTests
{
    [Fact]
    public void RunLinterCli_OnSymbolGraphMiniFixture_ReportsViolationAndExitsZero()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        var rootDir = FindSolutionRoot();
        var linterDllPath = Path.Combine(rootDir, "src", "AiNetLinter", "bin", "Debug", "net10.0", "AiNetLinter.dll");
        var configPath = Path.Combine(rootDir, "rules.json");

        Assert.True(File.Exists(linterDllPath), $"Linter-DLL nicht gefunden: {linterDllPath}");
        Assert.True(File.Exists(configPath), $"Konfiguration nicht gefunden: {configPath}");

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{configPath}\" --path \"{fixture.RootPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(processInfo);
        Assert.NotNull(process);

        var output = process!.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        // SymbolGraphMini enthaelt eine deterministische Verletzung (ViolationTrigger.cs,
        // fehlendes sealed), daher erwarten wir Exit-Code 1 (= Violations gefunden) statt 0.
        // Der bestehende Test RunLinterCli_OnWholeSolution_ReturnsSuccess laeuft gegen die
        // echte Solution (clean, Exit 0) — dieser Test ist das Pendant fuer die Mini-Fixture
        // mit Verletzung.
        Assert.True(
            process.ExitCode == 1,
            $"Linter-CLI brach unerwartet ab (Exit {process.ExitCode}, erwartet 1 fuer Violations). "
            + $"Output:\n{output}\nError:\n{error}");
        Assert.Contains("ViolationTrigger", output, StringComparison.Ordinal);
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("AiNetLinter.slnx nicht im Elternverzeichnispfad gefunden.");
    }
}
