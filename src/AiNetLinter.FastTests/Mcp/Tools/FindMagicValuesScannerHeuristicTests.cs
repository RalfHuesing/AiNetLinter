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

    [Fact]
    public async Task Classify_NameofCandidate_StringMatchesParameterName()
    {
        // String-Literal "foo" entspricht dem Parameter-Namen foo — sollte als
        // nameof_candidates klassifiziert werden mit Empfehlung nameof(foo).
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M(string foo)
    {
        throw new ArgumentNullException(""foo"");
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.NameofCandidates);

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("nameof_candidates", entry.Category);
        Assert.Equal("foo", entry.Value);
        Assert.Equal("nameof(foo)", entry.Recommendation);
    }

    [Fact]
    public async Task Classify_NameofCandidate_StringDoesNotMatchAnySymbol_IsNotMagic()
    {
        // String-Literal "bar" matcht KEINEN Parameter-Namen im Scope — kein Fund.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M(string foo)
    {
        throw new ArgumentNullException(""bar"");
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.NameofCandidates);

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task Classify_SecurityCandidate_ParameterNamedPassword()
    {
        // Parameter `password` deutet auf ein Secret; das hartcodierte Praefix-Literal
        // "sk-abc123" sollte als security_candidates klassifiziert werden (Heuristik 1:
        // SecurityPrefixes matcht "sk-").
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M(string password)
    {
        Connect(""sk-abc123"");
    }
    private void Connect(string secret) { }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.SecurityCandidates);

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("security_candidates", entry.Category);
        Assert.Contains("CWE-798", entry.ContextHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Classify_SecurityCandidate_AwsAccessKeyPrefix()
    {
        // "AKIA..." Praefix matcht — security_candidates mit CWE-798-Hinweis.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M()
    {
        var key = ""AKIAIOSFODNN7EXAMPLE"";
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.SecurityCandidates);

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("security_candidates", entry.Category);
        Assert.Equal("AKIAIOSFODNN7EXAMPLE", entry.Value);
        Assert.Contains("AKIA", entry.ContextHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Classify_StandardCandidateExtras_BufferSize1024()
    {
        // 1024 ist eine Well-known Buffer-Groesse, sollte als standard_candidates
        // mit Empfehlung BufferSize klassifiziert werden.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const int BufSize = 1024;
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.StandardCandidates);

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("standard_candidates", entry.Category);
        Assert.Equal("1024", entry.Value);
        Assert.Contains("BufferSize", entry.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Classify_StandardCandidateExtras_TimeConstant1000()
    {
        // 1000 ist MillisecondsPerSecond, sollte als standard_candidates klassifiziert werden.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const int MillisecondsPerSecond = 1000;
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.StandardCandidates);

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("standard_candidates", entry.Category);
        Assert.Equal("1000", entry.Value);
        Assert.Contains("MillisecondsPerSecond", entry.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Classify_DuplicateConstFields_TwoClassesSameValue()
    {
        // Zwei Klassen mit identischem const int Value in verschiedenen Files.
        // Erwartet: 2 Funde (einer pro Datei) mit Hochstufungs-Empfehlung.
        // Bewusst const int statt const double (0.80) gewaehlt, weil der Standard-
        // Schwellenwert-Pfad sonst zusaetzlich einen Fund pro Datei beisteuern wuerde
        // (siehe ScanAsync_ConstantDoubleThreshold_ReportedAsConstantCandidate). Mit
        // int-Wert ist die Standard-Pipeline still, und die Empfehlung kommt sauber
        // aus der Duplikat-Erkennung.
        var source1 = @"
namespace Test.One;
public sealed class A
{
    private const int SharedConstant = 12345;
}";
        var source2 = @"
namespace Test.Two;
public sealed class B
{
    private const int SharedConstant = 12345;
}";
        using var testSolution = FindMagicValuesTestHelpers.CreateSolution(
            ("A.cs", source1),
            ("B.cs", source2));

        var result = await FindMagicValuesTestHelpers.RunAsync(testSolution.Solution, category: MagicValueCategory.ConstantCandidates);

        Assert.Equal(2, result.Payload!.MagicValues.Count);
        Assert.All(result.Payload!.MagicValues, e => Assert.Contains("Hochstufung", e.Recommendation, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Classify_DuplicateConstFields_OnlyOneOccurrence_IsNotReported()
    {
        // Nur ein const-Feld mit Wert 12345 — Schwelle ist ≥ 2 Vorkommen in ≥ 2 Files,
        // also KEIN Duplikat-Fund. (Die Standard-Pipeline liefert ebenfalls keinen Fund,
        // weil 12345 kein HTTP-Statuscode, keine Buffer/Zeit-Konstante und keine
        // Schwellenwert-double/float/decimal ist.)
        const string source = @"
namespace Test;
public sealed class A
{
    private const int SharedConstant = 12345;
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("A.cs", source), category: MagicValueCategory.ConstantCandidates);

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task Classify_EnumCandidates_IfElseCascade()
    {
        // if-else-Kaskade mit ≥ 3 Vergleichen gegen denselben Identifier.
        // Erwartet: 3 Funde mit enum_candidates-Klassifizierung.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M(string status)
    {
        if (status == ""Pending"") { }
        else if (status == ""Active"") { }
        else if (status == ""Failed"") { }
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.EnumCandidates);

        Assert.Equal(3, result.Payload!.MagicValues.Count);
        Assert.All(result.Payload!.MagicValues, e =>
        {
            Assert.Equal("enum_candidates", e.Category);
            Assert.Contains("enum Status", e.Recommendation, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Classify_EnumCandidates_OnlyTwoComparisons_IsNotEnum()
    {
        // Nur 2 Vergleiche — unter der Schwelle (≥ 3), also KEIN Fund.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M(string status)
    {
        if (status == ""Pending"") { }
        else if (status == ""Active"") { }
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.EnumCandidates);

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task Classify_LocalizationCandidate_ExceptionMessageLongerThan15()
    {
        // Exception-Message mit > 15 Zeichen (Whitespace ungleich) als Argument in
        // Exception-Konstruktor — sollte als localization_candidates klassifiziert werden.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M()
    {
        throw new InvalidOperationException(""Connection refused from server"");
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.LocalizationCandidates);

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("localization_candidates", entry.Category);
        Assert.Equal("Connection refused from server", entry.Value);
    }

    [Fact]
    public async Task Classify_LocalizationCandidate_ShortExceptionMessage_IsNotMagic()
    {
        // Kurze Exception-Message (< 15 Zeichen) wird NICHT als localization_candidates
        // gemeldet (Schwelle).
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M()
    {
        throw new InvalidOperationException(""oops"");
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.LocalizationCandidates);

        Assert.Empty(result.Payload!.MagicValues);
    }
}
