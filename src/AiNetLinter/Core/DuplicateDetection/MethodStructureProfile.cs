#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// Deterministisches Strukturprofil einer Methode: Sparse-Feature-Vektor fuer Cosine-Similarity
/// plus menschenlesbare Kurzfassung fuer MCP-Ausgabe.
/// </summary>
internal sealed record MethodStructureProfile(
    string Summary,
    IReadOnlyDictionary<string, double> Features);
