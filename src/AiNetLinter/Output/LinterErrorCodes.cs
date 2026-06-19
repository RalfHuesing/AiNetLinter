#nullable enable

namespace AiNetLinter.Output;

/// <summary>
/// Definierte Fehlercodes fuer maschinenlesbares Error-Reporting.
/// </summary>
internal static class LinterErrorCodes
{
    internal const string ConfigRequired      = "CONFIG_REQUIRED";
    internal const string ConfigNotFound      = "CONFIG_NOT_FOUND";
    internal const string ConfigInvalid       = "CONFIG_INVALID";
    internal const string ConfigSmell         = "CONFIG_SMELL";
    internal const string BaselineNotFound    = "BASELINE_NOT_FOUND";
    internal const string BaselineInvalid     = "BASELINE_INVALID";
    internal const string WorkspaceDiagnostic = "WORKSPACE_DIAGNOSTIC";
    internal const string AnalysisFailed      = "ANALYSIS_FAILED";
    internal const string ResourceNotFound    = "RESOURCE_NOT_FOUND";
    internal const string DriftDetected       = "DRIFT_DETECTED";
}
