namespace AiNetLinter.Suppression;

/// <summary>
/// Ergebnis eines Inject-Laufs (Disable-all-Kommentare in C#-Dateien einfuegen).
/// </summary>
public sealed record DisableAllInjectResult(int CandidateFiles, int ModifiedFiles, int SkippedFiles);

/// <summary>
/// Ergebnis eines Remove-Laufs (Disable-all-Kommentare aus C#-Dateien entfernen).
/// </summary>
public sealed record DisableAllRemoveResult(int ScannedFiles, int ModifiedFiles);
