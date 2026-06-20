#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Generators;

/// <summary>
/// DTO für die Typ-Informationen im Codegraphen.
/// </summary>
internal sealed record TypeInfo(
    string Name,
    string Modifiers,
    string? BaseType,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> Dependencies);
