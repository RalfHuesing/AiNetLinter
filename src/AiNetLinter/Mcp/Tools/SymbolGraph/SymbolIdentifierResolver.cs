#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Kleine, reine Parsing-/Aufloesungs-Helfer fuer <see cref="FindReferencesTool.ResolveSymbolAsync"/>
/// — in eine eigene Datei ausgelagert, damit <see cref="FindReferencesTool"/>s eigener
/// <c>AIContextFootprint</c> (siehe Linter-Regel <c>AIContextFootprint</c>) nicht durch reine Hilfslogik unnoetig
/// waechst, waehrend <see cref="McpCodeGraphServer"/> (Parameter von
/// <see cref="FindReferencesTool.ExecuteAsync"/>) bereits allein einen erheblichen transitiven
/// Anteil beitraegt. Da <see cref="FindReferencesTool.ResolveSymbolAsync"/> der gemeinsame
/// Einstiegspunkt fuer <c>find_references</c>, <c>get_impact</c>, <c>get_type_hierarchy</c> und
/// <c>get_symbol_body</c> ist, gelten diese Helfer transitiv fuer alle vier Tools.
/// </summary>
internal static class SymbolIdentifierResolver
{
    /// <summary>
    /// Ermittelt das Symbol am Positions-Token: bevorzugt die Deklaration (Cursor auf
    /// Methoden-/Typnamen), sonst das an dieser Stelle referenzierte Symbol (Cursor auf
    /// Verwendungsstelle).
    /// </summary>
    internal static ISymbol? ResolveSymbolAtToken(SyntaxToken token, SemanticModel semanticModel)
    {
        var node = token.Parent;
        if (node is null) return null;

        return semanticModel.GetDeclaredSymbol(node) ?? semanticModel.GetSymbolInfo(node).Symbol;
    }

    /// <summary>
    /// Prueft, ob <paramref name="identifier"/> dem Format <c>Datei:Zeile:Spalte</c> entspricht
    /// (letzte zwei ':'-getrennte Segmente sind Ganzzahlen).
    /// </summary>
    internal static bool TryParsePosition(string identifier, out string path, out int line, out int column)
    {
        path = string.Empty;
        line = 0;
        column = 0;

        var segments = identifier.Split(':');
        if (segments.Length < 3) return false;
        if (!int.TryParse(segments[^1], out column)) return false;
        if (!int.TryParse(segments[^2], out line)) return false;

        path = string.Join(":", segments[..^2]);
        return true;
    }

    /// <summary>
    /// Prueft, ob <paramref name="identifier"/> dem Fallback-Format <c>Datei:Zeile</c> (ohne
    /// Spalte) entspricht. Wie <see cref="TryParsePosition"/> von hinten geparst: das letzte
    /// ':'-getrennte Segment muss eine Ganzzahl (Zeile) sein, alle vorangehenden Segmente werden
    /// (inkl. enthaltener ':') wieder zum Pfad zusammengesetzt. Das deckt sowohl relative Pfade
    /// (<c>src/Foo.cs:42</c>, zwei Segmente) als auch absolute Windows-Laufwerksbuchstaben-Pfade
    /// (<c>C:\Foo.cs:42</c>, drei Segmente durch den Doppelpunkt nach dem Laufwerksbuchstaben) ab —
    /// eine Beschraenkung auf exakt zwei Segmente wuerde Laufwerksbuchstaben-Pfade grundsaetzlich
    /// ausschliessen. Nur relevant, wenn <see cref="TryParsePosition"/> bereits fehlgeschlagen ist
    /// (Aufrufer prueft das Datei:Zeile:Spalte-Format zuerst).
    /// </summary>
    internal static bool TryParseLineOnlyPosition(string identifier, out string path, out int line)
    {
        path = string.Empty;
        line = 0;

        var segments = identifier.Split(':');
        if (segments.Length < 2) return false;
        if (!int.TryParse(segments[^1], out line)) return false;

        path = string.Join(":", segments[..^1]);
        return true;
    }

    /// <summary>
    /// Ermittelt alle eindeutigen Symbole, die auf einer Zeile deklariert oder referenziert
    /// werden (Grundlage fuer das <c>Datei:Zeile</c>-Fallback ohne Spalte). Iteriert alle Tokens
    /// der Zeile, loest jedes ueber <see cref="ResolveSymbolAtToken"/> auf und dedupliziert per
    /// <see cref="SymbolEqualityComparer"/>. Beschraenkt auf Symbole mit Quelltext-Fundstelle
    /// (<see cref="Location.IsInSource"/>) — Metadata-/BCL-Symbole (z. B. das <c>string</c>-Schluesselwort
    /// eines Rueckgabetyps) sind fuer eine Zeilen-Aufloesung reines Rauschen und wuerden sonst
    /// jede Zeile mit einem primitiven Typ faelschlich mehrdeutig machen.
    /// </summary>
    internal static List<ISymbol> ResolveSymbolsOnLine(SyntaxNode root, TextSpan lineSpan, SemanticModel semanticModel)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var symbols = new List<ISymbol>();

        foreach (var token in root.DescendantTokens(lineSpan))
        {
            if (!lineSpan.Contains(token.Span)) continue;

            var symbol = ResolveSymbolAtToken(token, semanticModel);
            if (symbol is null || !symbol.Locations.Any(l => l.IsInSource)) continue;
            if (seen.Add(symbol)) symbols.Add(symbol);
        }

        return symbols;
    }

    /// <summary>
    /// Entfernt eine Parameterliste (inkl. Klammern) aus einem <see cref="ISymbol.ToDisplayString()"/>
    /// -Ergebnis, damit Methoden-Identifikatoren ohne Parametertypen verglichen werden koennen.
    /// </summary>
    internal static string StripParameterList(string displayString)
    {
        var parenIndex = displayString.IndexOf('(');
        return parenIndex < 0 ? displayString : displayString[..parenIndex];
    }

    /// <summary>
    /// Entfernt Typparameter/Generics (inkl. Spitzenklammern) aus einem Bezeichner,
    /// z. B. "IPipelineStep<T>" -> "IPipelineStep".
    /// </summary>
    internal static string StripGenerics(string identifier)
    {
        var bracketIndex = identifier.IndexOf('<');
        return bracketIndex < 0 ? identifier : identifier[..bracketIndex].Trim();
    }

    /// <summary>
    /// Loest einen stabilen Symbol-Identifikator (DocumentationCommentId, z. B. <c>M:Ns.Type.Method(System.Int32)</c>)
    /// zu genau einem <see cref="ISymbol"/> auf. Iteriert dazu ueber alle
    /// <see cref="Microsoft.CodeAnalysis.DeclaredSymbolInfo"/>s aller Projekte, weil
    /// <see cref="SymbolFinder"/> keine direkte DocumentationCommentId-Suche anbietet. Wenn
    /// <paramref name="stableId"/> kein gueltiges DocumentationCommentId-Praefix
    /// (<c>M:</c>/<c>T:</c>/<c>P:</c>/<c>F:</c>/<c>E:</c>/<c>!:</c>) traegt, wird der Aufruf
    /// als Fehlschlag gewertet und der Aufrufer kann auf <see cref="FindReferencesTool.ResolveSymbolAsync"/>
    /// (Datei:Zeile:Spalte oder qualifizierter Name) zurueckfallen.
    /// </summary>
    internal static async Task<(ISymbol? Symbol, CallToolResult? Error)> TryResolveByStableIdAsync(
        Solution solution,
        string stableId,
        CancellationToken ct,
        AnalysisSymbolIdentity? expectedAssemblyIdentity = null)
    {
        var preparation = PrepareStableId(stableId, expectedAssemblyIdentity);
        if (preparation.Error is not null || preparation.NormalizedId is null) return (null, preparation.Error);
        stableId = preparation.NormalizedId;

        var assemblyCandidates = preparation.IsAssemblyId ? new List<ISymbol>() : null;
        var exactMatches = await FindExactStableIdAsync(solution, stableId, ct, assemblyCandidates);
        return ResolveExactMatches(exactMatches, assemblyCandidates, stableId, preparation.IsAssemblyId, preparation.IsHandoff);
    }

    private static (string? NormalizedId, bool IsAssemblyId, bool IsHandoff, CallToolResult? Error) PrepareStableId(string stableId, AnalysisSymbolIdentity? expectedIdentity)
    {
        if (string.IsNullOrEmpty(stableId)) return (null, false, false, null);
        if (!TryNormalizeHandoffId(stableId, expectedIdentity, out var normalizedId, out var isAssemblyId, out var isHandoff, out var error)) return (null, false, false, error);
        return HasKnownDocumentationCommentIdPrefix(normalizedId) ? (normalizedId, isAssemblyId, isHandoff, null) : (null, false, false, null);
    }

    private static (ISymbol? Symbol, CallToolResult? Error) ResolveExactMatches(
        IReadOnlyList<ISymbol> exactMatches, ICollection<ISymbol>? assemblyCandidates, string stableId, bool isAssemblyId, bool isHandoff)
    {
        if (exactMatches.Count == 1 || isAssemblyId && exactMatches.Count > 0) return (exactMatches[0], null);
        if (!isAssemblyId && exactMatches.Count > 1) return (null, McpToolResults.AmbiguousSymbol(SymbolHandoffIdentifier.ForError(stableId), FormatStableIdCandidates(exactMatches)));
        if (!isAssemblyId && isHandoff) return (null, McpToolResults.SymbolNotFound(SymbolHandoffIdentifier.ForError(stableId)));
        var matches = assemblyCandidates?.Where(symbol => MatchesAssemblyStableId(symbol, stableId)).Distinct(SymbolEqualityComparer.Default).ToList();
        return matches?.Count == 1 ? (matches[0], null) : (null, null);
    }

    internal static string NormalizeDocCommentId(string id)
    {
        var tilde = id.IndexOf('~');
        return tilde >= 0 ? id[..tilde] : id;
    }

    private static async Task<IReadOnlyList<ISymbol>> FindExactStableIdAsync(
        Solution solution,
        string stableId,
        CancellationToken ct,
        ICollection<ISymbol>? assemblyCandidates)
    {
        var normalizedStableId = NormalizeDocCommentId(stableId);
        var matches = new List<ISymbol>();
        foreach (var project in solution.Projects)
        {
            var declared = await SymbolFinder.FindSourceDeclarationsAsync(
                project, name => true, SymbolFilter.TypeAndMember, ct);
            foreach (var symbol in declared)
            {
                var declarationId = DocumentationCommentId.CreateDeclarationId(symbol);
                if (declarationId is not null)
                {
                    if (declarationId == stableId || NormalizeDocCommentId(declarationId) == normalizedStableId)
                    {
                        matches.Add(symbol);
                        continue;
                    }
                }

                if (assemblyCandidates is not null) assemblyCandidates.Add(symbol);
            }
        }

        return matches.Distinct(SymbolEqualityComparer.Default).ToArray();
    }

    private static IEnumerable<string> FormatStableIdCandidates(IEnumerable<ISymbol> symbols) =>
        symbols
            .SelectMany(symbol => symbol.Locations
                .Where(location => location.IsInSource)
                .Select(location =>
                {
                    var line = location.GetLineSpan().StartLinePosition.Line + 1;
                    return $"{location.SourceTree?.FilePath ?? "unbekannte Datei"}:{line}";
                }))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase);

    private static bool MatchesAssemblyStableId(ISymbol symbol, string stableId)
    {
        var declarationId = DocumentationCommentId.CreateDeclarationId(symbol);
        return declarationId is not null
            && (string.Equals(declarationId, stableId, StringComparison.Ordinal)
                || string.Equals(NormalizeDocCommentId(declarationId), NormalizeDocCommentId(stableId), StringComparison.Ordinal));
    }

    private static bool TryNormalizeHandoffId(
        string value,
        AnalysisSymbolIdentity? expectedIdentity,
        out string normalizedId,
        out bool isAssemblyId,
        out bool isHandoff,
        out CallToolResult? error)
    {
        normalizedId = value;
        isAssemblyId = false;
        isHandoff = false;
        error = null;
        if (!SymbolHandoffIdentifier.HasWirePrefix(value)
            && !SymbolHandoffIdentifier.HasUnsupportedPrefix(value))
        {
            // Unpräfixte Werte bleiben direkte fachliche Suchanfragen. Sie werden niemals als
            // Handoff ausgegeben; nur s:/a:-Werte durchlaufen die gebundene ID-Prüfung.
            isAssemblyId = false;
            return true;
        }

        if (!SymbolHandoffIdentifier.TryParse(value, out var providedIdentifier))
        {
            error = McpToolResults.InvalidArgument(
                "Die Handoff-ID ist nicht kanonisch.",
                hint: "Eine ID aus dem StructuredContent des aktuellen find_symbol-Ergebnisses kopieren.",
                fieldPath: "$.symbolIdentifier");
            return false;
        }

        isHandoff = true;
        isAssemblyId = providedIdentifier.Origin == SymbolHandoffOrigin.Assembly;
        if (expectedIdentity is null
            || expectedIdentity.IsAssembly != isAssemblyId
            || !SymbolHandoffToken.TryCreateTarget(expectedIdentity.CanonicalPath, out var expectedTargetToken)
            || !string.Equals(expectedTargetToken, providedIdentifier.TargetToken, StringComparison.Ordinal))
        {
            error = McpToolResults.TargetMismatch(SymbolHandoffIdentifier.ForError(value));
            return false;
        }

        if (!SymbolHandoffToken.TryCreateContent(expectedIdentity.ContentHash, out var expectedContentToken)
            || !string.Equals(expectedContentToken, providedIdentifier.ContentToken, StringComparison.Ordinal))
        {
            error = McpToolResults.StaleSnapshot(SymbolHandoffIdentifier.ForError(value));
            return false;
        }

        normalizedId = providedIdentifier.DocumentationCommentId;
        return true;
    }

    internal static bool HasKnownDocumentationCommentIdPrefix(string id)
    {
        return id.StartsWith("M:", StringComparison.Ordinal)
            || id.StartsWith("T:", StringComparison.Ordinal)
            || id.StartsWith("P:", StringComparison.Ordinal)
            || id.StartsWith("F:", StringComparison.Ordinal)
            || id.StartsWith("E:", StringComparison.Ordinal)
            || id.StartsWith("!:", StringComparison.Ordinal);
    }

    internal static ISymbol? TryFindEnclosingMember(SyntaxNode root, TextSpan lineSpan, SemanticModel semanticModel)
    {
        var node = root.FindNode(lineSpan, findInsideTrivia: false, getInnermostNodeForTie: true);
        var symbol = FindEnclosingDeclarationSymbol(node, semanticModel);
        if (symbol is not null) return symbol;

        var token = root.FindToken(lineSpan.Start);
        if (token.Parent is not null)
        {
            symbol = FindEnclosingDeclarationSymbol(token.Parent, semanticModel);
            if (symbol is not null) return symbol;
        }

        return null;
    }

    private static ISymbol? FindEnclosingDeclarationSymbol(SyntaxNode startNode, SemanticModel semanticModel)
    {
        var member = startNode.AncestorsAndSelf()
            .FirstOrDefault(n => n is MethodDeclarationSyntax
                              or PropertyDeclarationSyntax
                              or ConstructorDeclarationSyntax
                              or IndexerDeclarationSyntax
                              or EventDeclarationSyntax
                              or AccessorDeclarationSyntax
                              or LocalFunctionStatementSyntax);

        if (member is not null)
        {
            return semanticModel.GetDeclaredSymbol(member);
        }

        var type = startNode.AncestorsAndSelf().FirstOrDefault(n => n is BaseTypeDeclarationSyntax);
        return type is not null ? semanticModel.GetDeclaredSymbol(type) : null;
    }
}
