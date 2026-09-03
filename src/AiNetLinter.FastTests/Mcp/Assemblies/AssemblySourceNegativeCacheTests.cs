#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.Analysis.SourceSelection;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Providers;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

// @covers AssemblySourceProviderCoordinator
[Trait("Category", "Unit")]
public sealed class AssemblySourceNegativeCacheTests
{
    [Fact]
    public void RememberNegativeResult_StoresFallbackReasonAndDiagnostics_AndReturnsTrueBeforeExpiry()
    {
        using var registry = new SourceSnapshotRegistry();
        var coordinator = new AssemblySourceProviderCoordinator(new UnavailableExternalSourceProvider(), registry);

        var assemblyPath = "C:\\test\\assembly.dll";
        var reason = AssemblySourceFallbackReasons.ProviderUnavailable;
        var diagnostics = new List<ExternalSourceConfigurationDiagnostic>
        {
            new("ERR01", "Provider unavailable test", "error", "$test")
        };

        coordinator.RememberNegativeResult(assemblyPath, reason, diagnostics, TimeSpan.FromMinutes(1));

        var hit = coordinator.TryGetNegativeResult(assemblyPath, out var cachedReason, out var cachedDiagnostics);
        Assert.True(hit);
        Assert.Equal(reason, cachedReason);
        Assert.NotNull(cachedDiagnostics);
        Assert.Single(cachedDiagnostics!);
        Assert.Equal("ERR01", cachedDiagnostics[0].Code);
    }

    [Fact]
    public async Task RememberNegativeResult_WithShortTtl_ExpiresAndReturnsFalse()
    {
        using var registry = new SourceSnapshotRegistry();
        var coordinator = new AssemblySourceProviderCoordinator(new UnavailableExternalSourceProvider(), registry);

        var assemblyPath = "C:\\test\\assembly_expired.dll";
        coordinator.RememberNegativeResult(
            assemblyPath,
            AssemblySourceFallbackReasons.ProviderDegraded,
            null,
            TimeSpan.FromMilliseconds(50));

        // Unmittelbar nach Speichern noch aktiv
        Assert.True(coordinator.TryGetNegativeResult(assemblyPath, out _, out _));

        // Nach Ablauf des kurzen TTL abgelaufen
        await Task.Delay(100);
        var hit = coordinator.TryGetNegativeResult(assemblyPath, out var reason, out var diagnostics);
        Assert.False(hit);
        Assert.Null(reason);
        Assert.Null(diagnostics);
    }

    [Fact]
    public void RememberNegativeResult_RemovesPreviouslyCachedSnapshotIdentity()
    {
        using var registry = new SourceSnapshotRegistry();
        var coordinator = new AssemblySourceProviderCoordinator(new UnavailableExternalSourceProvider(), registry);

        var assemblyPath = "C:\\test\\assembly_replace.dll";
        coordinator.RememberSnapshotIdentity(assemblyPath, "snapshot-identity-123");
        Assert.True(coordinator.TryGetCachedSnapshotIdentity(assemblyPath, out var id));
        Assert.Equal("snapshot-identity-123", id);

        // Negatives Ergebnis überschreibt/entfernt die gecachte SnapshotIdentity
        coordinator.RememberNegativeResult(assemblyPath, AssemblySourceFallbackReasons.MappingNotFound, null);
        Assert.False(coordinator.TryGetCachedSnapshotIdentity(assemblyPath, out _));
    }
}
