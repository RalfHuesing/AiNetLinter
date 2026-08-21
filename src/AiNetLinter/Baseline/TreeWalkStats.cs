#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Baseline;

/// <summary>
/// Ergebnis eines <see cref="FileSystemExclusionHelpers.WalkFilteredTree"/>-Laufs: Jede Warnung
/// entspricht genau einem unzugänglichen Teilbaum — die Listenlänge ist damit zugleich der
/// Fehlerzähler (Health-Metadaten, Konzept 02 Abschnitt C). Bewusst auf Namespace-Ebene statt
/// nested, damit der Typ per Datei-Listing auffindbar bleibt (BanPublicNestedTypes).
/// </summary>
internal sealed record TreeWalkStats(IReadOnlyList<string> Warnings)
{
    internal int InaccessibleSubtreeCount => Warnings.Count;
}
