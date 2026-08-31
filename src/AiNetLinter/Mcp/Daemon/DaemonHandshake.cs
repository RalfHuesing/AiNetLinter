#nullable enable

namespace AiNetLinter.Mcp.Daemon;

internal interface IDaemonIdentityProvider
{
    DaemonIdentity GetIdentity();
}

internal sealed class CurrentDaemonIdentityProvider : IDaemonIdentityProvider
{
    public DaemonIdentity GetIdentity()
    {
        var version = McpServerOptionsFactory.GetServerVersion();
        return new DaemonIdentity(version, version, Environment.ProcessId);
    }
}

internal sealed class DaemonHandshake
{
    private readonly object gate = new();
    private readonly IDaemonIdentityProvider identityProvider;
    private readonly EffectiveDaemonConfiguration effectiveConfiguration;
    private bool configurationWarningReported;
    private bool shutdownRequested;

    internal DaemonHandshake(
        IDaemonIdentityProvider identityProvider,
        EffectiveDaemonConfiguration effectiveConfiguration)
    {
        this.identityProvider = identityProvider;
        this.effectiveConfiguration = effectiveConfiguration;
    }

    internal event Action<DaemonConfigurationDivergence>? ConfigurationWarning;

    internal DaemonHandshakeResult HandleHello(DaemonHello hello, int activeConnectionCount)
    {
        ArgumentNullException.ThrowIfNull(hello);
        ArgumentOutOfRangeException.ThrowIfNegative(activeConnectionCount);

        DaemonConfigurationDivergence? warning = null;
        DaemonHandshakeResult result;
        lock (gate)
        {
            var identity = identityProvider.GetIdentity();
            result = EvaluateHello(hello, activeConnectionCount, identity, out warning);
            if (warning is not null)
            {
                configurationWarningReported = true;
            }
        }

        if (warning is not null)
        {
            ConfigurationWarning?.Invoke(warning);
        }

        return result;
    }

    private DaemonHandshakeResult EvaluateHello(
        DaemonHello hello,
        int activeConnectionCount,
        DaemonIdentity identity,
        out DaemonConfigurationDivergence? warning)
    {
        warning = CreateConfigurationWarning(hello.Configuration);
        if (hello.ProtocolVersion != DaemonProtocol.Version)
        {
            warning = null;
            return ProtocolRejected(hello.ProtocolVersion);
        }

        if (!string.Equals(hello.ExecutableVersion, identity.ExecutableVersion, StringComparison.Ordinal))
        {
            warning = null;
            return HandleExecutableMismatch(hello.ExecutableVersion, identity.ExecutableVersion, activeConnectionCount);
        }

        var welcome = new DaemonWelcome(
            identity.DaemonVersion,
            identity.ExecutableVersion,
            identity.ProcessId,
            effectiveConfiguration);
        return new DaemonHandshakeResult(
            DaemonHandshakeStatus.Accepted,
            Welcome: welcome,
            ConfigurationDivergence: warning);
    }

    private DaemonConfigurationDivergence? CreateConfigurationWarning(
        EffectiveDaemonConfiguration? received)
    {
        if (configurationWarningReported || effectiveConfiguration.MatchesAdvertisedPeer(received))
        {
            return null;
        }

        return new DaemonConfigurationDivergence(effectiveConfiguration, received);
    }

    private DaemonHandshakeResult ProtocolRejected(int receivedVersion) =>
        new(
            DaemonHandshakeStatus.ProtocolRejected,
            Error: new DaemonError(
                DaemonProtocol.UnsupportedProtocolVersion,
                $"Unterstuetzte Protokollversion ist {DaemonProtocol.Version}; empfangen wurde {receivedVersion}."));

    private DaemonHandshakeResult HandleExecutableMismatch(
        string receivedVersion,
        string expectedVersion,
        int activeConnectionCount)
    {
        if (activeConnectionCount == 0 && !shutdownRequested)
        {
            shutdownRequested = true;
            return new DaemonHandshakeResult(
                DaemonHandshakeStatus.ShutdownRequested,
                Shutdown: new DaemonShutdown(DaemonProtocol.ExecutableVersionMismatch));
        }

        return new DaemonHandshakeResult(
            DaemonHandshakeStatus.VersionConflict,
            Error: new DaemonError(
                DaemonProtocol.VersionConflict,
                $"Ausfuehrbare Version {receivedVersion} kollidiert mit {expectedVersion}."));
    }
}
