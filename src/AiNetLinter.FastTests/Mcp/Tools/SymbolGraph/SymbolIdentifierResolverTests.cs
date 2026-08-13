#nullable enable

using AiNetLinter.Mcp.Tools.SymbolGraph;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

/// <summary>
/// Reine Parsing-Tests fuer <see cref="SymbolIdentifierResolver.TryParsePosition"/> und
/// <see cref="SymbolIdentifierResolver.TryParseLineOnlyPosition"/> — keine Solution/Fixture
/// noetig, da beide Methoden nur Strings segmentieren.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SymbolIdentifierResolverTests
{
    [Fact]
    public void TryParsePosition_FileLineColumn_ReturnsTrueWithParsedSegments()
    {
        var ok = SymbolIdentifierResolver.TryParsePosition("src/Foo.cs:42:10", out var path, out var line, out var column);

        Assert.True(ok);
        Assert.Equal("src/Foo.cs", path);
        Assert.Equal(42, line);
        Assert.Equal(10, column);
    }

    [Fact]
    public void TryParsePosition_FileLineOnly_ReturnsFalse()
    {
        var ok = SymbolIdentifierResolver.TryParsePosition("src/Foo.cs:42", out _, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParsePosition_WindowsDriveLetterPathWithColumn_ReturnsTrueAndReassemblesDriveLetter()
    {
        // Regression: ein Laufwerksbuchstabe erzeugt beim Split durch ':' ein zusaetzliches
        // Segment ("C", "\Foo.cs", "91", "5") — die letzten zwei Segmente sind trotzdem Zeile/
        // Spalte, der Rest (inkl. ':') wird als Pfad wieder zusammengesetzt.
        var ok = SymbolIdentifierResolver.TryParsePosition("C:\\Foo.cs:91:5", out var path, out var line, out var column);

        Assert.True(ok);
        Assert.Equal("C:\\Foo.cs", path);
        Assert.Equal(91, line);
        Assert.Equal(5, column);
    }

    [Fact]
    public void TryParsePosition_WindowsDriveLetterPathWithLineOnly_ReturnsFalse()
    {
        // "C:\Datei.cs:91" hat nach Split durch ':' drei Segmente ("C", "\Datei.cs", "91") — das
        // vorletzte Segment ("\Datei.cs") ist keine Ganzzahl, TryParsePosition lehnt korrekt ab.
        var ok = SymbolIdentifierResolver.TryParsePosition("C:\\Datei.cs:91", out _, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseLineOnlyPosition_FileLine_ReturnsTrueWithParsedSegments()
    {
        var ok = SymbolIdentifierResolver.TryParseLineOnlyPosition("src/Foo.cs:42", out var path, out var line);

        Assert.True(ok);
        Assert.Equal("src/Foo.cs", path);
        Assert.Equal(42, line);
    }

    [Fact]
    public void TryParseLineOnlyPosition_FileLineColumn_JoinsLeadingSegmentsAsPath()
    {
        // TryParseLineOnlyPosition wird nur aufgerufen, wenn TryParsePosition bereits
        // fehlgeschlagen ist (Aufrufer-Reihenfolge in FindReferencesTool) — isoliert betrachtet
        // parst die Methode aber immer "von hinten": letztes Segment = Zeile, Rest = Pfad
        // (inkl. enthaltener ':'). Dokumentiert diesen Mechanismus explizit.
        var ok = SymbolIdentifierResolver.TryParseLineOnlyPosition("src/Foo.cs:42:10", out var path, out var line);

        Assert.True(ok);
        Assert.Equal("src/Foo.cs:42", path);
        Assert.Equal(10, line);
    }

    [Fact]
    public void TryParseLineOnlyPosition_WindowsDriveLetterPathWithLineOnly_ReconstructsDriveLetterPath()
    {
        // Kernfall der Format-Ambiguitaet: "C:\Datei.cs:91" hat nach Split durch ':' drei
        // Segmente ("C", "\Datei.cs", "91"). Die 2-Segment-Beschraenkung aus einer frueheren
        // Fassung haette das faelschlich abgelehnt — auf einem reinen Windows-Projekt sind
        // absolute Laufwerksbuchstaben-Pfade der Normalfall, kein Sonderfall. Von hinten geparst
        // (letztes Segment = Zeile) wird der Laufwerksbuchstabe korrekt wieder Teil des Pfads.
        var ok = SymbolIdentifierResolver.TryParseLineOnlyPosition("C:\\Datei.cs:91", out var path, out var line);

        Assert.True(ok);
        Assert.Equal("C:\\Datei.cs", path);
        Assert.Equal(91, line);
    }

    [Fact]
    public void TryParseLineOnlyPosition_QualifiedNameWithoutColon_ReturnsFalse()
    {
        var ok = SymbolIdentifierResolver.TryParseLineOnlyPosition("Namespace.Klasse.Methode", out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseLineOnlyPosition_NonNumericLastSegment_ReturnsFalse()
    {
        var ok = SymbolIdentifierResolver.TryParseLineOnlyPosition("Klasse.Methode:xyz", out _, out _);

        Assert.False(ok);
    }
}
