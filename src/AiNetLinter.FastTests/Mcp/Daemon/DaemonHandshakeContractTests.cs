#nullable enable

using AiNetLinter.Mcp.Daemon;

namespace AiNetLinter.FastTests.Mcp.Daemon;

[Trait("Category", "Unit")]
public sealed class DaemonHandshakeContractTests
{
    private static readonly EffectiveDaemonConfiguration Configuration =
        new(4, 10m);

    [Fact]
    public void HandleHello_WithMatchingProtocolAndVersion_ReturnsWelcome()
    {
        var handshake = CreateHandshake();

        var result = handshake.HandleHello(CreateHello(), activeConnectionCount: 0);

        Assert.True(result.IsAccepted);
        Assert.NotNull(result.Welcome);
        Assert.Equal("daemon-1", result.Welcome.DaemonVersion);
        Assert.Equal("exe-1", result.Welcome.ExecutableVersion);
        Assert.Equal(4321, result.Welcome.ProcessId);
        Assert.Equal(Configuration, result.Welcome.Configuration);
    }

    [Fact]
    public void HandleHello_WithUnknownProtocol_RejectsWithoutWelcome()
    {
        var handshake = CreateHandshake();
        var hello = new DaemonHello("exe-1", 99, Configuration, DaemonProtocol.Version + 1);

        var result = handshake.HandleHello(hello, activeConnectionCount: 0);

        Assert.Equal(DaemonHandshakeStatus.ProtocolRejected, result.Status);
        Assert.Equal(DaemonProtocol.UnsupportedProtocolVersion, result.Error?.Code);
        Assert.Null(result.Welcome);
    }

    [Fact]
    public void HandleHello_VersionMismatchWithoutOtherConnections_RequestsShutdownOnce()
    {
        var handshake = CreateHandshake();
        var hello = new DaemonHello("exe-2", 99, Configuration);

        var first = handshake.HandleHello(hello, activeConnectionCount: 0);
        var second = handshake.HandleHello(hello, activeConnectionCount: 0);

        Assert.Equal(DaemonHandshakeStatus.ShutdownRequested, first.Status);
        Assert.NotNull(first.Shutdown);
        Assert.Equal(DaemonProtocol.ExecutableVersionMismatch, first.Shutdown.Reason);
        Assert.Equal(DaemonHandshakeStatus.VersionConflict, second.Status);
        Assert.Equal(DaemonProtocol.VersionConflict, second.Error?.Code);
        Assert.Null(second.Shutdown);
    }

    [Fact]
    public void HandleHello_VersionMismatchWithOtherConnections_ReturnsVersionConflict()
    {
        var handshake = CreateHandshake();
        var hello = new DaemonHello("exe-2", 99, Configuration);

        var result = handshake.HandleHello(hello, activeConnectionCount: 1);

        Assert.Equal(DaemonHandshakeStatus.VersionConflict, result.Status);
        Assert.Equal(DaemonProtocol.VersionConflict, result.Error?.Code);
        Assert.Null(result.Shutdown);
    }

    [Fact]
    public void HandleHello_ConfigurationDivergence_ReportsOneStructuredWarning()
    {
        var warnings = new List<DaemonConfigurationDivergence>();
        var handshake = CreateHandshake();
        handshake.ConfigurationWarning += warnings.Add;
        var requested = new EffectiveDaemonConfiguration(2, 5m);
        var hello = new DaemonHello("exe-1", 99, requested);

        var first = handshake.HandleHello(hello, activeConnectionCount: 0);
        var second = handshake.HandleHello(hello, activeConnectionCount: 0);

        Assert.NotNull(first.ConfigurationDivergence);
        Assert.Null(second.ConfigurationDivergence);
        var warning = Assert.Single(warnings);
        Assert.Equal(Configuration, warning.Expected);
        Assert.Equal(requested, warning.Received);
        Assert.Equal(DaemonProtocol.ConfigurationDivergence, warning.Code);
    }

    [Fact]
    public void HandleHello_ExternalLimitDivergenceIsReportedForNewClient()
    {
        var effective = new EffectiveDaemonConfiguration(
            4,
            10m,
            ExternalMaxDiskBytes: 100,
            ExternalMaxMemoryBytes: 200,
            ExternalMaxParallelOperations: 3,
            ExternalMaxResidentResources: 5,
            ExternalIdleTtlMinutes: 12m);
        var handshake = new DaemonHandshake(
            new FakeIdentityProvider(new DaemonIdentity("daemon-1", "exe-1", 4321)),
            effective);
        var requested = effective with { ExternalMaxDiskBytes = 50 };

        var result = handshake.HandleHello(new DaemonHello("exe-1", 99, requested), activeConnectionCount: 0);

        Assert.True(result.IsAccepted);
        Assert.NotNull(result.ConfigurationDivergence);
        Assert.Equal(effective, result.ConfigurationDivergence.Expected);
        Assert.Equal(requested, result.ConfigurationDivergence.Received);
    }

    [Fact]
    public void HandleHello_OldClientWithoutExternalFieldsRemainsCompatible()
    {
        var effective = new EffectiveDaemonConfiguration(
            4,
            10m,
            ExternalMaxDiskBytes: 100,
            ExternalMaxMemoryBytes: 200,
            ExternalMaxParallelOperations: 3,
            ExternalMaxResidentResources: 5,
            ExternalIdleTtlMinutes: 12m);
        var handshake = new DaemonHandshake(
            new FakeIdentityProvider(new DaemonIdentity("daemon-1", "exe-1", 4321)),
            effective);

        var result = handshake.HandleHello(CreateHello(), activeConnectionCount: 0);

        Assert.True(result.IsAccepted);
        Assert.Null(result.ConfigurationDivergence);
    }

    private static DaemonHandshake CreateHandshake() => new(
        new FakeIdentityProvider(new DaemonIdentity("daemon-1", "exe-1", 4321)),
        Configuration);

    private static DaemonHello CreateHello() => new("exe-1", 99, Configuration);

    private sealed class FakeIdentityProvider(DaemonIdentity identity) : IDaemonIdentityProvider
    {
        public DaemonIdentity GetIdentity() => identity;
    }
}
