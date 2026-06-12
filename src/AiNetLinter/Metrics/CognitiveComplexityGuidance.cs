using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Metrics;

/// <summary>
/// Erzeugt konkrete Refactor-Hints bei starker Überschreitung der kognitiven Komplexität.
/// </summary>
public static class CognitiveComplexityGuidance
{
    private const int ExcessThreshold = 2;

    /// <summary>
    /// Liefert erweiterte Guidance, wenn die Komplexität deutlich über dem Limit liegt.
    /// </summary>
    public static string Build(MethodDeclarationSyntax method, int complexity, int limit)
    {
        var baseGuidance =
            "Vereinfache verschachtelte Kontrollstrukturen (If-in-If etc.) und lagere Logik in flache Hilfsmethoden aus.";

        if (complexity <= limit + ExcessThreshold)
        {
            return baseGuidance;
        }

        var nestedIfCount = method.DescendantNodes().OfType<IfStatementSyntax>().Count();
        if (nestedIfCount >= 2)
        {
            return $"{baseGuidance} Extrahiere erkannte if-Bloecke in Methoden wie TryResolve{method.Identifier.Text}().";
        }

        return baseGuidance;
    }
}
