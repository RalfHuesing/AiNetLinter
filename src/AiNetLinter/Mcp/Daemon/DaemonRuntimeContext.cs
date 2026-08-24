#nullable enable

namespace AiNetLinter.Mcp.Daemon;

internal sealed record DaemonRuntimeSnapshot(
    int Connections,
    int ProcessId,
    TimeSpan Uptime,
    IReadOnlyList<string> Keys,
    string DaemonVersion);

internal sealed class DaemonRuntimeContext
{
    private readonly Func<DaemonRuntimeSnapshot> snapshotProvider;

    internal DaemonRuntimeContext(int connectionId, Func<DaemonRuntimeSnapshot> snapshotProvider)
    {
        ArgumentNullException.ThrowIfNull(snapshotProvider);
        ConnectionId = connectionId;
        this.snapshotProvider = snapshotProvider;
    }

    internal int ConnectionId { get; }

    internal string Mode => "daemon";

    internal DaemonRuntimeSnapshot Snapshot => snapshotProvider();
}
