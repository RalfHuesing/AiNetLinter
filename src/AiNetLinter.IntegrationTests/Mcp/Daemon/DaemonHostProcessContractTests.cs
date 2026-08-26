#nullable enable

using AiNetLinter.Mcp.Daemon;

namespace AiNetLinter.IntegrationTests.Mcp.Daemon;

[Trait("Category", "Integration")]
public sealed class DaemonHostProcessContractTests
{
    public DaemonHostProcessContractTests(DaemonEndpointJanitorFixture janitor) => _ = janitor;

    [Fact]
    public async Task TwoDaemonProcessesOnOneEndpointRejectSecondAndReleaseLock()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        using var temp = TestTempDirectory.Create("daemon-process-contract-");
        // Das Endpunkt-Gate ist exklusiv und wird von bis zu vier Daemon-Contracts geteilt;
        // die Wartezeit auf den eigenen Turn ist legitim und muss das Budget ueberleben,
        // nicht der Hang-Schutz des eigentlichen Testablaufs (der hat eigene Timeouts).
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(240));
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

    [Fact]
    public async Task TwoDaemonProcessesOnDifferentInstancesRunInParallel()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        using var temp = TestTempDirectory.Create("daemon-process-instances-");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var suffix = Guid.NewGuid().ToString("N");
        var beta = "beta-" + suffix[..12];
        var gamma = "gamma-" + suffix[..12];
        var firstSpec = new DaemonProcessSpec(
            fixture.RootPath,
            temp.DirectoryPath,
            0.1m,
            DaemonInstance: beta);
        var secondSpec = firstSpec with { DaemonInstance = gamma };

        await AssertParallelInstancesAsync(firstSpec, secondSpec, cancellation.Token)
            .ConfigureAwait(false);
    }

    private static async Task AssertParallelInstancesAsync(
        DaemonProcessSpec firstSpec,
        DaemonProcessSpec secondSpec,
        CancellationToken cancellationToken)
    {
        var (first, second) = await StartBothAsync(firstSpec, secondSpec, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await AssertParallelConnectionsAsync(
                first,
                second,
                firstSpec,
                secondSpec,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await DisposeBothAsync(
                first,
                second,
                static handle => handle.DisposeAsync()).ConfigureAwait(false);
        }
    }

    private static async Task AssertParallelConnectionsAsync(
        DaemonProcessHandle first,
        DaemonProcessHandle second,
        DaemonProcessSpec firstSpec,
        DaemonProcessSpec secondSpec,
        CancellationToken cancellationToken)
    {
        var beta = firstSpec.DaemonInstance!;
        var gamma = secondSpec.DaemonInstance!;
        var (firstConnection, secondConnection) = await ConnectBothAsync(
            beta,
            gamma,
            cancellationToken).ConfigureAwait(false);
        try
        {
            var welcomes = await Task.WhenAll(
                DaemonProcessContractHarness.PerformHandshakeAsync(firstConnection, firstSpec, cancellationToken),
                DaemonProcessContractHarness.PerformHandshakeAsync(secondConnection, secondSpec, cancellationToken));

            Assert.Equal(DaemonProtocol.Welcome, welcomes[0].Type);
            Assert.Equal(DaemonProtocol.Welcome, welcomes[1].Type);
            Assert.NotEqual(welcomes[0].ProcessId, welcomes[1].ProcessId);
            Assert.NotEqual(
                DaemonProtocol.GetPipeName(DaemonProtocol.CurrentUserName, beta),
                DaemonProtocol.GetPipeName(DaemonProtocol.CurrentUserName, gamma));
        }
        finally
        {
            await DisposeBothAsync(
                firstConnection,
                secondConnection,
                static connection => connection.DisposeAsync()).ConfigureAwait(false);
        }

        var results = await Task.WhenAll(
            first.WaitForExitAsync(TimeSpan.FromSeconds(15)),
            second.WaitForExitAsync(TimeSpan.FromSeconds(15))).ConfigureAwait(false);
        Assert.All(results, result =>
        {
            Assert.False(result.TimedOut, result.Error);
            Assert.Equal(0, result.ExitCode);
        });
    }

    private static async Task<(DaemonProcessHandle First, DaemonProcessHandle Second)> StartBothAsync(
        DaemonProcessSpec firstSpec,
        DaemonProcessSpec secondSpec,
        CancellationToken cancellationToken)
    {
        var firstStart = DaemonProcessContractHarness.StartAsync(firstSpec, cancellationToken);
        var secondStart = DaemonProcessContractHarness.StartAsync(secondSpec, cancellationToken);
        try
        {
            await Task.WhenAll(firstStart, secondStart).ConfigureAwait(false);
            return (
                await firstStart.ConfigureAwait(false),
                await secondStart.ConfigureAwait(false));
        }
        catch
        {
            await DisposeCompletedAsync(
                firstStart,
                secondStart,
                static handle => handle.DisposeAsync()).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<(DaemonPipeConnection First, DaemonPipeConnection Second)> ConnectBothAsync(
        string firstInstance,
        string secondInstance,
        CancellationToken cancellationToken)
    {
        var firstConnect = DaemonProcessContractHarness.ConnectWhenReadyAsync(
            firstInstance,
            cancellationToken);
        var secondConnect = DaemonProcessContractHarness.ConnectWhenReadyAsync(
            secondInstance,
            cancellationToken);
        try
        {
            await Task.WhenAll(firstConnect, secondConnect).ConfigureAwait(false);
            return (
                await firstConnect.ConfigureAwait(false),
                await secondConnect.ConfigureAwait(false));
        }
        catch
        {
            await DisposeCompletedAsync(
                firstConnect,
                secondConnect,
                static connection => connection.DisposeAsync()).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task DisposeCompletedAsync<T>(
        Task<T> firstTask,
        Task<T> secondTask,
        Func<T, ValueTask> disposeAsync)
        where T : class
    {
        T? first = null;
        T? second = null;
        if (firstTask.IsCompletedSuccessfully)
        {
            first = await firstTask.ConfigureAwait(false);
        }

        if (secondTask.IsCompletedSuccessfully)
        {
            second = await secondTask.ConfigureAwait(false);
        }

        await DisposeBothAsync(first, second, disposeAsync).ConfigureAwait(false);
    }

    private static async ValueTask DisposeBothAsync<T>(
        T? first,
        T? second,
        Func<T, ValueTask> disposeAsync)
        where T : class
    {
        try
        {
            if (second is not null)
            {
                await disposeAsync(second).ConfigureAwait(false);
            }
        }
        finally
        {
            if (first is not null)
            {
                await disposeAsync(first).ConfigureAwait(false);
            }
        }
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
