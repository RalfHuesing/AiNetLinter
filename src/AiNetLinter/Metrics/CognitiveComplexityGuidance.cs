using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Metrics;

/// <summary>
/// Erzeugt konkrete Refactor-Hints bei starker Überschreitung der kognitiven Komplexität.
/// </summary>
public static class CognitiveComplexityGuidance
{
    private const int ExcessThreshold = 2;

    // Grenze ab der eine Methode als "kurz aber dicht" gilt (nicht als "lang und komplex").
    private const int ShortMethodLineThreshold = 20;

    /// <summary>
    /// Liefert erweiterte Guidance, wenn die Komplexität deutlich über dem Limit liegt.
    /// Differenziert zwischen kurzen dichten Methoden und langen komplexen Methoden,
    /// da das Refactoring-Muster für beide Fälle verschieden ist.
    /// </summary>
    public static string Build(MethodDeclarationSyntax method, int complexity, int limit)
    {
        if (complexity <= limit + ExcessThreshold)
            return "Vereinfache verschachtelte Kontrollstrukturen (If-in-If etc.) und lagere Logik in flache Hilfsmethoden aus.";

        var lineCount = MethodLineCounter.GetCodeLineCount(method);
        var nestedIfCount = method.DescendantNodes().OfType<IfStatementSyntax>().Count();

        if (lineCount < ShortMethodLineThreshold)
            return BuildDenseMethodGuidance(method, nestedIfCount);

        return BuildLongComplexMethodGuidance(method, nestedIfCount);
    }

    private static string BuildDenseMethodGuidance(MethodDeclarationSyntax method, int nestedIfCount)
    {
        // Kurze aber dichte Methode: komplexe Bedingungen in benannte Booleans extrahieren
        var hint = nestedIfCount >= 2
            ? $"Extrahiere komplexe Bedingungen in benannte Properties oder Hilfsmethoden (z. B. 'bool IsEligibleFor{method.Identifier.Text}() => ...'). "
            : "";
        return $"{hint}Kurze, dichte Methoden profitieren von lesbaren Zwischenwerten: benannte Variablen und Guard-Clauses statt verschachtelter Conditions.";
    }

    private static string BuildLongComplexMethodGuidance(MethodDeclarationSyntax method, int nestedIfCount)
    {
        // Lange UND komplexe Methode: klassisches Extract Method
        var extractHint = nestedIfCount >= 2
            ? $"Extrahiere erkannte if-Bloecke in Methoden wie 'TryResolve{method.Identifier.Text}()'. "
            : "";
        return $"{extractHint}Die Methode ist lang UND kognitiv komplex — teile sie in kleinere Hilfsmethoden auf (Extract Method). Jede Hilfsmethode sollte eine klar benennbare Aufgabe haben.";
    }
}
