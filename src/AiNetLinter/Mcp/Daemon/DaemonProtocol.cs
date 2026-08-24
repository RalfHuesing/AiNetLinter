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
    internal const string DefaultLogTarget = "stderr";

    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    internal static string CurrentUserName => Environment.UserName;

    internal static string GetPipeName(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        return PipeNamePrefix + userName;
    }
}

internal sealed record EffectiveDaemonConfiguration(
    int MaxProjects,
    decimal IdleExitMinutes,
    string LogTarget)
{
    internal static EffectiveDaemonConfiguration Default { get; } = new(
        DaemonProtocol.DefaultMaxProjects,
        DaemonProtocol.DefaultIdleExitMinutes,
        DaemonProtocol.DefaultLogTarget);

    internal bool Matches(EffectiveDaemonConfiguration? other) =>
        other is not null && this == other;
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
