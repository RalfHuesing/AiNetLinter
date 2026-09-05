#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record FindReferencesRequest(
    string? SymbolIdentifier,
    int MaxResults,
    int Depth,
    string? Symbol = null)
{
    public string? EffectiveSymbolIdentifier =>
        !string.IsNullOrWhiteSpace(SymbolIdentifier) ? SymbolIdentifier : Symbol;
}

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
    /// Symbol auf und liefert dessen Aufrufstellen als Text. Ein defensiver try/catch-Wrapper
    /// faengt unerwartete Roslyn-Exceptions ab und liefert einen strukturierten [ERROR]-Antwort
    /// statt eines Server-Crashs (Defensiv-Pfad).
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        string? symbolIdentifier,
        int maxResults,
        int depth,
        CancellationToken ct) =>
        await ExecuteAsync(
            state,
            new FindReferencesRequest(symbolIdentifier, maxResults, depth),
            ct).ConfigureAwait(false);

    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        FindReferencesRequest request,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var symbolIdentifier = request.EffectiveSymbolIdentifier;
        if (string.IsNullOrEmpty(symbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' (oder 'symbol') fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifierHint);
        }

        try
        {
            var (symbol, error) = await ResolveSymbolAsync(
                solution,
                symbolIdentifier,
                ct,
                state.AssemblySymbolIdentity);
            if (error is not null) return error;

            var normalizedMaxResults = request.MaxResults < 1 ? 1 : request.MaxResults;
            var traversal = await CallGraphTraversal.ExpandAsync(
                new ReferenceTraversalRequest(
                    solution,
                    symbol!,
                    request.Depth,
                    normalizedMaxResults,
                    ct,
                    AssemblySymbolIdentity: state.AssemblySymbolIdentity));
            var formatted = TransitiveCallGraphFormatter.FormatResponse(
                traversal,
                traversal.Completeness.TotalCallSiteCount == 0
                    ? $"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'"
                    : null);

            var finalBody = TransitiveCallGraphFormatter.IsComplete(formatted.Traversal)
                ? McpSufficiencyHints.Append(formatted.Text)
                : formatted.Text;
            return McpToolResults.Text(finalBody, formatted.Traversal);
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
        var cleaned = McpInputNormalizer.StripEnclosingQuotesAndBackticks(identifier);
        var (symbol, error) = await ResolveSymbolCoreAsync(solution, cleaned, ct, assemblyIdentity);
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
            return await ResolveByLineAsync(solution, identifier, linePath, lineOnly, assemblyIdentity, ct);
        }

        return await ResolveByNameAsync(solution, identifier, assemblyIdentity, ct);
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
        Solution solution,
        string identifier,
        string path,
        int line,
        AnalysisSymbolIdentity? assemblyIdentity,
        CancellationToken ct)
    {
        var (root, text, semanticModel, error) = await ResolveDocumentForLineAsync(
            solution,
            identifier,
            path,
            line,
            column: null,
            ct);
        if (error is not null) return (null, error);

        var lineSpan = text!.Lines[line - 1].Span;
        var symbols = SymbolIdentifierResolver.ResolveSymbolsOnLine(root!, lineSpan, semanticModel!);

        if (symbols.Count == 0) return (null, McpToolResults.SymbolNotFound(identifier));
        if (symbols.Count == 1) return (symbols[0], null);

        var declarationsOnLine = symbols
            .Where(s => s.DeclaringSyntaxReferences.Any(r =>
                r.SyntaxTree == root!.SyntaxTree && lineSpan.Contains(r.Span.Start)))
            .ToList();

        var memberDeclarations = declarationsOnLine
            .Where(s => s is IMethodSymbol or IPropertySymbol or INamedTypeSymbol or IFieldSymbol or IEventSymbol)
            .ToList();

        if (memberDeclarations.Count == 1) return (memberDeclarations[0], null);
        if (declarationsOnLine.Count == 1) return (declarationsOnLine[0], null);

        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var lines = symbols.SelectMany(s => FindSymbolTool.FormatSymbolLocations(s, outputRoot, assemblyIdentity));
        return (null, McpToolResults.AmbiguousSymbol(identifier, lines));
    }

    private static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveByNameAsync(
        Solution solution,
        string identifier,
        AnalysisSymbolIdentity? assemblyIdentity,
        CancellationToken ct)
    {
        var unparameterized = SymbolIdentifierResolver.StripParameterList(identifier);
        var ungenericIdentifier = SymbolIdentifierResolver.StripGenerics(unparameterized);
        var lastSegment = ungenericIdentifier.Split('.')[^1];

        var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
            solution, name => name == lastSegment, SymbolFilter.TypeAndMember, ct).ConfigureAwait(false);

        var candidates = symbols
            .Where(s => IsSymbolMatch(s, identifier, unparameterized, ungenericIdentifier))
            .ToList();

        if (candidates.Count == 1) return (candidates[0], null);
        if (candidates.Count > 1)
        {
            var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
            var lines = candidates.SelectMany(s => FindSymbolTool.FormatSymbolLocations(s, outputRoot, assemblyIdentity));
            return (null, McpToolResults.AmbiguousSymbol(identifier, lines));
        }

        var metadataCandidate = await TryResolveMetadataTypeAsync(solution, unparameterized, ungenericIdentifier, ct).ConfigureAwait(false);
        if (metadataCandidate is not null) return (metadataCandidate, null);

        return (null, McpToolResults.SymbolNotFound(identifier));
    }

    private static bool IsSymbolMatch(
        ISymbol symbol,
        string rawIdentifier,
        string unparameterized,
        string ungenericIdentifier)
    {
        var display = SymbolIdentifierResolver.StripParameterList(symbol.ToDisplayString());
        if (display.EndsWith(rawIdentifier, StringComparison.Ordinal)
            || display.EndsWith(unparameterized, StringComparison.Ordinal))
        {
            return true;
        }

        var ungenericDisplay = SymbolIdentifierResolver.StripGenerics(display);
        return ungenericDisplay.EndsWith(ungenericIdentifier, StringComparison.Ordinal);
    }

    private static async Task<INamedTypeSymbol?> TryResolveMetadataTypeAsync(
        Solution solution,
        string unparameterized,
        string ungenericIdentifier,
        CancellationToken ct)
    {
        var normalized = ungenericIdentifier.Trim().Replace("global::", string.Empty, StringComparison.Ordinal);
        var isQualified = normalized.Contains('.');
        var bracketIndex = unparameterized.IndexOf('<');
        var arity = bracketIndex >= 0 ? unparameterized.Count(c => c == ',') + 1 : 0;

        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
            if (compilation is null) continue;

            var resolved = ResolveMetadataTypeFromCompilation(compilation, normalized, isQualified, arity, ct);
            if (resolved is not null) return resolved;
        }

        return null;
    }

    private static INamedTypeSymbol? ResolveMetadataTypeFromCompilation(
        Compilation compilation,
        string normalizedName,
        bool isQualified,
        int arity,
        CancellationToken ct) =>
        isQualified
            ? ResolveQualifiedMetadataType(compilation, normalizedName, arity, ct)
            : ResolveUnqualifiedMetadataType(compilation, normalizedName, arity, ct);

    private static INamedTypeSymbol? ResolveQualifiedMetadataType(
        Compilation compilation,
        string normalizedName,
        int arity,
        CancellationToken ct)
    {
        var direct = compilation.GetTypeByMetadataName(normalizedName)
            ?? (arity > 0 ? compilation.GetTypeByMetadataName($"{normalizedName}`{arity}") : null);
        if (direct is not null) return direct;

        foreach (var metadataRef in compilation.References)
        {
            ct.ThrowIfCancellationRequested();
            if (compilation.GetAssemblyOrModuleSymbol(metadataRef) is not IAssemblySymbol asm) continue;

            var refType = asm.GetTypeByMetadataName(normalizedName)
                ?? (arity > 0 ? asm.GetTypeByMetadataName($"{normalizedName}`{arity}") : null);
            if (refType is not null) return refType;
        }

        return null;
    }

    private static INamedTypeSymbol? ResolveUnqualifiedMetadataType(
        Compilation compilation,
        string normalizedName,
        int arity,
        CancellationToken ct)
    {
        foreach (var metadataRef in compilation.References)
        {
            ct.ThrowIfCancellationRequested();
            if (compilation.GetAssemblyOrModuleSymbol(metadataRef) is not IAssemblySymbol asm) continue;

            var refType = FindMetadataTypeInNamespace(asm.GlobalNamespace, normalizedName, arity, ct);
            if (refType is not null) return refType;
        }

        return FindMetadataTypeInNamespace(compilation.Assembly.GlobalNamespace, normalizedName, arity, ct);
    }

    private static INamedTypeSymbol? FindMetadataTypeInNamespace(
        INamespaceSymbol ns,
        string simpleName,
        int arity,
        CancellationToken ct)
    {
        foreach (var member in ns.GetTypeMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (string.Equals(member.Name, simpleName, StringComparison.OrdinalIgnoreCase)
                && (arity == 0 || member.Arity == arity))
            {
                return member;
            }

            var nested = FindNestedMetadataType(member, simpleName, arity, ct);
            if (nested is not null) return nested;
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            ct.ThrowIfCancellationRequested();
            var match = FindMetadataTypeInNamespace(child, simpleName, arity, ct);
            if (match is not null) return match;
        }

        return null;
    }

    private static INamedTypeSymbol? FindNestedMetadataType(
        INamedTypeSymbol parent,
        string simpleName,
        int arity,
        CancellationToken ct)
    {
        foreach (var member in parent.GetTypeMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (string.Equals(member.Name, simpleName, StringComparison.OrdinalIgnoreCase)
                && (arity == 0 || member.Arity == arity))
            {
                return member;
            }
        }

        return null;
    }
}
