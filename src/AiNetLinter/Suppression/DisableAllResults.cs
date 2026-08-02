namespace AiNetLinter.Suppression;

public sealed record DisableAllInjectResult(int CandidateFiles, int ModifiedFiles, int SkippedFiles);

public sealed record DisableAllRemoveResult(int ScannedFiles, int ModifiedFiles);
