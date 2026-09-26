#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using Microsoft.CodeAnalysis;
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
        return Start(scan, candidates, null);
    }

    internal CallToolResult Start(DeadCodeScanResult scan, VerifyAdvisorySnapshotContext context)
    {
        var candidates = GetVerifyAdvisoriesTool.SortCandidates(scan);
        return Start(scan, candidates, context);
    }

    private CallToolResult Start(DeadCodeScanResult scan, DeadCodeEntry[] candidates, VerifyAdvisorySnapshotContext? context)
    {
        var snapshotScan = scan with { DeadSymbols = candidates };
        var id = Guid.NewGuid();
        var page = GetVerifyAdvisoriesTool.RenderPage(snapshotScan, candidates, 0, id);
        if (page.IsError == true) return page;

        lock (gate)
        {
            RemoveExpired();
            snapshots.Add(id, new Snapshot(snapshotScan, candidates, context, DateTimeOffset.UtcNow));
        }
        return page;
    }

    internal string Capture(DeadCodeScanResult scan, VerifyAdvisorySnapshotContext context)
    {
        var candidates = GetVerifyAdvisoriesTool.SortCandidates(scan);
        var id = Guid.NewGuid();
        lock (gate)
        {
            RemoveExpired();
            snapshots.Add(id, new Snapshot(
                scan with { DeadSymbols = candidates },
                candidates,
                context,
                DateTimeOffset.UtcNow));
        }
        return $"{id:N}:0";
    }

    internal CallToolResult? TryReuse(VerifyAdvisorySnapshotContext context)
    {
        lock (gate)
        {
            RemoveExpired();
            var match = snapshots
                .Where(pair => pair.Value.Context is { } snapshotContext
                    && snapshotContext.Scope == context.Scope
                    && snapshotContext.SolutionVersion == context.SolutionVersion
                    && ReferenceEquals(snapshotContext.ConfigIdentity, context.ConfigIdentity)
                    && pair.Value.Scan.Summary.Status == "complete"
                    && !pair.Value.Scan.IsTruncated)
                .OrderByDescending(pair => pair.Value.LastAccess)
                .FirstOrDefault();
            if (match.Value is null) return null;

            var snapshot = match.Value with { LastAccess = DateTimeOffset.UtcNow };
            snapshots[match.Key] = snapshot;
            return GetVerifyAdvisoriesTool.RenderPage(snapshot.Scan, snapshot.Candidates, 0, match.Key);
        }
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
                || offset < 0 || offset > snapshot.Candidates.Length)
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

    private sealed record Snapshot(
        DeadCodeScanResult Scan,
        DeadCodeEntry[] Candidates,
        VerifyAdvisorySnapshotContext? Context,
        DateTimeOffset LastAccess);

}

internal sealed record VerifyAdvisorySnapshotContext(VersionStamp SolutionVersion, object ConfigIdentity, string Scope);
