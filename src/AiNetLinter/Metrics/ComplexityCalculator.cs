using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Metrics;

/// <summary>
/// Berechnet Komplexitätsmetriken für C#-Syntaxknoten.
/// </summary>
public static class ComplexityCalculator
{
    /// <summary>
    /// Berechnet die Zyklomatische Komplexität (McCabe) für eine Methode oder Eigenschaft.
    /// </summary>
    public static int GetCyclomaticComplexity(MethodDeclarationSyntax method)
    {
        // TODO: Roslyn SyntaxWalker implementieren, der Verzweigungen (if, while, case, &&, || etc.) zählt.
        // Erstmal Standardrückgabe für das Grundgerüst.
        return 1;
    }

    /// <summary>
    /// Berechnet die Kognitive Komplexität (SonarSource) für eine Methode oder Eigenschaft.
    /// </summary>
    public static int GetCognitiveComplexity(MethodDeclarationSyntax method)
    {
        // TODO: Roslyn SyntaxWalker für kognitive Komplexität (inkl. Nesting-Faktor) implementieren.
        // Erstmal Standardrückgabe für das Grundgerüst.
        return 1;
    }
}
