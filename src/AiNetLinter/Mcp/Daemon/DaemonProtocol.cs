#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiNetLinter.Mcp.Daemon;

internal static class DaemonProtocol
{
    internal const int Version = 1;
    internal const string PipeNamePrefix = "ainetlinter.analyzer.v1.";
    internal const string Hello = "hello";
    internal const string Welcome = "welcome";
    internal const string Shutdown = "shutdown";
    internal const string VersionConflict = "VERSION_CONFLICT";
    internal const string UnsupportedProtocolVersion = "PROTOCOL_VERSION_UNSUPPORTED";
    internal const string ExecutableVersionMismatch = "EXECUTABLE_VERSION_MISMATCH";
    internal const string ConfigurationDivergence = "CONFIGURATION_DIVERGENCE";
    internal const int DefaultMaxProjects = 4;
    internal const decimal DefaultIdleExitMinutes = 10m;

    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    internal static string CurrentUserName => Environment.UserName;

    internal static string GetPipeName(string userName, string? daemonInstance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        var normalizedInstance = DaemonInstanceId.Normalize(daemonInstance);
        return normalizedInstance is null
            ? PipeNamePrefix + userName
            : PipeNamePrefix + userName + "." + normalizedInstance;
    }
}

internal sealed record EffectiveDaemonConfiguration(
    int MaxProjects,
    decimal IdleExitMinutes,
    long? ExternalMaxDiskBytes = null,
    long? ExternalMaxMemoryBytes = null,
    int? ExternalMaxParallelOperations = null,
    int? ExternalMaxResidentResources = null,
    decimal? ExternalIdleTtlMinutes = null)
{
    internal static EffectiveDaemonConfiguration Default { get; } = new(
        DaemonProtocol.DefaultMaxProjects,
        DaemonProtocol.DefaultIdleExitMinutes);

    // Clientseitig sind nur explizit gesetzte externe Limits verbindlich. Ein
    // alter Daemon darf die optionalen Felder im Welcome daher auslassen.
    internal bool Matches(EffectiveDaemonConfiguration? other) =>
        other is not null
        && MaxProjects == other.MaxProjects
        && IdleExitMinutes == other.IdleExitMinutes
        && OptionalMatches(ExternalMaxDiskBytes, other.ExternalMaxDiskBytes)
        && OptionalMatches(ExternalMaxMemoryBytes, other.ExternalMaxMemoryBytes)
        && OptionalMatches(ExternalMaxParallelOperations, other.ExternalMaxParallelOperations)
        && OptionalMatches(ExternalMaxResidentResources, other.ExternalMaxResidentResources)
        && OptionalMatches(ExternalIdleTtlMinutes, other.ExternalIdleTtlMinutes);

    // Serverseitig beschreibt ein fehlendes optionales Feld einen alten Client.
    // Neue Clients mit einem expliziten Limit werden dagegen vollständig
    // verglichen und erhalten den bestehenden Divergenz-Warnpfad.
    internal bool MatchesAdvertisedPeer(EffectiveDaemonConfiguration? other) =>
        other is not null
        && MaxProjects == other.MaxProjects
        && IdleExitMinutes == other.IdleExitMinutes
        && OptionalMatches(other.ExternalMaxDiskBytes, ExternalMaxDiskBytes)
        && OptionalMatches(other.ExternalMaxMemoryBytes, ExternalMaxMemoryBytes)
        && OptionalMatches(other.ExternalMaxParallelOperations, ExternalMaxParallelOperations)
        && OptionalMatches(other.ExternalMaxResidentResources, ExternalMaxResidentResources)
        && OptionalMatches(other.ExternalIdleTtlMinutes, ExternalIdleTtlMinutes);

    private static bool OptionalMatches<T>(T? expected, T? received)
        where T : struct => expected is null || expected.Value.Equals(received);
}

internal sealed record DaemonIdentity(
    string DaemonVersion,
    string ExecutableVersion,
    int ProcessId);

internal sealed record DaemonHello(
    string ExecutableVersion,
    int ProcessId,
    EffectiveDaemonConfiguration? Configuration,
    int ProtocolVersion = DaemonProtocol.Version)
{
    [JsonPropertyName("type")]
    public string Type => DaemonProtocol.Hello;
}

internal sealed record DaemonWelcome(
    string DaemonVersion,
    string ExecutableVersion,
    int ProcessId,
    EffectiveDaemonConfiguration Configuration,
    int ProtocolVersion = DaemonProtocol.Version)
{
    [JsonPropertyName("type")]
    public string Type => DaemonProtocol.Welcome;

    public int ConnectionId { get; init; }
}

internal sealed record DaemonShutdown(string Reason)
{
    [JsonPropertyName("type")]
    public string Type => DaemonProtocol.Shutdown;
}

internal sealed record DaemonError(string Code, string Message);

internal sealed record DaemonConfigurationDivergence(
    EffectiveDaemonConfiguration Expected,
    EffectiveDaemonConfiguration? Received,
    string Code = DaemonProtocol.ConfigurationDivergence);

internal enum DaemonHandshakeStatus
{
    Accepted,
    ProtocolRejected,
    ShutdownRequested,
    VersionConflict,
}

internal sealed record DaemonHandshakeResult(
    DaemonHandshakeStatus Status,
    DaemonWelcome? Welcome = null,
    DaemonShutdown? Shutdown = null,
    DaemonError? Error = null,
    DaemonConfigurationDivergence? ConfigurationDivergence = null)
{
    internal bool IsAccepted => Status == DaemonHandshakeStatus.Accepted;
}
