#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Kapselt den mutablen Zustand für die EnforceReadonlyFields-Analyse
/// und verhindert so shared-state-Kopplung über Partial-Klassen-Grenzen.
/// </summary>
internal sealed class FieldReadonlyTracker
{
    private readonly HashSet<IFieldSymbol> _candidates = new(SymbolEqualityComparer.Default);
    private readonly HashSet<IFieldSymbol> _modifiedOutsideConstructor = new(SymbolEqualityComparer.Default);

    internal void RegisterCandidate(IFieldSymbol field) => _candidates.Add(field);

    internal void MarkModifiedOutsideConstructor(IFieldSymbol field) => _modifiedOutsideConstructor.Add(field);

    internal bool IsCandidate(IFieldSymbol field) => _candidates.Contains(field);

    internal IEnumerable<IFieldSymbol> GetReadonlyCandidates()
    {
        foreach (var field in _candidates)
        {
            if (!_modifiedOutsideConstructor.Contains(field))
            {
                yield return field;
            }
        }
    }

    internal static RuleViolation BuildViolation(IFieldSymbol field, string filePath)
    {
        var syntaxRef = field.DeclaringSyntaxReferences.Length > 0
            ? field.DeclaringSyntaxReferences[0]
            : null;
        var syntaxNode = syntaxRef?.GetSyntax();
        var lineNumber = syntaxNode != null
            ? syntaxNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            : 1;

        return new RuleViolation
        {
            FilePath = filePath,
            LineNumber = lineNumber,
            RuleName = "EnforceReadonlyFields",
            Details = $"Das private Feld '{field.Name}' wird nur im Konstruktor oder Initialisierer zugewiesen, ist aber nicht als 'readonly' deklariert.",
            Guidance = "Fuege den 'readonly' Modifikator zum Feld hinzu, um unabsichtliche Modifikationen zu verhindern.",
        };
    }
}
