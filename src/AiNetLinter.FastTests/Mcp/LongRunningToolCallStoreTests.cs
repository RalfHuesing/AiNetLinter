#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class LongRunningToolCallStoreTests
{
    [Fact]
    public async Task SlowTool_ReturnsRunningTokenThenOriginalResultWithoutRestartingWork()
    {
        var store = new LongRunningToolCallStore(TimeSpan.FromMilliseconds(20));
        var release = new TaskCompletionSource<CallToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var original = new CallToolResult { Content = [new TextContentBlock { Text = "final result" }] };
        var starts = 0;
        Task<CallToolResult> Start(CancellationToken _)
        {
            Interlocked.Increment(ref starts);
            return release.Task;
        }

        try
        {
            var firstCall = store.RunAsync(new LongRunningToolCallRequest(
                "verify", "C:/source/app.slnx", "solution", null, Start));
            var firstWinner = await Task.WhenAny(firstCall, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(firstCall, firstWinner);
            var pending = await firstCall;
            var text = Assert.IsType<TextContentBlock>(Assert.Single(pending.Content)).Text;
            Assert.Contains("operation=running", text, StringComparison.Ordinal);
            var token = text.Split('\n').Single(line => line.StartsWith("operationToken=", StringComparison.Ordinal))["operationToken=".Length..];

            release.SetResult(original);
            var final = await store.RunAsync(new LongRunningToolCallRequest(
                "verify", "C:/source/app.slnx", "solution", token, Start));
            Assert.Same(original, final);
            Assert.Equal(1, starts);
        }
        finally
        {
            release.TrySetResult(original);
        }
    }

    [Fact]
    public async Task PendingToken_RejectsOtherArgumentsWithoutStartingAnotherOperation()
    {
        var store = new LongRunningToolCallStore(TimeSpan.FromMilliseconds(10));
        var release = new TaskCompletionSource<CallToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var original = new CallToolResult { Content = [new TextContentBlock { Text = "verified" }] };
        var starts = 0;
        Task<CallToolResult> Start(CancellationToken _)
        {
            Interlocked.Increment(ref starts);
            return release.Task;
        }

        try
        {
            var pending = await store.RunAsync(new LongRunningToolCallRequest(
                "verify", "C:/source/app.slnx", "solution", null, Start));
            var text = Assert.IsType<TextContentBlock>(Assert.Single(pending.Content)).Text;
            var token = text.Split('\n').Single(line => line.StartsWith("operationToken=", StringComparison.Ordinal))["operationToken=".Length..];

            var wrongScope = await store.RunAsync(new LongRunningToolCallRequest(
                "verify", "C:/source/app.slnx", "changes", token, Start));
            Assert.True(wrongScope.IsError);
            Assert.Contains("operationToken", Assert.IsType<TextContentBlock>(Assert.Single(wrongScope.Content)).Text, StringComparison.Ordinal);
            Assert.Equal(1, starts);

            release.SetResult(original);
            var final = await store.RunAsync(new LongRunningToolCallRequest(
                "verify", "C:/source/app.slnx", "solution", token, Start));
            Assert.Same(original, final);
        }
        finally
        {
            release.TrySetResult(original);
        }
    }

    [Fact]
    public async Task AbandonedRunningOperation_IsCanceledAfterIdleLimit()
    {
        var store = new LongRunningToolCallStore(TimeSpan.FromMilliseconds(10),
            runningIdleTtl: TimeSpan.FromMilliseconds(80));
        var wasCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<CallToolResult> Start(CancellationToken token)
        {
            using var registration = token.Register(() => wasCanceled.TrySetResult());
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Der Lauf muss abgebrochen werden.");
        }

        var pending = await store.RunAsync(new LongRunningToolCallRequest(
            "verify", "C:/source/app.slnx", "solution", null, Start));
        var text = Assert.IsType<TextContentBlock>(Assert.Single(pending.Content)).Text;
        var token = text.Split('\n').Single(line => line.StartsWith("operationToken=", StringComparison.Ordinal))["operationToken=".Length..];

        await wasCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var expired = await store.RunAsync(new LongRunningToolCallRequest(
            "verify", "C:/source/app.slnx", "solution", token, Start));

        Assert.True(expired.IsError);
    }

    [Fact]
    public async Task PendingAnalysis_DoesNotBlockAnIndependentToolCall()
    {
        var store = new LongRunningToolCallStore(TimeSpan.FromMilliseconds(10));
        var release = new TaskCompletionSource<CallToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var quickResult = new CallToolResult { Content = [new TextContentBlock { Text = "other tool" }] };

        try
        {
            var pending = await store.RunAsync(new LongRunningToolCallRequest(
                "verify", "C:/source/app.slnx", "solution", null, _ => release.Task));
            Assert.Contains("operation=running",
                Assert.IsType<TextContentBlock>(Assert.Single(pending.Content)).Text,
                StringComparison.Ordinal);

            var other = await store.RunAsync(new LongRunningToolCallRequest(
                "get_hotspots", "C:/source/app.slnx", "", null, _ => Task.FromResult(quickResult)));
            Assert.Same(quickResult, other);
        }
        finally
        {
            release.TrySetResult(quickResult);
        }
    }

    [Fact]
    public async Task TooManyActiveAnalyses_RejectsFifthStartWithoutStoppingExistingJobs()
    {
        var store = new LongRunningToolCallStore(TimeSpan.FromMilliseconds(10));
        var release = new TaskCompletionSource<CallToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new CallToolResult { Content = [new TextContentBlock { Text = "done" }] };

        try
        {
            for (var index = 0; index < 4; index++)
            {
                var pending = await store.RunAsync(new LongRunningToolCallRequest(
                    "verify", "C:/source/app.slnx", index.ToString(), null, _ => release.Task));
                Assert.False(pending.IsError == true);
            }

            var fifth = await store.RunAsync(new LongRunningToolCallRequest(
                "verify", "C:/source/app.slnx", "fifth", null, _ => release.Task));
            Assert.True(fifth.IsError);
            Assert.Contains("TOO_MANY_OPERATIONS",
                Assert.IsType<TextContentBlock>(Assert.Single(fifth.Content)).Text,
                StringComparison.Ordinal);
        }
        finally
        {
            release.TrySetResult(completed);
        }
    }

    [Fact]
    public async Task CanceledFirstRequest_CancelsWorkBeforeAnyTokenWasDelivered()
    {
        var store = new LongRunningToolCallStore(TimeSpan.FromSeconds(1));
        using var requestCancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workerCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<CallToolResult> Start(CancellationToken token)
        {
            started.TrySetResult();
            using var registration = token.Register(() => workerCanceled.TrySetResult());
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Der Lauf muss abgebrochen werden.");
        }

        var call = store.RunAsync(new LongRunningToolCallRequest(
            "verify", "C:/source/app.slnx", "solution", null, Start), requestCancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        requestCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await workerCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
