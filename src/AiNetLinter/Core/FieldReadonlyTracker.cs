#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Kapselt den mutablen Zustand für die EnforceReadonlyFields-Analyse.
/// Thread-sicher für gemeinsame Nutzung über Partial-Class-Dateien hinweg.
/// </summary>
internal sealed class FieldReadonlyTracker
{
    private readonly object _lock = new();
    private readonly HashSet<IFieldSymbol> _candidates = new(SymbolEqualityComparer.Default);
    private readonly HashSet<IFieldSymbol> _modifiedOutsideConstructor = new(SymbolEqualityComparer.Default);

    internal void RegisterCandidate(IFieldSymbol field)
    {
        lock (_lock) _candidates.Add(field);
    }

    internal void MarkModifiedOutsideConstructor(IFieldSymbol field)
    {
        lock (_lock) _modifiedOutsideConstructor.Add(field);
    }

    internal bool IsCandidate(IFieldSymbol field)
    {
        lock (_lock) return _candidates.Contains(field);
    }

    internal IReadOnlyList<IFieldSymbol> GetReadonlyCandidates()
    {
        lock (_lock)
        {
            return _candidates
                .Where(f => !_modifiedOutsideConstructor.Contains(f))
                .ToList();
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
