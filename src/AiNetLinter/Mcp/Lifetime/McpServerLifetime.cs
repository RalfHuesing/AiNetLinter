#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Lifetime;

internal sealed class McpServerLifetime : IAsyncDisposable
{
    private readonly CancellationTokenSource linkedSource;
    private readonly ParentProcessWatchdog? watchdog;
    private int disposed;

    private McpServerLifetime(
        CancellationTokenSource linkedSource,
        ParentProcessWatchdog? watchdog)
    {
        this.linkedSource = linkedSource;
        this.watchdog = watchdog;
    }

    internal CancellationToken Token => linkedSource.Token;

    internal static McpServerLifetime Start(
        int? configuredParentProcessId,
        CancellationToken rootToken,
        Action<string>? report = null)
    {
        if (configuredParentProcessId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuredParentProcessId));
        }

        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(rootToken);
        try
        {
            var parentProcessId = configuredParentProcessId ?? ParentProcessDetector.TryGetParentProcessId(report);
            var watchdog = parentProcessId is { } id
                ? ParentProcessWatchdog.Start(id, linkedSource, report: report)
                : null;
            return new McpServerLifetime(linkedSource, watchdog);
        }
        catch
        {
            linkedSource.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        if (watchdog is not null)
        {
            await watchdog.DisposeAsync().ConfigureAwait(false);
        }

        linkedSource.Dispose();
    }
}
