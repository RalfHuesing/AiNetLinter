#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
using AiNetLinter.Mcp.Assemblies.Analysis.References;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;

internal sealed class AssemblyAnalysisRegistryCoordinatorContext
{
    internal Lock Gate { get; init; } = null!;
    internal Dictionary<string, AssemblyAnalysisRegistryEntryCreation> Entries { get; init; } = null!;
    internal Dictionary<string, long> NextGenerations { get; init; } = null!;
    internal List<Task> RetiredEntries { get; init; } = null!;
    internal Func<bool> IsDisposed { get; init; } = null!;
    internal AssemblyAnalysisResourceBudget ResourceBudget { get; init; } = null!;
    internal Func<AssemblyAnalysisEntry, Task>? BeforeRetirementAsync { get; init; }
    internal Func<AssemblyAnalysisRegistryEntryCreation, Task> RetireEntryAsync { get; init; } = null!;
    internal AssemblyAnalysisSourceProjectEntryFactory SourceProjectEntryFactory { get; init; } = null!;
    internal Action<string, AssemblyAnalysisRegistryEntryCreation> ObserveCreation { get; init; } = null!;
    internal Action<string, AssemblyAnalysisRegistryEntryCreation> RemoveFailedEntry { get; init; } = null!;
    internal Func<bool, string?, CancellationToken, Task<int>> RunEvictionTick { get; init; } = null!;
    internal Func<string, bool, AssemblyAnalysisLeaseResult> Failure { get; init; } = null!;
}
