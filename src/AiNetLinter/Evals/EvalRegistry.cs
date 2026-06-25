#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.Evals;

/// <summary>
/// Statisches Register aller verfügbaren Eval-Typen.
/// </summary>
internal static class EvalRegistry
{
    internal static readonly IReadOnlyList<EvalDefinition> All =
    [
        new EvalDefinition(
            Name:        "naming-drift",
            DisplayName: "Naming & Vocabulary Drift",
            Description: "Vergleicht Domain-Vokabular aus der Spec mit Code-Identifiers. Findet Synonyme, aufgeblähte Namen und verwaiste Begriffe.",
            Evidence:    EvalEvidenceType.Vocabulary),

        new EvalDefinition(
            Name:        "architecture-intent",
            DisplayName: "Architecture Intent",
            Description: "Prüft ob die Verzeichnisstruktur und Dateigrößen noch dem ursprünglichen Design-Intent entsprechen.",
            Evidence:    EvalEvidenceType.Structure),
    ];

    internal static EvalDefinition? TryResolve(string name) =>
        All.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
