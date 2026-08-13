#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// Deterministische Verträge für das Prozesslebensdauerbudget ohne echten Serverprozess.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpTestClientRetryTests
{
    [Fact]
    public async Task ConnectAsync_AllRetriesExhausted_ThrowsInvalidOperationException()
    {
        var delays = new List<TimeSpan>();
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpProcessHost.ConnectWithRetryAsync(
                _ =>
                {
                    attempts++;
                    return Task.FromException<string>(new FileNotFoundException("simulierter Connect-Fehler"));
                },
                new McpConnectRetryOptions(MaxRetries: 1, BaseDelayMs: 10, BackoffFactor: 1.0),
                (delay, _) =>
                {
                    delays.Add(delay);
                    return Task.CompletedTask;
                }));

        Assert.Equal(2, attempts);
        Assert.Equal([TimeSpan.FromMilliseconds(10)], delays);
        Assert.Contains("MCP-Client-Connect scheiterte", exception.Message);
        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    [Fact]
    public async Task AcquireAsync_TwoLeasesBlockThirdUntilDisposal()
    {
        var gate = new McpProcessLifetimeGate(2);
        using var first = await gate.AcquireAsync(CancellationToken.None);
        using var second = await gate.AcquireAsync(CancellationToken.None);
        var third = gate.AcquireAsync(CancellationToken.None);

        Assert.False(third.IsCompleted);
        first.Dispose();
        using var released = await third.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
