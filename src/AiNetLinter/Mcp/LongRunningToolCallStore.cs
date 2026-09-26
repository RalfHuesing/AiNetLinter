#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal sealed record LongRunningToolCallRequest(
    string ToolName,
    string TargetPath,
    string ArgumentsKey,
    string? OperationToken,
    Func<CancellationToken, Task<CallToolResult>> Operation);

internal sealed class LongRunningToolCallStore
{
    private static readonly TimeSpan CompletedIdleTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DefaultRunningIdleTtl = TimeSpan.FromMinutes(30);
    private const int MaxRunning = 4;
    private const int MaxCompleted = 32;
    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly TimeSpan responseWindow;
    private readonly TimeSpan runningIdleTtl;
    private readonly CancellationToken lifetimeToken;

    internal LongRunningToolCallStore(
        TimeSpan responseWindow,
        CancellationToken lifetimeToken = default,
        TimeSpan? runningIdleTtl = null)
    {
        if (responseWindow <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(responseWindow));
        this.responseWindow = responseWindow;
        this.lifetimeToken = lifetimeToken;
        this.runningIdleTtl = runningIdleTtl ?? DefaultRunningIdleTtl;
        if (this.runningIdleTtl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(runningIdleTtl));
    }

    internal async Task<CallToolResult> RunAsync(
        LongRunningToolCallRequest request,
        CancellationToken requestCancellationToken = default)
    {
        var resolution = ResolveOrStart(request);
        if (resolution.Error is not null) return resolution.Error;
        return await AwaitResultAsync(resolution.Entry!, request.OperationToken is null, requestCancellationToken)
            .ConfigureAwait(false);
    }

    private (Entry? Entry, CallToolResult? Error) ResolveOrStart(LongRunningToolCallRequest request)
    {
        lock (gate)
        {
            PruneCompleted(DateTimeOffset.UtcNow);
            Entry entry;
            if (request.OperationToken is not null)
            {
                if (!entries.TryGetValue(request.OperationToken, out entry!)
                    || !entry.Matches(request.ToolName, request.TargetPath, request.ArgumentsKey))
                {
                    return (null, McpToolResults.InvalidArgument(
                        "operationToken ist unbekannt oder gehört zu einem anderen Aufruf.",
                        "Den operationToken des identischen Toolaufrufs verwenden oder ohne Token neu starten.",
                        "$.operationToken"));
                }
                if (entry.WorkCancellation.IsCancellationRequested)
                {
                    return (null, McpToolResults.Recoverable(
                        "OPERATION_EXPIRED",
                        "Der lange MCP-Lauf wurde nach 30 Minuten ohne Abruf beendet.",
                        hint: "Den Toolaufruf ohne operationToken neu starten."));
                }
            }
            else
            {
                if (entries.Values.Count(candidate => !candidate.Work.IsCompleted) >= MaxRunning)
                {
                    return (null, McpToolResults.Recoverable(
                        "TOO_MANY_OPERATIONS",
                        "Es laufen bereits zu viele lange MCP-Analysen.",
                        hint: "Eine laufende Analyse mit ihrem operationToken fortsetzen und danach erneut starten."));
                }

                var token = Guid.NewGuid().ToString("N");
                var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
                workCancellation.CancelAfter(runningIdleTtl);
                var work = Task.Run(() => request.Operation(workCancellation.Token), CancellationToken.None);
                entry = new Entry(new EntryIdentity(token, request.ToolName, request.TargetPath, request.ArgumentsKey),
                    work, workCancellation);
                entries.Add(token, entry);
            }
            if (!entry.Work.IsCompleted) entry.WorkCancellation.CancelAfter(runningIdleTtl);
            entry.LastAccessUtc = DateTimeOffset.UtcNow;
            return (entry, null);
        }
    }

    private async Task<CallToolResult> AwaitResultAsync(
        Entry entry,
        bool startedHere,
        CancellationToken requestCancellationToken)
    {
        try
        {
            var result = await entry.Work.WaitAsync(responseWindow, requestCancellationToken).ConfigureAwait(false);
            if (startedHere && !entry.WasPending)
            {
                lock (gate) Remove(entry);
            }
            return result;
        }
        catch (OperationCanceledException) when (startedHere && requestCancellationToken.IsCancellationRequested)
        {
            entry.WorkCancellation.Cancel();
            throw;
        }
        catch (TimeoutException)
        {
            lock (gate) entry.WasPending = true;
            return new CallToolResult
            {
                Content = [new TextContentBlock
                {
                    Text = "Status: operation=running, completeness=not_applicable\n" +
                           $"operationToken={entry.Token}\n" +
                           "retry: denselben Toolaufruf mit operationToken wiederholen; die Analyse läuft weiter.",
                }],
            };
        }
    }

    private void PruneCompleted(DateTimeOffset now)
    {
        foreach (var entry in entries.Values.Where(candidate => candidate.Work.IsCompleted
                     && now - candidate.LastAccessUtc >= CompletedIdleTtl).ToArray())
        {
            Remove(entry);
        }

        var completed = entries.Values.Where(candidate => candidate.Work.IsCompleted)
            .OrderBy(candidate => candidate.LastAccessUtc).ToArray();
        foreach (var entry in completed.Take(Math.Max(0, completed.Length - MaxCompleted)))
        {
            Remove(entry);
        }
    }

    private void Remove(Entry entry)
    {
        entries.Remove(entry.Token);
        entry.WorkCancellation.Dispose();
    }

    private sealed record EntryIdentity(string Token, string ToolName, string TargetPath, string ArgumentsKey);

    private sealed class Entry(
        EntryIdentity identity,
        Task<CallToolResult> work,
        CancellationTokenSource workCancellation)
    {
        internal string Token => identity.Token;
        internal Task<CallToolResult> Work { get; } = work;
        internal CancellationTokenSource WorkCancellation { get; } = workCancellation;
        internal DateTimeOffset LastAccessUtc { get; set; }
        internal bool WasPending { get; set; }

        internal bool Matches(string requestedTool, string requestedTarget, string requestedArguments) =>
            string.Equals(identity.ToolName, requestedTool, StringComparison.Ordinal)
            && string.Equals(identity.TargetPath, requestedTarget, StringComparison.OrdinalIgnoreCase)
            && string.Equals(identity.ArgumentsKey, requestedArguments, StringComparison.Ordinal);
    }
}
