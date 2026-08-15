#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.MagicValues;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FindMagicValues;

/// <summary>
/// Argument-Aktivierungs-Tests fuer <see cref="FindMagicValuesScanner"/>: <c>includeSuppressed</c>
/// (wirksam via <c>HasDisableComment</c>), <c>includeTests</c> (Pfad-Match <c>/Tests/</c>)
/// und <c>changedOnly</c> (Git-Diff-Filter via <c>DiffImpactAnalyzer</c>). Aus
/// <see cref="FindMagicValuesScannerTests"/> in eine eigene Datei extrahiert, damit die
/// Haupt-Testklasse unter dem <c>MaxPublicMembersPerType: 15</c>-Limit bleibt. Geteilte
/// Helpers ueber <see cref="FindMagicValuesTestHelpers"/>.
/// </summary>
[Trait("Category", "Component")]
public sealed class FindMagicValuesScannerArgTests
{
    [Fact]
    public async Task ScanAsync_DefaultValueType_IsAll()
    {
        // Wird ohne valueType-Argument aufgerufen â€” der Default muss "all" (intern null)
        // sein, damit sowohl String-Literale als auch numerische Literale gefunden werden.
        // Vorher: Default war 'string', Zahlen wurden ignoriert.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com"";
    public const double Tolerance = 0.19;
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        // Beide Literale muessen gefunden werden (String + Number).
        Assert.Equal(2, result.Payload!.MagicValues.Count);
        Assert.Contains(result.Payload!.MagicValues, e => e.ValueType == "string");
        Assert.Contains(result.Payload!.MagicValues, e => e.ValueType == "number");
    }

    [Fact]
    public async Task ScanAsync_IncludeSuppressedFalse_SuppressesLiteralWithDisableComment()
    {
        // includeSuppressed=false unterdrueckt jetzt echte Suppression-Kommentare. Das Literal
        // mit // ainetlinter-disable MagicValues
        // wird NICHT gemeldet (0 Funde), waehrend includeSuppressed=true es melden wuerde
        // (siehe ScanAsync_IncludeSuppressedTrue_ReportsLiteralWithDisableComment).
        const string source = @"
namespace Test;
public sealed class Foo
{
    // ainetlinter-disable MagicValues
    public const string Url = ""https://api.example.com"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), options: new FindMagicValuesRunOptions(IncludeSuppressed: false));

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task ScanAsync_IncludeSuppressedTrue_ReportsLiteralWithDisableComment()
    {
        // includeSuppressed=true ignoriert den Suppression-Kommentar und meldet das Literal
        // trotzdem (1 Fund). Verifiziert das wirksame includeSuppressed-Argument.
        const string source = @"
namespace Test;
public sealed class Foo
{
    // ainetlinter-disable MagicValues
    public const string Url = ""https://api.example.com"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), options: new FindMagicValuesRunOptions(IncludeSuppressed: true));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("https://api.example.com", entry.Value);
    }

    [Fact]
    public async Task ScanAsync_IncludeTestsFalse_ExcludesTestPaths()
    {
        // includeTests=false (Default): Test-Pfade mit /Tests/ im Pfad werden ausgefiltert.
        // Nur das Production-File (ohne /Tests/) liefert einen Fund.
        var productionSource = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com"";
}";
        var testSource = @"
namespace Test;
public sealed class Bar
{
    public const string Url = ""https://api.test.com"";
}";
        using var testSolution = FindMagicValuesTestHelpers.CreateSolution(
            ("src/Production/Foo.cs", productionSource),
            ("tests/FastTests/Bar.cs", testSource));

        var result = await FindMagicValuesTestHelpers.RunAsync(testSolution.Solution, options: new FindMagicValuesRunOptions(IncludeTests: false));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("https://api.example.com", entry.Value);
        Assert.DoesNotContain("/Tests/", entry.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScanAsync_IncludeTestsTrue_IncludesTestPaths()
    {
        // includeTests=true: Beide Dateien (Production + Test) liefern Funde.
        var productionSource = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com"";
}";
        var testSource = @"
namespace Test;
public sealed class Bar
{
    public const string Url = ""https://api.test.com"";
}";
        using var testSolution = FindMagicValuesTestHelpers.CreateSolution(
            ("src/Production/Foo.cs", productionSource),
            ("tests/FastTests/Bar.cs", testSource));

        var result = await FindMagicValuesTestHelpers.RunAsync(testSolution.Solution, options: new FindMagicValuesRunOptions(IncludeTests: true));

        Assert.Equal(2, result.Payload!.MagicValues.Count);
        Assert.Contains(result.Payload!.MagicValues, e => e.Value == "https://api.example.com");
        Assert.Contains(result.Payload!.MagicValues, e => e.Value == "https://api.test.com");
    }

    [Fact]
    public async Task ScanAsync_ChangedOnlyTrue_LimitsToChangedFiles()
    {
        // changedOnly=true mit leerem Git-Output (kein Git-Repo im Solution-Verzeichnis
        // oder keine uncommitteten Diffs) liefert 0 Dateien im Scope.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), options: new FindMagicValuesRunOptions(ChangedOnly: true));

        // Kein Git-Repo, also 0 Funde â€” gewuenschte Semantik.
        Assert.False(result.IsMalfunction);
    }

    [Fact]
    public async Task ScanAsync_ChangedOnlyFalse_ScansAllFiles()
    {
        // changedOnly=false (Default) ignoriert den Git-Diff-Filter â€” alle Dateien.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), options: new FindMagicValuesRunOptions(ChangedOnly: false));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("https://api.example.com", entry.Value);
    }
}

