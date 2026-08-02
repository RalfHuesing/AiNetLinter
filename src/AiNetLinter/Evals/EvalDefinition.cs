#nullable enable

namespace AiNetLinter.Evals;

internal enum EvalEvidenceType { Vocabulary, Structure }

internal sealed record EvalDefinition(
    string Name,
    string DisplayName,
    string Description,
    EvalEvidenceType Evidence);
