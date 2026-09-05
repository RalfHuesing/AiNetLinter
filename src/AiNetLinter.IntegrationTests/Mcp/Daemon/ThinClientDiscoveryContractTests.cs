#nullable enable

using System.IO.Pipelines;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Daemon;

[Trait("Category", "Integration")]
public sealed class ThinClientDiscoveryContractTests
{
    [Fact]
    public async Task NewClient_StopsAfterLegacyWelcomeWithoutFingerprint_WithoutReadinessRetry()
    {
        var instance = "legacy-" + Guid.NewGuid().ToString("N")[..12];
        var transport = new DaemonPipeTransport(daemonInstance: instance);
        await using var serverStream = transport.CreateServerStream();
        using var serverCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = ServeLegacyWelcomeAsync(serverStream, serverCancellation.Token);
        var console = new RecordingLintConsole();
        var input = new Pipe();
        var output = new Pipe();
        var connectAttempts = 0;
        var detachedStarts = 0;
        var context = new ThinClientSessionContext(
            CancellationToken.None,
            console,
            new ThinClientSessionOptions(
                _ => throw new InvalidOperationException("Der Legacy-Test muss den Instanz-Transport verwenden."),
                (_, _) =>
                {
                    Interlocked.Increment(ref detachedStarts);
                    return true;
                },
                TimeSpan.FromSeconds(30),
                input.Reader.AsStream(),
                output.Writer.AsStream(),
                ConnectForInstanceAsync: (requestedInstance, cancellationToken) =>
                {
                    Interlocked.Increment(ref connectAttempts);
                    return new DaemonPipeTransport(daemonInstance: requestedInstance)
                        .ConnectAsync(cancellationToken);
                },
                AcquireStartupGateForInstanceAsync: (_, _, _) =>
                    throw new InvalidOperationException("Ein Discovery-Mismatch darf keinen Startup-Gate-Pfad erreichen.")));

        try
        {
            var exitCode = await ThinClientProxy.RunSessionAsync(
                new ThinClientLaunchOptions(null, null, null, instance),
                context).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            Assert.Equal(2, exitCode);
            Assert.Equal(1, Volatile.Read(ref connectAttempts));
            Assert.Equal(0, Volatile.Read(ref detachedStarts));
            var error = Assert.Single(console.ErrorLines);
            Assert.Contains(DaemonProtocol.DiscoveryFingerprintMismatch, error, StringComparison.Ordinal);
            Assert.Contains("kein Retry ohne Zustandsaenderung", error, StringComparison.Ordinal);
            Assert.Contains("empfangen fehlt", error, StringComparison.Ordinal);
            await server.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        finally
        {
            await ThinClientPipeTestDoubles.CompleteAsync(input).ConfigureAwait(false);
            await ThinClientPipeTestDoubles.CompleteAsync(output).ConfigureAwait(false);
            serverCancellation.Cancel();
            try
            {
                await server.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (serverCancellation.IsCancellationRequested)
            {
            }
        }
    }

    private static async Task ServeLegacyWelcomeAsync(
        Stream serverStream,
        CancellationToken cancellationToken)
    {
        await ((System.IO.Pipes.NamedPipeServerStream)serverStream)
            .WaitForConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var connection = new DaemonPipeConnection(serverStream);
        var hello = await connection.ReadJsonFrameAsync<DaemonHello>(cancellationToken).ConfigureAwait(false);
        Assert.NotNull(hello);

        await connection.WriteJsonFrameAsync(
            new DaemonWelcome(
                DaemonVersion: hello.ExecutableVersion,
                ExecutableVersion: hello.ExecutableVersion,
                ProcessId: 9876,
                Configuration: EffectiveDaemonConfiguration.Default,
                ToolContractFingerprint: null),
            cancellationToken).ConfigureAwait(false);

        Assert.Null(await connection.ReadFrameAsync(cancellationToken).ConfigureAwait(false));
    }
}
