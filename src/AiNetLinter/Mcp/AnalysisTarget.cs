#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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

    internal bool HasLegacyArguments => LegacyKeys.Count > 0;

    internal IReadOnlySet<string> LegacyKeys { get; init; } = EmptyLegacyKeys;

    internal static AnalysisTargetRequest FromArguments(
        IReadOnlyDictionary<string, object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var targetPath = arguments.TryGetValue("targetPath", out var rawTargetPath)
            ? rawTargetPath as string
            : null;
        var legacyKeys = arguments.Keys
            .Where(key => LegacyArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new AnalysisTargetRequest(targetPath)
        {
            LegacyKeys = legacyKeys,
        };
    }

    private static readonly IReadOnlySet<string> EmptyLegacyKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] LegacyArgumentNames =
        ["targetType", "projectRoot", "configPath", "ainetlinter.project.json"];
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
