#nullable enable

using AiNetLinter.Mcp.Daemon;

namespace AiNetLinter.IntegrationTests.Mcp.Daemon;

[Trait("Category", "Integration")]
public sealed class DaemonHostProcessContractTests
{
    [Fact]
    public async Task TwoDaemonProcessesOnOneEndpointRejectSecondAndReleaseLock()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        using var temp = TestTempDirectory.Create("daemon-process-contract-");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var endpointLease = await DaemonProcessContractHarness
            .AcquireEndpointAsync(cancellation.Token)
            .ConfigureAwait(false);
        var spec = new DaemonProcessSpec(fixture.RootPath, temp.DirectoryPath, 0.1m);

        await using var first = await DaemonProcessContractHarness
            .StartAsync(spec, cancellation.Token)
            .ConfigureAwait(false);
        await using var heldConnection = await DaemonProcessContractHarness
            .ConnectWhenReadyAsync(cancellation.Token)
            .ConfigureAwait(false);
        var heldWelcome = await DaemonProcessContractHarness
            .PerformHandshakeAsync(heldConnection, spec, cancellation.Token)
            .ConfigureAwait(false);
        Assert.Equal(DaemonProtocol.Welcome, heldWelcome.Type);

        var second = await DaemonProcessContractHarness
            .RunToExitAsync(spec, TimeSpan.FromSeconds(10), cancellation.Token)
            .ConfigureAwait(false);
        Assert.False(second.TimedOut, second.Error);
        Assert.NotEqual(0, second.ExitCode);
        Assert.Contains("[ERROR]: Daemon fuer Pipe-Endpunkt", second.Error, StringComparison.Ordinal);

        await AssertHostHandshakeAsync(spec, cancellation.Token).ConfigureAwait(false);
        await heldConnection.DisposeAsync().ConfigureAwait(false);
        var firstResult = await first.WaitForExitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        Assert.False(firstResult.TimedOut, firstResult.Error);
        Assert.True(firstResult.ExitCode == 0, firstResult.Error);

        var third = await DaemonProcessContractHarness
            .RunToExitAsync(spec, TimeSpan.FromSeconds(15), cancellation.Token)
            .ConfigureAwait(false);
        Assert.False(third.TimedOut, third.Error);
        Assert.Equal(0, third.ExitCode);
    }

    private static async Task AssertHostHandshakeAsync(
        DaemonProcessSpec spec,
        CancellationToken cancellationToken)
    {
        await using var connection = await DaemonProcessContractHarness
            .ConnectWhenReadyAsync(cancellationToken)
            .ConfigureAwait(false);
        var welcome = await DaemonProcessContractHarness
            .PerformHandshakeAsync(connection, spec, cancellationToken)
            .ConfigureAwait(false);

        Assert.Equal(DaemonProtocol.Welcome, welcome.Type);
        Assert.Equal(DaemonProtocol.Version, welcome.ProtocolVersion);
    }
}
