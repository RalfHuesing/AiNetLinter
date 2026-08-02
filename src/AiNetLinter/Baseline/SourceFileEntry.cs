namespace AiNetLinter.Baseline;

/// <summary>
/// Eine analysierbare Quelldatei mit absolutem und relativem Pfad.
/// </summary>
public sealed record SourceFileEntry(string AbsolutePath, string RelativePath);
