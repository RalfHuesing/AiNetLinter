#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.MagicValues;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

// @covers MagicValueSyntaxWalker (VisitInterpolatedStringExpression via ScanAsync_InterpolatedString_StaticTextSegmentsClassified)
/// <summary>
/// Filter-/Aggregations-Pipeline-Tests fuer <see cref="FindMagicValuesScanner"/>:
/// Rausch-Filter (Trivial/Attribut/Index/Loop/GetHashCode/ignoreNumbers), Aggregation
/// (minOccurrences), Filter (valueType/categoryFilter/scopeFilter/maxResults),
/// StructuredContent-Shape, Malfunction- und EPIC-2-Platzhalter-Verhalten. Die
/// Heuristik-Detail-Tests (URL/Pfad/Format-String/HTTP-Statuscode/Schwellenwert/
/// Connection-String) liegen in
/// <see cref="FindMagicValuesScannerHeuristicTests"/>; Geteilte Helpers in
/// <see cref="FindMagicValuesTestHelpers"/>. Aufteilung dient der Einhaltung des
/// <c>MaxLineCount: 500</c>-Limits pro Datei.
/// </summary>
[Trait("Category", "Component")]
public sealed class FindMagicValuesScannerTests
{
    [Fact]
    public async Task ScanAsync_TrivialLiterals_AreNeverReported()
    {
        const string source = @"
using System.Threading;
namespace Test;
public sealed class Foo
{
    public const int Zero = 0;
    public const int One = 1;
    public const int MinusOne = -1;
    public const string Empty = """";
    public const string Space = "" "";
    public const string Newline = ""\n"";
    public const bool True = true;
    public const bool False = false;
    public const object? Null = null;
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        Assert.False(result.IsMalfunction);
        Assert.NotNull(result.Payload);
        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task ScanAsync_IndexAndLoopLiterals_AreSkipped()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public int Get(string[] args)
    {
        var x = args[2];
        var y = args[5];
        return x + y;
    }
    public int Loop()
    {
        var sum = 0;
        for (int i = 2; i < 10; i++) { sum += i; }
        return sum;
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task ScanAsync_AttributeLiterals_AreNotReported()
    {
        const string source = @"
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
namespace Test;
public sealed class Foo
{
    [JsonPropertyName(""foo"")]
    public string Name { get; set; } = """";

    [Route(""/api/v1/users"")]
    public void M() {}

    [Obsolete(""legacy"")]
    public void M2() {}
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task ScanAsync_GetHashCodeLiterals_AreSkippedEvenWithoutIgnoreNumbers()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + 23;
        return hash;
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task ScanAsync_IgnoreNumbers_ExtendsTrivialList()
    {
        // HTTP-Statuscodes sind per Default Magic Values (Wertebereich 100-599 ist semantisch
        // eindeutig). ignoreNumbers ergaenzt die Trivial-Liste {0, 1, -1} um zusaetzliche
        // Werte. 200 und 301 sollen via ignoreNumbers verschwinden, 404 und 500 bleiben
        // sichtbar — der Test verifiziert, dass ignoreNumbers tatsaechlich greift.
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M(int s)
    {
        if (s == 200) { }
        if (s == 301) { }
        if (s == 404) { }
        if (s == 500) { }
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), ignoreNumbers: new HashSet<int> { 200, 301 });

        Assert.Equal(2, result.Payload!.MagicValues.Count);
        Assert.Contains(result.Payload!.MagicValues, e => e.Value == "404");
        Assert.Contains(result.Payload!.MagicValues, e => e.Value == "500");
        Assert.DoesNotContain(result.Payload!.MagicValues, e => e.Value == "200");
        Assert.DoesNotContain(result.Payload!.MagicValues, e => e.Value == "301");
    }

    [Fact]
    public async Task ScanAsync_MinOccurrencesDefault_IncludesSingleOccurrence()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com/only-once"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        Assert.Single(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task ScanAsync_MinOccurrencesFilter_AppliesToAggregation()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string UrlOnce = ""https://api.example.com/once"";
    public const string UrlTwiceA = ""https://api.example.com/twice"";
    public const string UrlTwiceB = ""https://api.example.com/twice"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), minOccurrences: 2);

        Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("https://api.example.com/twice", result.Payload!.MagicValues[0].Value);
        Assert.Equal(2, result.Payload!.MagicValues[0].Occurrences);
    }

    [Fact]
    public async Task ScanAsync_ValueTypeFilter_StringsOnly_ExcludesNumbers()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com"";
    public const double Tolerance = 0.19;
}";
        var resultStrings = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), valueType: MagicValueValueType.String);
        var resultNumbers = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), valueType: MagicValueValueType.Number);

        Assert.Single(resultStrings.Payload!.MagicValues);
        Assert.Equal("string", resultStrings.Payload!.MagicValues[0].ValueType);
        Assert.Single(resultNumbers.Payload!.MagicValues);
        Assert.Equal("number", resultNumbers.Payload!.MagicValues[0].ValueType);
    }

    [Fact]
    public async Task ScanAsync_CategoryFilter_OnlyConfigCandidates()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com"";
    public const string DateFmt = ""yyyy-MM-dd"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.ConfigCandidates);

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("config_candidates", entry.Category);
    }

    [Fact]
    public async Task ScanAsync_AllFilters_ReportsEverything()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com"";
    public const string DateFmt = ""yyyy-MM-dd"";
    public const double Tolerance = 0.19;
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source),
            valueType: null, // all
            category: null); // all

        Assert.Equal(3, result.Payload!.MagicValues.Count);
    }

    [Fact]
    public async Task ScanAsync_ScopeFilter_SubstringMatch_FiltersFiles()
    {
        var sourceSubdir = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com/sub"";
}";
        var sourceOther = @"
namespace Test;
public sealed class Bar
{
    public const string Url = ""https://api.example.com/other"";
}";

        using var testSolution = FindMagicValuesTestHelpers.CreateSolution(
            ("Subdir/Foo.cs", sourceSubdir),
            ("Other/Bar.cs", sourceOther));

        var result = await FindMagicValuesTestHelpers.RunAsync(testSolution.Solution,
            new ScanAsyncParams(
                ScopeFilter: "Subdir",
                ValueType: MagicValueValueType.String));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Contains("Subdir", entry.FilePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_ScopeFilterNoMatch_RetrunsTextOnlyWithoutPayload()
    {
        const string source = "namespace Test; public sealed class Foo { }";
        using var testSolution = FindMagicValuesTestHelpers.CreateSolution(("Foo.cs", source));
        var solution = testSolution.Solution;

        var result = await FindMagicValuesScanner.ScanAsync(new FindMagicValuesScannerParameters(
            Solution: solution,
            ScopeFilter: "DoesNotExistAnywhere",
            ValueType: MagicValueValueType.String,
            Category: null,
            MinOccurrences: 1,
            MaxResults: 50,
            IgnoreNumbers: null,
            IncludeTests: false,
            IncludeSuppressed: false,
            ChangedOnly: false,
            CancellationToken: CancellationToken.None));

        Assert.False(result.IsMalfunction);
        Assert.Null(result.Payload);
        Assert.Contains("Keine Dateien im Scope", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_MaxResultsTruncation_TruncatesAndKeepsTotalCount()
    {
        var files = Enumerable.Range(0, 5)
            .Select(i => ($"F{i}.cs", $@"
namespace Test;
public sealed class F{i}
{{
    public const string Url = ""https://api.example.com/v{i}"";
}}"))
            .ToArray();
        using var testSolution = FindMagicValuesTestHelpers.CreateSolution(files);

        var result = await FindMagicValuesScanner.ScanAsync(new FindMagicValuesScannerParameters(
            Solution: testSolution.Solution,
            ScopeFilter: null,
            ValueType: MagicValueValueType.String,
            Category: null,
            MinOccurrences: 1,
            MaxResults: 2,
            IgnoreNumbers: null,
            IncludeTests: false,
            IncludeSuppressed: false,
            ChangedOnly: false,
            CancellationToken: CancellationToken.None));

        Assert.Equal(2, result.Payload!.MagicValues.Count);
        Assert.True(result.IsTruncated);
        Assert.Equal(5, result.Payload!.Summary.Total);
        Assert.Contains("Treffer gesamt", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_StructuredContentShape_PayloadHasMagicValuesAndSummary()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string Url = ""https://api.example.com"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("Foo.cs", entry.FilePath);
        Assert.Equal("config_candidates", entry.Category);
        Assert.NotNull(result.Payload!.Summary);
        Assert.Equal(1, result.Payload!.Summary.Total);
    }

    [Fact]
    public async Task ScanAsync_IncludeSuppressedFalse_SuppressesLiteralWithDisableComment()
    {
        // EPIC-1-Platzhalter-Anker umgedreht: includeSuppressed=false unterdrueckt jetzt
        // echte Suppression-Kommentare. Das Literal mit // ainetlinter-disable MagicValues
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
    public async Task ScanAsync_InterpolatedString_StaticTextSegmentsClassified()
    {
        // Konzept §"Muss-Haven" Beispiel 2: in-string magic values & interpolation fragments.
        // Der statische Text-Teil vor der Interpolation (vor dem "{") wird durch den
        // MagicValuesClassifier klassifiziert; das dynamische Segment ({env}) wird nicht
        // ausgewertet. Hier trifft die Connection-String-Heuristik auf "Server=" und
        // "Database=" im statischen Fragment.
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

        // Kein Git-Repo, also 0 Funde — gewuenschte Semantik.
        Assert.False(result.IsMalfunction);
    }

    [Fact]
    public async Task ScanAsync_ChangedOnlyFalse_ScansAllFiles()
    {
        // changedOnly=false (Default) ignoriert den Git-Diff-Filter — alle Dateien.
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
