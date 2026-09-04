#nullable enable

using System;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core;

/// <summary>
/// Zentrale Extension-Methoden fuer Roslyn <see cref="ISymbol"/>-Instanzen.
/// Konsolidiert haeufig wiederkehrende Symbol-Normalisierungen und fehlerresistente
/// ID-Erzeugungen projektweit (DRY-Konsolidierung via find_duplicates structural audit).
/// </summary>
internal static class RoslynSymbolExtensions
{
    /// <summary>
    /// Loest Compiler-generierte Accessoren (z. B. Property get/set, Event add/remove) 
    /// auf das uebergeordnete Member-Symbol auf.
    /// </summary>
    public static ISymbol? NormalizeToOwningMember(this ISymbol? symbol) =>
        symbol is IMethodSymbol { AssociatedSymbol: { } owner } ? owner : symbol;

    /// <summary>
    /// Erzeugt die DocumentationCommentId (stabile Declaration-ID) sicher und faengt Roslyn-Fehler ab.
    /// </summary>
    public static string? TryGetDocCommentId(this ISymbol? symbol)
    {
        if (symbol is null) return null;
        try
        {
            return DocumentationCommentId.CreateDeclarationId(symbol);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _ = ex;
            return null;
        }
    }

    /// <summary>
    /// Prüft, ob ein <see cref="SyntaxTrivia"/> ein Kommentar oder XML-Dokumentations-Trivia ist.
    /// </summary>
    public static bool IsCommentOrDocTrivia(this SyntaxTrivia trivia) =>
        trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineDocumentationCommentTrivia)
        || trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.DocumentationCommentExteriorTrivia);
}
