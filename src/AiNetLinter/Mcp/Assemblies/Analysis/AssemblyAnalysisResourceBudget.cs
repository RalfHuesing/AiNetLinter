#nullable enable

using System;
using System.IO;
using System.Threading;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyAnalysisResourceBudget(ExternalResourceRegistry? registry)
{
    internal bool IsEnabled => registry is not null;

    internal TimeSpan IdleTtl => registry?.IdleTtl ?? ExternalResourceRegistryDefaults.IdleTtl;

    internal ExternalResourceHealthSnapshot? Health => registry?.Health;

    internal (ExternalResourceLease? Lease, string? FailureReason) Acquire(string path)
    {
        if (registry is null)
        {
            return (null, null);
        }

        var acquired = registry.TryAcquire(new ExternalResourceRequest(
            path,
            GetAssemblyLength(path),
            GetAssemblyMemoryEstimate(path)));
        return (acquired.Lease, acquired.FailureReason);
    }

    internal ExternalResourceOperationLease? BeginOperation(CancellationToken cancellationToken)
    {
        if (registry is null) return null;
        if (registry.TryBeginOperation(cancellationToken, out var operation)) return operation;
        throw new ExternalResourceCapacityException(
            registry.Health.LastFailureReason
            ?? "Das externe Parallelitätsbudget ist ausgeschöpft.");
    }

    internal int EvictIdle() => registry?.EvictIdle() ?? 0;

    private static long GetAssemblyLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return 0;
        }
    }

    private static long GetAssemblyMemoryEstimate(string path)
    {
        var length = GetAssemblyLength(path);
        return Math.Min(length, ExternalResourceRegistryDefaults.MaxMemoryBytes / 4) * 4;
    }
}

internal sealed class ExternalResourceCapacityException(string message) : Exception(message);
