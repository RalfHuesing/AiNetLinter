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
    internal const string ProjectNotRestored  = "PROJECT_NOT_RESTORED";
    internal const string AnalysisFailed      = "ANALYSIS_FAILED";
    internal const string ResourceNotFound    = "RESOURCE_NOT_FOUND";
    internal const string DriftDetected       = "DRIFT_DETECTED";
    internal const string SolutionNotLoaded   = "SOLUTION_NOT_LOADED";
    internal const string SymbolNotFound      = "SYMBOL_NOT_FOUND";
    internal const string AmbiguousSymbol     = "AMBIGUOUS_SYMBOL";
    internal const string InvalidArgument     = "INVALID_ARGUMENT";
}
