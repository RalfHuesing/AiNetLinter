#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.Verify;

internal sealed class VerifyAdvisoryPageStore
{
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromMinutes(30);
    private readonly Dictionary<Guid, Snapshot> snapshots = new();
    private readonly object gate = new();

    internal CallToolResult Start(DeadCodeScanResult scan)
    {
        var candidates = GetVerifyAdvisoriesTool.SortCandidates(scan);
        var snapshotScan = scan with { DeadSymbols = candidates };
        var id = Guid.NewGuid();
        var page = GetVerifyAdvisoriesTool.RenderPage(snapshotScan, candidates, 0, id);
        if (page.IsError == true || candidates.Length == 0) return page;

        lock (gate)
        {
            RemoveExpired();
            snapshots.Add(id, new Snapshot(snapshotScan, candidates, DateTimeOffset.UtcNow));
        }
        return page;
    }

    internal CallToolResult Continue(string continuationToken)
    {
        var pieces = continuationToken.Split(':');
        if (pieces.Length != 2 || !Guid.TryParseExact(pieces[0], "N", out var id)
            || !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
        {
            return InvalidToken();
        }

        lock (gate)
        {
            RemoveExpired();
            if (!snapshots.TryGetValue(id, out var snapshot)
                || offset <= 0 || offset >= snapshot.Candidates.Length)
            {
                return InvalidToken();
            }

            snapshots[id] = snapshot with { LastAccess = DateTimeOffset.UtcNow };
            return GetVerifyAdvisoriesTool.RenderPage(snapshot.Scan, snapshot.Candidates, offset, id);
        }
    }

    private void RemoveExpired()
    {
        var threshold = DateTimeOffset.UtcNow - SnapshotLifetime;
        foreach (var id in snapshots.Where(pair => pair.Value.LastAccess < threshold).Select(pair => pair.Key).ToArray())
        {
            snapshots.Remove(id);
        }
    }

    private static CallToolResult InvalidToken() => VerifyResponseFormatter.Error(
        "INVALID_CONTINUATION_TOKEN",
        "Das Fortsetzungstoken ist ungültig oder der Scan-Snapshot ist abgelaufen.",
        "get_verify_advisories ohne continuationToken erneut aufrufen und die neue Liste paginieren.",
        "$.continuationToken");

    private sealed record Snapshot(DeadCodeScanResult Scan, DeadCodeEntry[] Candidates, DateTimeOffset LastAccess);
}
