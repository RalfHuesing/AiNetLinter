#nullable enable

using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance.Projection;

internal static class DaemonHealthProjection
{
    internal static DaemonHealthPayload FromContext(DaemonRuntimeContext context)
    {
        var snapshot = context.Snapshot;
        return new DaemonHealthPayload(
            context.Mode,
            context.ConnectionId,
            snapshot.Connections,
            snapshot.ProcessId,
            snapshot.Uptime.TotalSeconds,
            snapshot.Keys,
            snapshot.DaemonVersion,
            snapshot.DaemonProfile);
    }
}
