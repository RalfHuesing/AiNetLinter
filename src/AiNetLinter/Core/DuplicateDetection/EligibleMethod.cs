#nullable enable

using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// Fuer Duplicate-Detection zulaessige Methode bzw. lokale Funktion nach denselben Ausschluessen
/// wie die tokenbasierte Clone-Erkennung (generierter Code, permanente Pfade, Scope, triviale
/// Koerper). Traegt Syntax und <see cref="SemanticModel"/>, damit Clone-Fingerprints und
/// Strukturprofile aus derselben Kandidatenmenge entstehen.
/// </summary>
internal sealed record EligibleMethod(
    string FilePath,
    int LineNumber,
    string SignatureName,
    int TokenCount,
    SyntaxNode Declaration,
    SyntaxNode Body,
    IMethodSymbol Symbol,
    SemanticModel SemanticModel);
