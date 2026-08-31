#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;

internal interface IAssemblyAnalysisEvictionResourceBudget
{
    TimeSpan IdleTtl { get; }
    DateTime UtcNow { get; }
    int EvictIdle();
    bool HasCapacity(string path);
}

internal sealed class AssemblyAnalysisRegistryEvictionContext
{
    internal Lock Gate { get; init; } = null!;
    internal Dictionary<string, AssemblyAnalysisRegistryEntryCreation> Entries { get; init; } = null!;
    internal List<Task> RetiredEntries { get; init; } = null!;
    internal Func<bool> IsDisposed { get; init; } = null!;
    internal IAssemblyAnalysisEvictionResourceBudget ResourceBudget { get; init; } = null!;
    internal Func<AssemblyAnalysisEntry, Task>? BeforeRetirementAsync { get; init; }
    internal Func<AssemblyAnalysisRegistryEntryCreation, Task> RetireEntryAsync { get; init; } = null!;
}
