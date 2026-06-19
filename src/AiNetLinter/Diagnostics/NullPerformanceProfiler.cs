#nullable enable

namespace AiNetLinter.Diagnostics;

internal sealed class NullPerformanceProfiler : IPerformanceProfiler
{
    internal static readonly NullPerformanceProfiler Instance = new();

    private NullPerformanceProfiler() { }

    public void StartPhase(string phaseName) { }
    public void StopPhase(string phaseName) { }
    public void RecordDocumentAnalysis(string filePath, double durationMs, int violationsCount) { }
    public void RecordPostAnalysisStep(string stepName, double durationMs) { }
    public void WriteReport(string targetPath, string? solutionFilePath, string? rulesFilePath = null) { }
}
