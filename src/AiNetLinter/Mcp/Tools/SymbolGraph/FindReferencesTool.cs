#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// MCP-Tool <c>find_references</c>: loest einen Symbol-Identifikator (stabile
/// DocumentationCommentId, Datei:Zeile:Spalte oder qualifizierter/teil-qualifizierter Name) zu
/// genau einem Roslyn-<see cref="ISymbol"/> auf und liefert dessen Aufrufstellen ueber den
/// gemeinsamen strukturierten Traversal-Result-Typ. Deckt nur .cs-Dateien ab
/// (Roslyn-Symbolgraph). Optionaler <c>depth</c>-Parameter (Default 1, hard cap 3) loest
/// transitive Aufrufstellen ueber <see cref="CallGraphTraversal"/> auf; Text und
/// <c>structuredContent</c> werden aus derselben aggregierten Trefferliste erzeugt.
/// </summary>
internal static class FindReferencesTool
{
    /// <summary>
    /// Tool-Einstiegspunkt: prueft, ob eine Solution geladen ist, loest den Identifikator zu einem
    /// Symbol auf und liefert dessen Aufrufstellen als Text. Stellt dem Aufrufstellen-Output
    /// einen Warnhinweis voran, falls die Solution Compile-Fehler in einzelnen Dateien hat
    /// (Roslyn toleriert sie, aber der Agent weiss sonst nicht, dass die Antwort unvollstaendig
    /// sein kann). Defensiver try/catch-Wrapper faengt unerwartete Roslyn-Exceptions ab und
    /// liefert einen strukturierten [ERROR]-Antwort statt eines Server-Crashs (Defensiv-Pfad).
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? symbolIdentifier, int maxResults, int depth, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (string.IsNullOrEmpty(symbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifierHint);
        }

        try
        {
            var (symbol, error) = await ResolveSymbolAsync(solution, symbolIdentifier, ct, state.AssemblySymbolIdentity);
            if (error is not null) return error;

            var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
            var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;
            var traversal = await CallGraphTraversal.ExpandAsync(
                new ReferenceTraversalRequest(
                    solution,
                    symbol!,
                    depth,
                    normalizedMaxResults,
                    ct,
                    AssemblySymbolIdentity: state.AssemblySymbolIdentity));
            var body = TransitiveCallGraphFormatter.Format(traversal);
            if (traversal.Completeness.TotalCallSiteCount == 0)
            {
                body = $"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'";
            }

            var finalBody = TransitiveCallGraphFormatter.IsComplete(traversal)
                ? McpSufficiencyHints.Append(body)
                : body;
            var finalText = FindSymbolTool.PrependWarning(warning, finalBody);
            return McpToolResults.Text(finalText, traversal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in find_references: {ex.Message}",
                context: symbolIdentifier);
        }
    }

    /// <summary>
    /// Loest <paramref name="identifier"/> ueber eine stabile DocumentationCommentId, eine
    /// Datei:Zeile:Spalte-Angabe oder einen qualifizierten/teil-qualifizierten Namen zu genau
    /// einem Symbol auf — gemeinsamer Einstiegspunkt fuer alle drei dokumentierten Formate. Reine
    /// Funktion (Solution rein, Symbol/Fehler raus) ohne Abhaengigkeit von
    /// <see cref="McpCodeGraphServer"/> — direkt unit-testbar. Normalisiert Accessor-Symbole
    /// (Property/Event) auf den zugrunde liegenden Owner, damit eine Position auf einem
    /// <c>get</c>/<c>set</c>/<c>add</c>/<c>remove</c>-Keyword konsistent dieselbe ID liefert
    /// wie eine Position auf dem Property-/Event-Namen.
    /// </summary>
    internal static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveSymbolAsync(
        Solution solution,
        string identifier,
        CancellationToken ct,
        AnalysisSymbolIdentity? assemblyIdentity = null)
    {
        var (symbol, error) = await ResolveSymbolCoreAsync(solution, identifier, ct, assemblyIdentity);
        return (symbol.NormalizeToOwningMember(), error);
    }

    private static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveSymbolCoreAsync(
        Solution solution,
        string identifier,
        CancellationToken ct,
        AnalysisSymbolIdentity? assemblyIdentity)
    {
        var (stableSymbol, stableError) =
            await SymbolIdentifierResolver.TryResolveByStableIdAsync(solution, identifier, ct, assemblyIdentity);
        if (stableError is not null) return (null, stableError);
        if (stableSymbol is not null) return (stableSymbol, null);

        if (SymbolIdentifierResolver.TryParsePosition(identifier, out var path, out var line, out var column))
        {
            return await ResolveByPositionAsync(solution, identifier, path, line, column, ct);
        }

        if (SymbolIdentifierResolver.TryParseLineOnlyPosition(identifier, out var linePath, out var lineOnly))
        {
            return await ResolveByLineAsync(solution, identifier, linePath, lineOnly, ct);
        }

        return await ResolveByNameAsync(solution, identifier, ct);
    }


    /// <summary>
    /// Loest Dokument, Syntaxbaum, Quelltext und SemanticModel fuer eine Datei:Zeile-Angabe auf
    /// und validiert die Zeilennummer — gemeinsame Vorstufe fuer <see cref="ResolveByPositionAsync"/>
    /// (Datei:Zeile:Spalte) und <see cref="ResolveByLineAsync"/> (Datei:Zeile-Fallback), damit
    /// Dateiauflösung und Zeilen-Validierung nicht dupliziert werden.
    /// </summary>
    private static async Task<(SyntaxNode? Root, SourceText? Text, SemanticModel? SemanticModel, CallToolResult? Error)>
        ResolveDocumentForLineAsync(
            Solution solution,
            string identifier,
            string path,
            int line,
            int? column,
            CancellationToken ct)
    {
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, path);
        if (document is null) return (null, null, null, McpToolResults.SymbolNotFound(identifier));

        var text = await document.GetTextAsync(ct);
        if (text is null)
        {
            return (null, null, null, McpToolResults.CompilationError(
                $"Quelltext für '{path}' konnte nicht gelesen werden.",
                context: identifier));
        }

        if (line < 1 || line > text.Lines.Count)
        {
            return (
                null,
                null,
                null,
                InvalidPosition(
                    identifier,
                    $"Ungültige Zeile {line}; der gültige Bereich ist 1 bis {text.Lines.Count}."));
        }

        if (column is { } requestedColumn)
        {
            var lineLength = text.Lines[line - 1].Span.Length;
            if (requestedColumn < 1 || requestedColumn > lineLength)
            {
                var range = lineLength == 0 ? "keine gültige Spalte (leere Zeile)" : $"1 bis {lineLength}";
                return (
                    null,
                    null,
                    null,
                    InvalidPosition(
                        identifier,
                        $"Ungültige Spalte {requestedColumn} in Zeile {line}; der gültige Bereich ist {range}."));
            }
        }

        var root = await document.GetSyntaxRootAsync(ct);
        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (root is null || semanticModel is null)
        {
            return (null, null, null, McpToolResults.CompilationError(
                $"Roslyn konnte das Dokument '{path}' nicht für die Symbolauflösung bereitstellen.",
                context: identifier));
        }

        return (root, text, semanticModel, null);
    }

    private static CallToolResult InvalidPosition(string identifier, string message) =>
        McpToolResults.Recoverable(
            LinterErrorCodes.InvalidArgument,
            message,
            context: identifier,
            hint: "Position im Format Datei:Zeile:Spalte angeben; Zeile und Spalte beginnen bei 1 und müssen innerhalb des Quelltexts liegen.");

    private static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveByPositionAsync(
        Solution solution, string identifier, string path, int line, int column, CancellationToken ct)
    {
        var (root, text, semanticModel, error) = await ResolveDocumentForLineAsync(
            solution,
            identifier,
            path,
            line,
            column,
            ct);
        if (error is not null) return (null, error);

        var position = text!.Lines[line - 1].Start + (column - 1);
        var token = root!.FindToken(position);
        var symbol = SymbolIdentifierResolver.ResolveSymbolAtToken(token, semanticModel!);

        return symbol is null ? (null, McpToolResults.SymbolNotFound(identifier)) : (symbol, null);
    }

    /// <summary>
    /// Loest den Datei:Zeile-Fallback (ohne Spalte) auf: sammelt alle eindeutigen Symbole der
    /// Zeile ueber <see cref="SymbolIdentifierResolver.ResolveSymbolsOnLine"/> und liefert bei
    /// genau einem Treffer das Symbol, sonst <see cref="McpToolResults.AmbiguousSymbol"/> bzw.
    /// <see cref="McpToolResults.SymbolNotFound"/> — analog zu <see cref="ResolveByNameAsync"/>s
    /// Mehrdeutigkeitsbehandlung.
    /// </summary>
    private static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveByLineAsync(
        Solution solution, string identifier, string path, int line, CancellationToken ct)
    {
        var (root, text, semanticModel, error) = await ResolveDocumentForLineAsync(
            solution,
            identifier,
            path,
            line,
            column: null,
            ct);
        if (error is not null) return (null, error);

        var symbols = SymbolIdentifierResolver.ResolveSymbolsOnLine(root!, text!.Lines[line - 1].Span, semanticModel!);

        if (symbols.Count == 0) return (null, McpToolResults.SymbolNotFound(identifier));
        if (symbols.Count == 1) return (symbols[0], null);

        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var lines = symbols.SelectMany(s => FindSymbolTool.FormatSymbolLocations(s, outputRoot));
        return (null, McpToolResults.AmbiguousSymbol(identifier, lines));
    }

    private static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveByNameAsync(
        Solution solution, string identifier, CancellationToken ct)
    {
        var lastSegment = SymbolIdentifierResolver.StripParameterList(identifier).Split('.')[^1];
        var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
            solution, name => name == lastSegment, SymbolFilter.TypeAndMember, ct);

        var candidates = symbols
            .Where(s => SymbolIdentifierResolver.StripParameterList(s.ToDisplayString())
                .EndsWith(identifier, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0) return (null, McpToolResults.SymbolNotFound(identifier));
        if (candidates.Count == 1) return (candidates[0], null);

        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var lines = candidates.SelectMany(s => FindSymbolTool.FormatSymbolLocations(s, outputRoot));
        return (null, McpToolResults.AmbiguousSymbol(identifier, lines));
    }
}
