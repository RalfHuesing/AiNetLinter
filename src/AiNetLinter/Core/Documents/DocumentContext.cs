#nullable enable

using Microsoft.CodeAnalysis;
using AiNetLinter.Configuration;

namespace AiNetLinter.Core.Documents;

/// <summary>
/// Kontextinformationen für das zu analysierende Dokument.
/// </summary>
internal sealed record DocumentContext(
    string FilePath,
    SemanticModel SemanticModel,
    bool IsTestFile,
    Config EffectiveConfig,
    string ProjectName,
    bool ProjectHasLoadDiagnostics = false
);
