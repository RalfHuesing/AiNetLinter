#nullable enable

using AiNetLinter.Mcp.Projects;

namespace AiNetLinter.Mcp.Daemon;

internal sealed record DaemonRuntimeSnapshot(
    int Connections,
    int ProcessId,
    TimeSpan Uptime,
    IReadOnlyList<string> Keys,
    string DaemonVersion,
    string? DaemonProfile = null);

internal sealed class DaemonRuntimeContext
{
    private readonly Func<DaemonRuntimeSnapshot> snapshotProvider;
    private readonly Func<string, ProjectSnapshot?>? projectSnapshotProvider;

    internal DaemonRuntimeContext(
        int connectionId,
        Func<DaemonRuntimeSnapshot> snapshotProvider,
        Func<string, ProjectSnapshot?>? projectSnapshotProvider = null)
    {
        ArgumentNullException.ThrowIfNull(snapshotProvider);
        ConnectionId = connectionId;
        this.snapshotProvider = snapshotProvider;
        this.projectSnapshotProvider = projectSnapshotProvider;
    }

    internal int ConnectionId { get; }

    internal string Mode => "daemon";

    internal DaemonRuntimeSnapshot Snapshot => snapshotProvider();

    internal ProjectSnapshot? FindProjectSnapshot(string projectRoot) =>
        projectSnapshotProvider?.Invoke(projectRoot);
}
