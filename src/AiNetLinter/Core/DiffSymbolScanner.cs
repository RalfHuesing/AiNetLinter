#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core;

/// <summary>
/// Symbolermittlungs-Scope eines Diff-Analyse-Laufs: <see cref="Callers"/> behaelt den
/// bisherigen schmalen Scope (oeffentliche/interne Methoden und Konstruktoren) bei,
/// <see cref="ChangeContext"/> oeffnet den breiten Scope inklusive privater Symbole,
/// Properties/Indexer, Events, Felder, Typdeklarationen und lokaler Funktionen.
/// </summary>
internal enum DiffSymbolScope
{
    Callers,
    ChangeContext,
}

/// <summary>
/// Breiter Diff-Symbolscanner: bildet je Dokument aus den Hunk-Ranges die Menge getroffener
/// Deklarationen ab — pro geaenderter Zeile die innerste passende Deklaration, partielle
/// Deklarationen ueber Datei + Spanne je Teildeklaration unterscheidbar. Die Kandidatenmenge
/// haengt vom <see cref="DiffSymbolScope"/> ab; Ueberlappungspruefung, Innerste-Regel und
/// Entry-Bildung laufen fuer beide Scopes uniform.
/// </summary>
internal static class DiffSymbolScanner
{
    /// <summary>
    /// Ermittelt die im Diff getroffenen Symbole eines Dokuments als Paarung von Symbol und
    /// knotenbasiertem Eintrag (Datei/Spanne aus der konkreten Deklaration, damit partielle
    /// Typen je geaenderter Teildeklaration erscheinen).
    /// </summary>
    internal static async Task<List<ChangedSymbolMatch>> FindChangedSymbolsAsync(
        Document document, IReadOnlyList<HunkRange> ranges, DiffSymbolScope scope)
    {
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (root == null || semanticModel == null) return [];

        var overlapping = CollectCandidateNodes(root, scope)
            .Where(node => IntersectsWithChangedLines(node, ranges))
            .ToList();

        var matches = new List<ChangedSymbolMatch>();
        foreach (var node in overlapping.Where(node => IsInnermost(node, overlapping)))
        {
            var symbol = semanticModel.GetDeclaredSymbol(node);
            if (symbol == null || !IsIncludedByScope(symbol, scope)) continue;

            matches.Add(new ChangedSymbolMatch(symbol, CreateEntry(symbol, node, document)));
        }

        return matches;
    }

    /// <summary>
    /// Artabhaengiger Anzeigename: Typdeklarationen namensraumsqualifiziert bzw. verschachtelt
    /// als „EnthaltenderTyp.Name“, lokale Funktionen hinter ihrem einschliessenden Member, alle
    /// uebrigen Member konsistent zur Call-Site-Benennung.
    /// </summary>
    internal static string FormatDisplayName(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol namedType => FormatTypeDisplayName(namedType),
        IMethodSymbol { MethodKind: MethodKind.LocalFunction } localFunction =>
            FormatLocalFunctionDisplayName(localFunction),
        _ => DiffImpactAnalyzer.FormatMemberDisplayName(symbol),
    };

    // Reihenfolge wie im bisherigen schmalen Pfad: erst alle Methoden, dann alle Konstruktoren
    // (je in Quellreihenfolge) — die callers-Ausgabe bleibt so reihenfolgetreu.
    private static IEnumerable<SyntaxNode> CollectCallerCandidates(SyntaxNode root)
    {
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            yield return method;
        }

        foreach (var constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            yield return constructor;
        }
    }

    private static IEnumerable<SyntaxNode> CollectCandidateNodes(SyntaxNode root, DiffSymbolScope scope) =>
        scope == DiffSymbolScope.ChangeContext
            ? root.DescendantNodes().Where(IsBroadCandidate).SelectMany(ExpandToDeclaredNodes)
            : CollectCallerCandidates(root);

    // Accessor-Deklarationen bewusst keine eigenen Kandidaten: sie gehen über Containment in
    // ihrem Property/Indexer/Event auf. Lambdas, anonyme Funktionen, lokale Variablen und
    // Statements bleiben ebenso außen vor.
    private static bool IsBroadCandidate(SyntaxNode node) =>
        IsTypeLikeDeclaration(node) || IsMemberLikeDeclaration(node)
        || node is LocalFunctionStatementSyntax;

    private static bool IsTypeLikeDeclaration(SyntaxNode node) =>
        node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax;

    private static bool IsMemberLikeDeclaration(SyntaxNode node) =>
        node is MethodDeclarationSyntax or ConstructorDeclarationSyntax
            or PropertyDeclarationSyntax or IndexerDeclarationSyntax
            or EventDeclarationSyntax or EventFieldDeclarationSyntax or FieldDeclarationSyntax;

    // Feld- und Ereignisfeld-Deklarationen haengen ihre Symbole an den Variablen-Deklarator,
    // nicht an den Wrapper-Knoten — erst diese Expansion macht sie semantisch aufloesbar.
    private static IEnumerable<SyntaxNode> ExpandToDeclaredNodes(SyntaxNode node) => node switch
    {
        FieldDeclarationSyntax field => field.Declaration.Variables,
        EventFieldDeclarationSyntax eventField => eventField.Declaration.Variables,
        _ => [node],
    };

    // Eine Quelle der Wahrheit fuer „innerste Deklaration“: ein Kandidat weicht nur, wenn seine
    // Spanne einen anderen Kandidaten vollstaendig enthaelt. Auf dem schmalen Pfad ein No-op,
    // dessen Kandidaten (Methoden/Konstruktoren) sich gegenseitig nicht enthalten.
    private static bool IsInnermost(SyntaxNode node, IReadOnlyList<SyntaxNode> peers) =>
        !peers.Any(peer =>
            !ReferenceEquals(node, peer) && node.Span != peer.Span && node.Span.Contains(peer.Span));

    private static bool IsIncludedByScope(ISymbol symbol, DiffSymbolScope scope) =>
        scope == DiffSymbolScope.ChangeContext || IsPublicOrInternal(symbol);

    // Ueberlappungspruefung auf Hunk-Ranges; semantisch identisch zur frueheren
    // Einzellinien-Mitgliedschaft (count=0-Ranges expandieren zu keiner Zeile).
    private static bool IntersectsWithChangedLines(SyntaxNode node, IReadOnlyList<HunkRange> ranges)
    {
        var span = node.GetLocation().GetLineSpan();
        var start = span.StartLinePosition.Line + 1;
        var end = span.EndLinePosition.Line + 1;

        foreach (var range in ranges)
        {
            if (range.LineCount <= 0) continue;
            var rangeEnd = range.StartLine + range.LineCount - 1;
            if (rangeEnd >= start && range.StartLine <= end) return true;
        }

        return false;
    }

    private static bool IsPublicOrInternal(ISymbol symbol)
    {
        var accessibility = symbol.DeclaredAccessibility;
        return accessibility == Accessibility.Public ||
               accessibility == Accessibility.Internal ||
               accessibility == Accessibility.Protected ||
               accessibility == Accessibility.ProtectedOrInternal;
    }

    private static ChangedSymbolEntry CreateEntry(ISymbol symbol, SyntaxNode node, Document document) =>
        DiffImpactAnalyzer.CreateChangedSymbolEntry(symbol, document, node.GetLocation());

    private static string FormatTypeDisplayName(INamedTypeSymbol type)
    {
        if (type.ContainingType is { } containingType)
        {
            return $"{containingType.Name}.{type.Name}";
        }

        return type.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
            ? $"{containingNamespace.ToDisplayString()}.{type.Name}"
            : type.Name;
    }

    private static string FormatLocalFunctionDisplayName(IMethodSymbol localFunction)
    {
        if (localFunction.ContainingSymbol is not IMethodSymbol { MethodKind: MethodKind.LocalFunction } enclosing)
        {
            return $"{DiffImpactAnalyzer.FormatMemberDisplayName(localFunction.ContainingSymbol!)}.{localFunction.Name}";
        }

        return $"{FormatLocalFunctionDisplayName(enclosing)}.{localFunction.Name}";
    }
}
