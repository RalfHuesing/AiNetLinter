#nullable enable

using System;
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

internal sealed record AnalysisTargetRequest(
    string? TargetType,
    string? TargetPath);

internal sealed record AnalysisTarget(
    AnalysisTargetType TargetType,
    string CanonicalPath,
    AnalysisTargetRequest Request);

internal sealed record AnalysisTargetResolution(
    AnalysisTarget? Target,
    CallToolResult? Error);

internal sealed record AnalysisToolDispatch(
    Func<ProjectLease, Task<CallToolResult>>? ProjectCall = null,
    Func<string, Task<CallToolResult>>? AssemblyCall = null,
    Func<AssemblyAnalysisLease, Task<CallToolResult>>? AssemblySessionCall = null,
    bool ExpandAssemblyReferences = false);
