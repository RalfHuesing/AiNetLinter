#nullable enable

using AiNetLinter.Configuration;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.References;

internal interface ISolutionStateProvider
{
    Solution? GetCurrentSolution();
    AnalysisSymbolIdentity? AssemblySymbolIdentity { get; }
    AnalysisSymbolIdentity? HandoffSymbolIdentity { get; }
    ServerLoadState LoadState { get; }
    ILintConsole Console { get; }
    (ILinterEngineConfig? Config, string? ResolvedConfigPath) GetConfigSnapshot();
}
