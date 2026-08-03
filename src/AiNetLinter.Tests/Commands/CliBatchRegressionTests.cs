#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
///: der bestehende CLI-Batch-Modus
/// (ainetlinter --config rules.json --path &lt;dir&gt;) bleibt nach allen
/// Aenderungen unveraendert lauffaehig. Bestehender Test
/// <c>CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess</c> deckt die echte
/// AiNetLinter-Solution ab; dieser Test deckt eine Mini-Fixture
/// (<c>SymbolGraphMiniFixtureWorkspace</c>) ab, die schneller und deterministischer ist
/// und die deterministische Verletzung in <c>ViolationTrigger.cs</c> (fehlendes sealed)
/// als Erfolgs-Marker nutzt.
///
/// A3-Pfad: wenn in <c>Program.Main</c> der CLI-Dispatcher die args-Verarbeitung
/// bricht (z. B. weil ein neuer
/// <c>ExecuteLinterAsync</c> aufgerufen wird), oder wenn der Pfad zu
/// <c>ViolationTrigger.cs</c> durch einen Refactor von <c>EnforceSealedClasses</c>
/// stillschweigend uebersprungen wird, schlaegt dieser Test fehl.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CliBatchRegressionTests
{
    [Fact]
    public async Task RunLinterCli_OnSymbolGraphMiniFixture_ReportsViolationAndExitsZero()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        var rootDir = CliProcessRunner.FindSolutionRoot();
        var configPath = Path.Combine(rootDir, "rules.json");

        Assert.True(File.Exists(configPath), $"Konfiguration nicht gefunden: {configPath}");

        var result = await CliProcessRunner.RunLinterAsync($"--config \"{configPath}\" --path \"{fixture.RootPath}\"");

        // SymbolGraphMini enthaelt eine deterministische Verletzung (ViolationTrigger.cs,
        // fehlendes sealed), daher erwarten wir Exit-Code 1 (= Violations gefunden) statt 0.
        // Der bestehende Test RunLinterCli_OnWholeSolution_ReturnsSuccess laeuft gegen die
        // echte Solution (clean, Exit 0) — dieser Test ist das Pendant fuer die Mini-Fixture
        // mit Verletzung.
        Assert.True(
            result.ExitCode == 1,
            $"Linter-CLI brach unerwartet ab (Exit {result.ExitCode}, erwartet 1 fuer Violations). "
            + $"Output:\n{result.Output}\nError:\n{result.Error}");
        Assert.Contains("ViolationTrigger", result.Output, StringComparison.Ordinal);
    }
}
