#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;

internal interface IAssemblyAnalysisEvictionResourceBudget
{
    TimeSpan IdleTtl { get; }
    DateTime UtcNow { get; }
    int EvictIdle();
    bool HasCapacity(string path);
}

internal interface IAssemblyAnalysisEvictionEntry
{
    string CanonicalPath { get; }
    DateTime LastUsedUtc { get; }
    bool IsRetiring { get; }
    bool IsIdleForCapacity();
    bool IsIdle(DateTime now, TimeSpan idleTtl);
}

internal sealed record AssemblyAnalysisEvictionCreation(
    string Key,
    IAssemblyAnalysisEvictionEntry Entry,
    Func<Task?> TryRetire);

internal sealed record AssemblyAnalysisEvictionCandidate(
    string Key,
    string CanonicalPath,
    DateTime LastUsedUtc,
    Func<bool> IsIdleForCapacity,
    Func<DateTime, TimeSpan, bool> IsIdle,
    Func<Task?> TryRetire,
    Func<Task>? BeforeRetirementAsync = null,
    Action? OnRetired = null);

internal sealed class AssemblyAnalysisRegistryEvictionContext
{
    internal IAssemblyAnalysisEvictionResourceBudget ResourceBudget { get; init; } = null!;
    internal Func<Task<IReadOnlyList<AssemblyAnalysisEvictionCandidate>>> GetCandidates { get; init; } = null!;
    internal Func<AssemblyAnalysisEvictionCandidate, Task?> TryRetireCandidate { get; init; } = null!;
}
