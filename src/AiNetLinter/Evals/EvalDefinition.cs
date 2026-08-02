#nullable enable

namespace AiNetLinter.Evals;

internal enum EvalEvidenceType { Vocabulary, Structure }

/// <summary>
/// Beschreibt einen Eval-Typ: Name, Anzeige, benötigte Evidence.
/// </summary>
internal sealed record EvalDefinition(
    string Name,
    string DisplayName,
    string Description,
    EvalEvidenceType Evidence);
