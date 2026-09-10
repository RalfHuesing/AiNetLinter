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
    private readonly Func<string, DaemonRegistryLeaseResult>? projectLeaseProvider;

    internal DaemonRuntimeContext(
        int connectionId,
        Func<DaemonRuntimeSnapshot> snapshotProvider,
        Func<string, ProjectSnapshot?>? projectSnapshotProvider = null,
        Func<string, DaemonRegistryLeaseResult>? projectLeaseProvider = null)
    {
        ArgumentNullException.ThrowIfNull(snapshotProvider);
        ConnectionId = connectionId;
        this.snapshotProvider = snapshotProvider;
        this.projectSnapshotProvider = projectSnapshotProvider;
        this.projectLeaseProvider = projectLeaseProvider;
    }

    internal int ConnectionId { get; }

    internal string Mode => "daemon";

    internal DaemonRuntimeSnapshot Snapshot => snapshotProvider();

    internal ProjectSnapshot? FindProjectSnapshot(string analysisRoot) =>
        projectSnapshotProvider?.Invoke(analysisRoot);

    internal DaemonRegistryLeaseResult? LeaseProject(string analysisRoot) =>
        projectLeaseProvider?.Invoke(analysisRoot);
}
