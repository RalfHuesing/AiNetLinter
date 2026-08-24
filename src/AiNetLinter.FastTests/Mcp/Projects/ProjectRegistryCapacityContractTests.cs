#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Projects;

/// <summary>
/// Kapazitaets-Vertrag der Registry (Soft-Cap): Bei nur-busy Slots waechst der Bestand
/// ueber MaxProjects und der TTL-Tick raeumt den Ueberschuss nicht aktiv — bewusste
/// Entscheidung, dokumentiert an <c>ProjectRegistry.EvictLeastRecentlyUsed</c>. Erst wenn
/// ein Slot frei wird, greift die LRU-Eviction beim naechsten Insert wieder.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProjectRegistryCapacityContractTests
{
    [Fact]
    public async Task Lease_AllSlotsBusy_AllowsOverflowUntilSlotFreesThenEvictsAgain()
    {
        using var tempDir = TestTempDirectory.Create("registry-capacity-");
        var rootA = WriteMinimalProject(tempDir, "alpha");
        var rootB = WriteMinimalProject(tempDir, "beta");
        var rootC = WriteMinimalProject(tempDir, "gamma");
        var rootD = WriteMinimalProject(tempDir, "delta");
        var factory = new TrackingServerFactory();
        var clock = new FakeClock();
        await using var registry = new ProjectRegistry(new ProjectRegistryOptions(
            factory.Factory,
            clock,
            MaxProjects: 2,
            IdleTtl: TimeSpan.FromMinutes(45)));

        // Alle Slots busy: Der dritte Key wird trotzdem registriert (Ueberlauf erlaubt),
        // und der TTL-Tick reklamiert den Ueberschuss nicht.
        using var heldALease = registry.Lease(rootA).Lease!;
        using var heldBLease = registry.Lease(rootB).Lease!;
        clock.AdvanceMinutes(1);
        using var overflowLease = registry.Lease(rootC).Lease!;

        Assert.Equal(3, registry.Snapshots().Count);
        Assert.Equal(3, factory.InstancesCreated);
        Assert.Equal(0, factory.LoadsCancelled);
        clock.AdvanceMinutes(1);
        await registry.RunEvictionTickAsync();
        Assert.Equal(3, registry.Snapshots().Count);

        // Slot wird frei: Die Eviction greift beim naechsten Insert wieder und raumt den
        // am laengsten ungenutzten freien Entry (alpha).
        heldALease.Dispose();
        clock.AdvanceMinutes(1);

        using var fourthLease = registry.Lease(rootD).Lease!;

        Assert.Equal(1, factory.LoadsCancelled);
        Assert.Null(registry.FindSnapshot(rootA));
        Assert.NotNull(registry.FindSnapshot(rootD));
        await registry.DisposeAsync();
    }

    private static string WriteMinimalProject(TestTempDirectory tempDir, string name)
    {
        var root = System.IO.Path.Combine(tempDir.DirectoryPath, name);
        tempDir.CreateFile(System.IO.Path.Combine(name, "app.slnx"), string.Empty);
        tempDir.CreateFile(System.IO.Path.Combine(name, "rules.json"), "{}");
        tempDir.CreateFile(
            System.IO.Path.Combine(name, "ainetlinter.project.json"),
            "{ \"solution\": \"app.slnx\", \"rules\": \"rules.json\" }");
        return root;
    }
}
