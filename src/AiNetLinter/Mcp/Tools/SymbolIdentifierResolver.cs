#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// Kleine, reine Parsing-/Aufloesungs-Helfer fuer <see cref="FindReferencesTool"/> — in eine eigene
/// Datei ausgelagert, damit <see cref="FindReferencesTool"/>s eigener <c>AIContextFootprint</c>
/// (siehe <c> nicht durch reine Hilfslogik unnoetig waechst, waehrend
/// <see cref="McpCodeGraphServer"/> (Parameter von <see cref="FindReferencesTool.ExecuteAsync"/>)
/// bereits allein einen erheblichen transitiven Anteil beitraegt.
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
        Solution solution, string stableId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(stableId) || !HasKnownDocumentationCommentIdPrefix(stableId))
        {
            return (null, null);
        }

        foreach (var project in solution.Projects)
        {
            var declared = await SymbolFinder.FindSourceDeclarationsAsync(
                project, name => true, SymbolFilter.TypeAndMember, ct);
            foreach (var symbol in declared)
            {
                if (DocumentationCommentId.CreateDeclarationId(symbol) == stableId)
                {
                    return (symbol, null);
                }
            }
        }

        return (null, null);
    }

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
