#nullable enable

using System;
using System.IO;
using System.Threading;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed record AssemblyAnalysisRegistryRuntimeOptions(
    AssemblyDecompilationConfiguration? DecompilationConfiguration = null,
    string? DaemonProfile = null);

internal static class ExternalResourceRegistryDefaults
{
    internal const long MaxDiskBytes = ExternalSourceResourceOptions.DefaultMaxDiskBytes;
    internal const long MaxMemoryBytes = ExternalSourceResourceOptions.DefaultMaxMemoryBytes;
    internal const int MaxParallelOperations = ExternalSourceResourceOptions.DefaultMaxParallelOperations;
    internal const int MaxResidentResources = ExternalSourceResourceOptions.DefaultMaxResidentResources;
    internal static readonly TimeSpan IdleTtl = ExternalSourceResourceOptions.DefaultIdleTtl;
}

internal sealed record ExternalResourceRegistryOptions(
    long MaxDiskBytes = ExternalResourceRegistryDefaults.MaxDiskBytes,
    long MaxMemoryBytes = ExternalResourceRegistryDefaults.MaxMemoryBytes,
    int MaxParallelOperations = ExternalResourceRegistryDefaults.MaxParallelOperations,
    int MaxResidentResources = ExternalResourceRegistryDefaults.MaxResidentResources,
    TimeSpan IdleTtl = default,
    TimeProvider? Clock = null);

internal enum ExternalResourceHealth
{
    Healthy,
    Degraded,
    CapacityExceeded,
    Disposed,
}

internal sealed record ExternalResourceRequest(
    string Identity,
    long DiskBytes,
    long MemoryBytes);

internal sealed record ExternalResourceHealthSnapshot(
    ExternalResourceHealth Health,
    int ResidentResources,
    int MaxResidentResources,
    long DiskBytes,
    long MaxDiskBytes,
    long MemoryBytes,
    long MaxMemoryBytes,
    int ActiveOperations,
    int MaxParallelOperations,
    string? LastFailureReason);

internal sealed record ExternalResourceAcquireResult(
    ExternalResourceLease? Lease,
    ExternalResourceHealthSnapshot Health,
    string? FailureReason)
{
    internal bool Succeeded => Lease is not null;
}

internal sealed record ExternalResourceRegistryOverrides(
    long? MaxDiskBytes = null,
    long? MaxMemoryBytes = null,
    int? MaxParallelOperations = null,
    int? MaxResidentResources = null,
    decimal? IdleTtlMinutes = null);

internal static class ExternalResourceRegistryOptionsFactory
{
    internal static ExternalResourceRegistryOptions Create(
        ExternalSourceResourceOptions configured,
        ExternalResourceRegistryOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(configured);
        var idleTtl = ResolveIdleTtl(configured.IdleTtl, overrides?.IdleTtlMinutes);
        return new(
            overrides?.MaxDiskBytes ?? configured.MaxDiskBytes,
            overrides?.MaxMemoryBytes ?? configured.MaxMemoryBytes,
            overrides?.MaxParallelOperations ?? configured.MaxParallelOperations,
            overrides?.MaxResidentResources ?? configured.MaxResidentResources,
            idleTtl);
    }

    private static TimeSpan ResolveIdleTtl(TimeSpan configured, decimal? overrideMinutes)
    {
        if (overrideMinutes is null) return configured;
        if (overrideMinutes <= 0 || overrideMinutes > (decimal)TimeSpan.MaxValue.TotalMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(overrideMinutes));
        }

        var ticks = decimal.ToInt64(overrideMinutes.Value * TimeSpan.TicksPerMinute);
        if (ticks <= 0) throw new ArgumentOutOfRangeException(nameof(overrideMinutes));
        return TimeSpan.FromTicks(ticks);
    }
}

internal sealed class AssemblyAnalysisResourceBudget(ExternalResourceRegistry? registry) : IAssemblyAnalysisEvictionResourceBudget
{
    internal bool IsEnabled => registry is not null;

    internal TimeSpan IdleTtl => registry?.IdleTtl ?? ExternalResourceRegistryDefaults.IdleTtl;

    internal TimeProvider Clock => registry?.Clock ?? TimeProvider.System;

    internal DateTime UtcNow => Clock.GetUtcNow().UtcDateTime;

    internal ExternalResourceHealthSnapshot? Health => registry?.Health;

    internal (ExternalResourceLease? Lease, string? FailureReason) Acquire(string path)
    {
        if (registry is null)
        {
            return (null, null);
        }

        var acquired = registry.TryAcquireWithoutEvictions(CreateRequest(path));
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

    internal bool HasCapacity(string path) =>
        registry is null || registry.HasCapacity(CreateRequest(path));

    internal bool CanAccommodate(string path) =>
        registry is null || registry.CanAccommodate(CreateRequest(path));

    TimeSpan IAssemblyAnalysisEvictionResourceBudget.IdleTtl => IdleTtl;

    DateTime IAssemblyAnalysisEvictionResourceBudget.UtcNow => UtcNow;

    int IAssemblyAnalysisEvictionResourceBudget.EvictIdle() => EvictIdle();

    bool IAssemblyAnalysisEvictionResourceBudget.HasCapacity(string path) => HasCapacity(path);

    private ExternalResourceRequest CreateRequest(string path)
    {
        var length = GetAssemblyLength(path);
        return new(
            path,
            length,
            GetAssemblyMemoryEstimate(length, registry!.Health.MaxMemoryBytes));
    }

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

    private static long GetAssemblyMemoryEstimate(long length, long maxMemoryBytes)
    {
        var boundedLength = Math.Min(length, Math.Max(1, maxMemoryBytes / 4));
        return Math.Min(maxMemoryBytes, checked(boundedLength * 4));
    }
}
