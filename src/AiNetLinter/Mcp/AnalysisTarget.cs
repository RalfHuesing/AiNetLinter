#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Projects;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal enum AnalysisTargetType
{
    Project,
    Assembly,
}

internal enum AnalysisTargetOrigin
{
    Source,
    Decompiled,
}

internal enum AnalysisCapabilityStatus
{
    Supported,
    NotConfigured,
    Unsupported,
}

internal sealed record AnalysisTargetCapabilities(
    AnalysisCapabilityStatus Navigation,
    AnalysisCapabilityStatus Lint);

/// <summary>Request-Modell des einheitlichen targetPath-only Vertrags.</summary>
internal sealed record AnalysisTargetRequest
{
    internal AnalysisTargetRequest(string? targetPath)
    {
        TargetPath = targetPath;
    }

    internal string? TargetPath { get; }

}

internal sealed record AnalysisTarget(
    AnalysisTargetType TargetType,
    string CanonicalPath,
    AnalysisTargetRequest Request)
{
    internal AnalysisTargetOrigin Origin =>
        TargetType == AnalysisTargetType.Project
            ? AnalysisTargetOrigin.Source
            : AnalysisTargetOrigin.Decompiled;

    internal string AnalysisRoot { get; init; } = string.Empty;

    internal string Fingerprint { get; init; } = string.Empty;

    internal string? RulesPath { get; init; }

    internal AnalysisTargetCapabilities Capabilities { get; init; } =
        new(AnalysisCapabilityStatus.Supported, AnalysisCapabilityStatus.Unsupported);
}

internal sealed record ResolvedAnalysisContext(AnalysisTarget Target);

internal sealed record AnalysisTargetResolution(
    AnalysisTarget? Target,
    CallToolResult? Error);

internal sealed record AnalysisToolDispatch(
    Func<ProjectLease, Task<CallToolResult>>? ProjectCall = null,
    Func<string, Task<CallToolResult>>? AssemblyCall = null,
    Func<AssemblyAnalysisLease, Task<CallToolResult>>? AssemblySessionCall = null,
    bool ExpandAssemblyReferences = false,
    int MaxResponseBytes = 0,
    string? DetailLevel = null,
    string? Cursor = null);

internal sealed record AssemblyAnalysisExecutionOptions(
    bool ExpandAssemblyReferences = false,
    CancellationToken CancellationToken = default,
    int MaxResponseBytes = 0,
    string? DetailLevel = null,
    string? Cursor = null);
