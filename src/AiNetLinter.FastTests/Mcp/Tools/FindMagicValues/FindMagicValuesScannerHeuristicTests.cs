#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.MagicValues;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FindMagicValues;

/// <summary>
/// Basis-Heuristik-Tests fuer <see cref="FindMagicValuesScanner"/> â€” prueft die einzelnen
/// Literal-Klassifizierungs-Heuristiken (URL, Windows-Pfad, Format-String, HTTP-Statuscode,
/// Schwellenwert-Doppel, Connection-String, NonHttpStatus) und die interpolierten
/// String-Segmente jeweils isoliert auf kleinen Quell-Texten. Die erweiterten Heuristiken
/// (<c>nameof</c>/<c>security</c>/<c>standard</c>/<c>duplicate const</c>/<c>enum</c>/
/// <c>localization</c>) liegen in <see cref="FindMagicValuesScannerAdvancedHeuristicTests"/>;
/// Arg-Aktivierungen in <see cref="FindMagicValuesScannerArgTests"/>. Aufteilung dient der
/// Einhaltung des <c>MaxPublicMembersPerType: 15</c>-Limits pro Test-Klasse. Geteilte
/// Helpers ueber <see cref="FindMagicValuesTestHelpers"/>.
/// </summary>
[Trait("Category", "Component")]
public sealed class FindMagicValuesScannerHeuristicTests
{
    [Fact]
    public async Task ScanAsync_UrlLiteral_ReportedAsConfigCandidate()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string ApiBaseUrl = ""https://api.example.com/v1"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("config_candidates", entry.Category);
        Assert.Equal("https://api.example.com/v1", entry.Value);
        Assert.Contains("appsettings.json", entry.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_WindowsPathLiteral_ReportedAsConfigCandidate()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string DataDir = @""C:\Data\Production\Input"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("config_candidates", entry.Category);
        Assert.Equal(@"C:\Data\Production\Input", entry.Value);
    }

    [Fact]
    public async Task ScanAsync_FormatString_ReportedAsConstantCandidate()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string DateFormat = ""yyyy-MM-dd"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("constant_candidates", entry.Category);
        Assert.Equal("yyyy-MM-dd", entry.Value);
    }

    [Fact]
    public async Task ScanAsync_HttpStatusCode_ReportedAsStandardCandidate()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M(int status)
    {
        if (status == 404) { }
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("standard_candidates", entry.Category);
        Assert.Equal("404", entry.Value);
        Assert.Contains("Status404", entry.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_ConstantDoubleThreshold_ReportedAsConstantCandidate()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    private const double Tolerance = 0.19;
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("constant_candidates", entry.Category);
        Assert.Equal("0.19", entry.Value);
    }

    [Fact]
    public async Task ScanAsync_ConnectionStringLiteral_ReportedAsConfigCandidate()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string ConnString = ""Server=prod;Database=mydb;Trusted_Connection=True;"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("config_candidates", entry.Category);
        Assert.Contains("ConnectionString", entry.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_NonHttpStatusCodeNumber_NotReportedAsStandard()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M(int status)
    {
        if (status == 7) { }
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), valueType: MagicValueValueType.Number);

        // 7 ist kein HTTP-Statuscode â€” keine Meldung.
        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task ScanAsync_InterpolatedString_StaticTextSegmentsClassified()
    {
        // Konzept Â§"Muss-Haven" Beispiel 2: in-string magic values & interpolation fragments.
        // Der statische Text-Teil vor der Interpolation (vor dem "{") wird durch den
        // MagicValuesClassifier klassifiziert; das dynamische Segment ({env}) wird nicht
        // ausgewertet. Hier trifft die Connection-String-Heuristik auf "Server=" und
        // "Database=" im statischen Fragment. Verifiziert zusaetzlich, dass synthetische
        // Literal-Knoten (Parent == null) die Heuristik-Pipeline defensiv durchlaufen.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public string M(int env) => $""Server=prod;Database=mydb; for env {env}"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("config_candidates", entry.Category);
        Assert.Equal("Server=prod;Database=mydb; for env ", entry.Value);
        Assert.Contains("Server=prod;Database=mydb", entry.Value, StringComparison.Ordinal);
    }
}

