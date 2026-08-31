#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class FindReferencesTransitivePositionTests
{
    [Fact]
    public async Task ResolveSymbolAsync_LineOnlyIdentifier_MatchesPositionIdentifierOnUniqueLine()
    {
        // OtherCaller.cs Zeile 3 ("public class OtherCaller") enthaelt nur ein einziges
        // quelltext-eigenes Symbol (die Klasse selbst) — Datei:Zeile muss dasselbe Symbol liefern
        // wie die explizite Datei:Zeile:Spalte-Angabe auf den Klassennamen.
        using var context = new McpInMemoryTestContext();
        var lineOnlyIdentifier = $"{SymbolGraphMiniSolutionSpec.OtherCallerPath}:3";
        var positionIdentifier = $"{SymbolGraphMiniSolutionSpec.OtherCallerPath}:3:14";

        var (lineOnlySymbol, lineOnlyError) = await FindReferencesTool.ResolveSymbolAsync(
            context.Solution, lineOnlyIdentifier, CancellationToken.None);
        var (positionSymbol, positionError) = await FindReferencesTool.ResolveSymbolAsync(
            context.Solution, positionIdentifier, CancellationToken.None);

        Assert.Null(lineOnlyError);
        Assert.Null(positionError);
        Assert.NotNull(lineOnlySymbol);
        Assert.True(SymbolEqualityComparer.Default.Equals(lineOnlySymbol, positionSymbol));
        Assert.Equal("OtherCaller", lineOnlySymbol!.Name);
    }

    [Fact]
    public async Task ResolveSymbolAsync_LineOnlyIdentifierWithMultipleSymbols_ReturnsAmbiguousSymbolError()
    {
        // Caller.cs Zeile 8 traegt zwei eigenstaendige quelltext-Symbole: die lokale Variable
        // "greeter" und die Methode "Greet".
        using var context = new McpInMemoryTestContext();
        var identifier = $"{SymbolGraphMiniSolutionSpec.CallerPath}:8";

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            context.Solution, identifier, CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("AMBIGUOUS_SYMBOL", textContent.Text);
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSymbolAsync_LineOnlyIdentifierWithNoSymbolsOnLine_ReturnsSymbolNotFoundError()
    {
        // Caller.cs Zeile 2 ist eine Leerzeile ohne ein aufloesbares Symbol.
        using var context = new McpInMemoryTestContext();
        var identifier = $"{SymbolGraphMiniSolutionSpec.CallerPath}:2";

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            context.Solution, identifier, CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ResolveSymbolAsync_WindowsDriveLetterPathWithLineOnly_ReconstructsFullPathAndReportsMissingFileNotSymbolNotFoundOnWrongPath()
    {
        // Das Datei:Zeile-Fallback parst den Laufwerksbuchstaben von hinten und rekonstruiert
        // korrekt "C:\\Datei.cs" statt faelschlich nur "C".
        using var context = new McpInMemoryTestContext();
        var identifier = "C:\\Datei.cs:91";

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            context.Solution, identifier, CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_GetTypeHierarchyTool_LineOnlyIdentifier_ResolvesTransitively()
    {
        // Belegt, dass der gemeinsame Resolver-Fix transitiv auch fuer get_type_hierarchy wirkt
        // (nicht nur fuer find_references direkt) — Greeter.cs Zeile 3 ist eine eindeutige
        // Klassendeklarationszeile.
        using var context = new McpInMemoryTestContext();
        var state = context.CreateServer();
        var identifier = $"{SymbolGraphMiniSolutionSpec.GreeterPath}:3";

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, identifier, GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        // Waere die Datei:Zeile-Aufloesung fehlgeschlagen oder auf ein Nicht-Typ-Symbol
        // gelaufen, kaeme SYMBOL_NOT_FOUND bzw. INVALID_ARGUMENT statt der Hierarchie-Ausgabe —
        // "Basisklassen:" ist nur im Erfolgsfall (Typ aufgeloest) im Text enthalten.
        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Basisklassen:", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_GetSymbolBodyTool_LineOnlyIdentifier_ResolvesTransitively()
    {
        // Belegt denselben transitiven Effekt fuer get_symbol_body — OtherCaller.cs Zeile 5
        // ("public string Run() => \"other\";") ist eine eindeutige Methodenzeile.
        using var context = new McpInMemoryTestContext();
        var state = context.CreateServer();
        var identifier = $"{SymbolGraphMiniSolutionSpec.OtherCallerPath}:5";

        var result = await GetSymbolBodyTool.ExecuteAsync(state, [identifier], GetSymbolBodyTool.DefaultMaxBodyLines, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Run", textContent.Text, StringComparison.Ordinal);
    }
}
