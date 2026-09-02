#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Kleine, reine Parsing-/Aufloesungs-Helfer fuer <see cref="FindReferencesTool.ResolveSymbolAsync"/>
/// — in eine eigene Datei ausgelagert, damit <see cref="FindReferencesTool"/>s eigener
/// <c>AIContextFootprint</c> (siehe <c>AiNetLinter.mdc</c>) nicht durch reine Hilfslogik unnoetig
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
        if (string.IsNullOrEmpty(stableId))
        {
            return (null, null);
        }

        if (!TryNormalizeAssemblyId(
                stableId,
                expectedAssemblyIdentity,
                out var normalizedId,
                out var isAssemblyId,
                out var assemblyError))
        {
            return (null, assemblyError);
        }

        stableId = normalizedId;

        if (!HasKnownDocumentationCommentIdPrefix(stableId))
        {
            return (null, null);
        }

        if (expectedAssemblyIdentity is not null && !isAssemblyId)
        {
            return (null, StaleAssemblyId(stableId));
        }

        var assemblyCandidates = isAssemblyId ? new List<ISymbol>() : null;
        var exactMatch = await FindExactStableIdAsync(solution, stableId, ct, assemblyCandidates);
        if (exactMatch is not null) return (exactMatch, null);

        if (assemblyCandidates is not null)
        {
            var matches = assemblyCandidates
                .Where(symbol => MatchesAssemblyStableId(symbol, stableId))
                .Distinct(SymbolEqualityComparer.Default)
                .ToList();
            if (matches.Count == 1) return (matches[0], null);
        }

        return (null, null);
    }

    private static async Task<ISymbol?> FindExactStableIdAsync(
        Solution solution,
        string stableId,
        CancellationToken ct,
        ICollection<ISymbol>? assemblyCandidates)
    {
        foreach (var project in solution.Projects)
        {
            var declared = await SymbolFinder.FindSourceDeclarationsAsync(
                project, name => true, SymbolFilter.TypeAndMember, ct);
            foreach (var symbol in declared)
            {
                var declarationId = DocumentationCommentId.CreateDeclarationId(symbol);
                if (declarationId == stableId)
                {
                    return symbol;
                }

                if (assemblyCandidates is not null) assemblyCandidates.Add(symbol);
            }
        }

        return null;
    }

    private static bool MatchesAssemblyStableId(ISymbol symbol, string stableId)
    {
        var declarationId = DocumentationCommentId.CreateDeclarationId(symbol);
        if (declarationId is null) return false;

        return string.Equals(
                   NormalizeUnresolvedStableId(declarationId),
                   NormalizeUnresolvedStableId(stableId),
                   StringComparison.Ordinal)
            || MatchesStableIdShape(declarationId, stableId);
    }

    private static bool MatchesStableIdShape(string declarationId, string stableId)
    {
        if (!TryParseStableIdShape(declarationId, out var declarationShape)
            || !TryParseStableIdShape(stableId, out var stableShape))
        {
            return false;
        }

        return declarationShape.Prefix == stableShape.Prefix
            && string.Equals(declarationShape.Name, stableShape.Name, StringComparison.Ordinal)
            && declarationShape.ParameterCount == stableShape.ParameterCount;
    }

    private static bool TryParseStableIdShape(string value, out StableIdShape shape)
    {
        shape = default;
        if (value.Length < 3 || value[1] != ':') return false;

        var payload = value[2..];
        var parameterStart = payload.IndexOf('(');
        if (parameterStart < 0)
        {
            shape = new(value[0], NormalizeUnresolvedStableId(payload), null);
            return !string.IsNullOrEmpty(shape.Name);
        }

        if (!payload.EndsWith(")", StringComparison.Ordinal)) return false;
        var name = payload[..parameterStart];
        var parameters = payload[(parameterStart + 1)..^1];
        shape = new(value[0], NormalizeUnresolvedStableId(name), CountStableParameters(parameters));
        return !string.IsNullOrEmpty(shape.Name);
    }

    private static int CountStableParameters(string parameters)
    {
        if (string.IsNullOrEmpty(parameters)) return 0;

        var depth = 0;
        var separators = 0;
        foreach (var character in parameters)
        {
            if (character is '{' or '[' or '<' or '(') depth++;
            else if (character is '}' or ']' or '>' or ')') depth = Math.Max(0, depth - 1);
            else if (character == ',' && depth == 0) separators++;
        }

        return separators + 1;
    }

    private static string NormalizeUnresolvedStableId(string value) =>
        value.Replace("~", string.Empty, StringComparison.Ordinal)
            .Replace("?", string.Empty, StringComparison.Ordinal);

    private readonly record struct StableIdShape(char Prefix, string Name, int? ParameterCount);

    private static bool TryNormalizeAssemblyId(
        string value,
        AnalysisSymbolIdentity? expectedIdentity,
        out string normalizedId,
        out bool isAssemblyId,
        out CallToolResult? error)
    {
        normalizedId = value;
        isAssemblyId = false;
        error = null;
        if (!value.StartsWith(AnalysisSymbolIdentity.Prefix, StringComparison.Ordinal)) return true;

        isAssemblyId = true;
        if (!AnalysisSymbolIdentity.TryParse(value, out var providedIdentity, out var unwrappedId)
            || providedIdentity is null
            || expectedIdentity is null
            || !expectedIdentity.Matches(providedIdentity))
        {
            error = StaleAssemblyId(value);
            return false;
        }

        normalizedId = unwrappedId;
        return true;
    }

    private static CallToolResult StaleAssemblyId(string identifier) =>
        McpToolResults.Recoverable(
            LinterErrorCodes.InvalidArgument,
            $"Die Assembly-Symbol-ID '{identifier}' gehört nicht zur aktuellen Assembly-Generation.",
            hint: "Eine aktuelle assembly:<sha256>:<generation>:<symbolId>-ID aus dem Assembly-Ziel verwenden.");

    private static bool HasKnownDocumentationCommentIdPrefix(string id)
    {
        return id.StartsWith("M:", StringComparison.Ordinal)
            || id.StartsWith("T:", StringComparison.Ordinal)
            || id.StartsWith("P:", StringComparison.Ordinal)
            || id.StartsWith("F:", StringComparison.Ordinal)
            || id.StartsWith("E:", StringComparison.Ordinal)
            || id.StartsWith("!:", StringComparison.Ordinal);
    }
}
