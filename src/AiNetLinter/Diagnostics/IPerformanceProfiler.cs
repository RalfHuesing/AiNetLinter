#nullable enable

namespace AiNetLinter.Diagnostics;

internal interface IPerformanceProfiler
{
    void StartPhase(string phaseName);
    void StopPhase(string phaseName);
    void RecordDocumentAnalysis(string filePath, double durationMs, int violationsCount);
    void RecordPostAnalysisStep(string stepName, double durationMs);
    void WriteReport(string targetPath, string? solutionFilePath, string? rulesFilePath = null);
}
