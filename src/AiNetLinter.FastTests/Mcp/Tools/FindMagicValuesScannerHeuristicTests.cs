#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.MagicValues;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

/// <summary>
/// Heuristik-Detail-Tests fuer <see cref="FindMagicValuesScanner"/> — prueft die einzelnen
/// Klassifizierungs-Heuristiken (URL, Windows-Pfad, Format-String, HTTP-Statuscode,
/// Schwellenwert-Doppel, Connection-String) jeweils isoliert auf kleinen Quell-Texten.
/// Aufteilung in eine eigene Datei, damit die Haupt-Testklasse
/// <see cref="FindMagicValuesScannerTests"/> unter dem <c>MaxLineCount: 500</c>-Limit
/// bleibt (siehe <c>AiNetLinter.mdc</c>). Beide Klassen teilen die Helpers ueber
/// <see cref="FindMagicValuesTestHelpers"/>.
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

        // 7 ist kein HTTP-Statuscode — keine Meldung.
        Assert.Empty(result.Payload!.MagicValues);
    }
}
