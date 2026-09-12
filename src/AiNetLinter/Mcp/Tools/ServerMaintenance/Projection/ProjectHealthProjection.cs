#nullable enable

using AiNetLinter.Mcp.Projects;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance.Projection;

internal static class ProjectHealthProjection
{
    internal static TargetProjectHealthEntry ToTargetEntry(ProjectSnapshot snapshot)
    {
        var entry = FromSnapshot(snapshot);
        return new TargetProjectHealthEntry(
            entry.TargetPath,
            entry.LoadState,
            entry.SolutionPath,
            entry.ConfigPath,
            entry.LastUsedUtc,
            entry.RefreshCount,
            entry.StalenessCheckCount,
            entry.StalenessCheckDurationMs,
            entry.StalenessWarningCount,
            entry.LastStalenessWarning,
            entry.LastGoodStateUtc,
            entry.LastLoadError);
    }

    internal static ProjectHealthEntry FromSnapshot(ProjectSnapshot snapshot)
    {
        var server = snapshot.Server;
        var (_, resolvedConfigPath) = server.GetConfigSnapshot();
        var staleness = server.LastStalenessStats;
        return new ProjectHealthEntry(
            TargetPath: snapshot.Definition.SolutionPath,
            LoadState: server.LoadState.ToString(),
            SolutionPath: server.LoadState == ServerLoadState.Loading ? null : server.GetCurrentSolution()?.FilePath,
            ConfigPath: resolvedConfigPath,
            LastUsedUtc: snapshot.LastUsedUtc,
            UptimeSeconds: server.Uptime.TotalSeconds,
            RefreshCount: server.RefreshCount,
            StalenessCheckCount: staleness.CheckCount,
            StalenessCheckDurationMs: staleness.TotalMilliseconds,
            StalenessWarningCount: staleness.WarningCount,
            LastStalenessWarning: staleness.LastWarning,
            LastGoodStateUtc: server.LastGoodStateUtc,
            LastLoadError: server.LastLoadError);
    }
}
