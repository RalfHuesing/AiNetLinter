#nullable enable

using Microsoft.CodeAnalysis;
using AiNetLinter.Configuration;

namespace AiNetLinter.Core;

internal sealed record DocumentContext(
    string FilePath,
    SemanticModel SemanticModel,
    bool IsTestFile,
    Config EffectiveConfig,
    string ProjectName
);
