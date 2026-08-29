#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class ExternalSourceGitProcessExecutor : IExternalSourceGitProcessExecutor
{
    internal const int OutputCaptureLimit = 64 * 1024;

    private const int OutputReadBufferSize = 4096;
    private static readonly TimeSpan ProcessTerminationTimeout = TimeSpan.FromSeconds(5);

    public Task<ExternalSourceGitProcessResult> ExecuteAsync(
        ExternalSourceGitProcessRequest request,
        CancellationToken cancellationToken = default)
        => ExecuteAsyncCore(
            request,
            cancellationToken,
            ExternalSourceGitProcessNativeOperations.Runtime);

    internal Task<ExternalSourceGitProcessResult> ExecuteWithNativeOperationsAsync(
        ExternalSourceGitProcessRequest request,
        ExternalSourceGitProcessNativeOperations operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return ExecuteAsyncCore(request, cancellationToken, operations);
    }

    private static async Task<ExternalSourceGitProcessResult> ExecuteAsyncCore(
        ExternalSourceGitProcessRequest request,
        CancellationToken cancellationToken,
        ExternalSourceGitProcessNativeOperations operations)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var timeoutSource = new CancellationTokenSource(request.Timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        var startInfo = CreateStartInfo(request);
        ExternalSourceGitProcessTreeScope? scope = null;
        try
        {
            scope = ExternalSourceGitProcessTreeScope.Start(startInfo, operations);
            return await ExecuteStartedProcessAsync(
                    scope,
                    timeoutSource,
                    linkedCancellation,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private static async Task<ExternalSourceGitProcessResult> ExecuteStartedProcessAsync(
        ExternalSourceGitProcessTreeScope scope,
        CancellationTokenSource timeoutSource,
        CancellationTokenSource linkedCancellation,
        CancellationToken cancellationToken)
    {
        var execution = new ProcessExecutionState();
        try
        {
            execution.StartOutputReaders(scope, linkedCancellation.Token);
            await scope.Process.WaitForExitAsync(linkedCancellation.Token).ConfigureAwait(false);
            var output = await WaitForOutputAsync(execution, linkedCancellation.Token)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (timeoutSource.IsCancellationRequested)
            {
                return await CompleteTimeoutAsync(scope, execution, linkedCancellation)
                    .ConfigureAwait(false);
            }

            return new ExternalSourceGitProcessResult(
                scope.Process.ExitCode,
                output.StandardOutput.Text,
                output.StandardError.Text,
                new ExternalSourceGitProcessResultOptions
                {
                    StandardOutputTruncated = output.StandardOutput.IsTruncated,
                    StandardErrorTruncated = output.StandardError.IsTruncated,
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await CompleteCallerCancellationAsync(
                    scope,
                    execution,
                    linkedCancellation,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return await CompleteTimeoutAsync(scope, execution, linkedCancellation)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return await RethrowPrimaryFailureAsync(
                    scope,
                    execution,
                    linkedCancellation,
                    exception)
                .ConfigureAwait(false);
        }
    }

    private static async Task<ExternalSourceGitProcessResult> CompleteCallerCancellationAsync(
        ExternalSourceGitProcessTreeScope scope,
        ProcessExecutionState execution,
        CancellationTokenSource cancellation,
        CancellationToken callerToken)
    {
        var cleanup = await CleanupProcessAsync(scope, execution, cancellation)
            .ConfigureAwait(false);
        if (cleanup.Failure is not null)
        {
            throw new OperationCanceledException(
                "Der Git-Prozess konnte nicht kontrolliert beendet werden.",
                cleanup.Failure,
                callerToken);
        }

        throw new OperationCanceledException(callerToken);
    }

    private static async Task<ExternalSourceGitProcessResult> CompleteTimeoutAsync(
        ExternalSourceGitProcessTreeScope scope,
        ProcessExecutionState execution,
        CancellationTokenSource cancellation)
    {
        var cleanup = await CleanupProcessAsync(scope, execution, cancellation)
            .ConfigureAwait(false);
        if (cleanup.Failure is not null)
        {
            throw new TimeoutException(
                "Der Git-Prozess konnte nicht kontrolliert beendet werden.",
                cleanup.Failure);
        }

        return new ExternalSourceGitProcessResult(
            exitCode: -1,
            cleanup.Output.StandardOutput.Text,
            cleanup.Output.StandardError.Text,
            new ExternalSourceGitProcessResultOptions
            {
                WasTimedOut = true,
                StandardOutputTruncated = cleanup.Output.StandardOutput.IsTruncated,
                StandardErrorTruncated = cleanup.Output.StandardError.IsTruncated,
            });
    }

    private static async Task<ExternalSourceGitProcessResult> RethrowPrimaryFailureAsync(
        ExternalSourceGitProcessTreeScope scope,
        ProcessExecutionState execution,
        CancellationTokenSource cancellation,
        Exception primaryException)
    {
        var cleanup = await CleanupProcessAsync(
                scope,
                execution,
                cancellation)
            .ConfigureAwait(false);
        if (cleanup.Failure is not null)
        {
            ExternalSourceGitProcessStartFailureCleanup.AttachCleanupFailure(
                primaryException,
                cleanup.Failure);
        }

        ExceptionDispatchInfo.Capture(primaryException).Throw();
        throw new InvalidOperationException("Der Git-Prozess ist ohne Ergebnis beendet worden.");
    }

    private static async Task<ProcessOutput> WaitForOutputAsync(
        ProcessExecutionState execution,
        CancellationToken cancellationToken)
    {
        var output = Task.WhenAll(execution.StandardOutput, execution.StandardError);
        try
        {
            var captured = await output
                .WaitAsync(ProcessTerminationTimeout, cancellationToken)
                .ConfigureAwait(false);
            return new(captured[0], captured[1]);
        }
        catch
        {
            ObserveCompletion(output);
            throw;
        }
    }

    private static async Task<ProcessOutputCapture> ReadOutputAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var buffer = new char[OutputReadBufferSize];
        var captured = new StringBuilder();
        var isTruncated = false;
        try
        {
            while (true)
            {
                var readCount = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                if (readCount == 0)
                {
                    break;
                }

                var remaining = OutputCaptureLimit - captured.Length;
                var captureCount = Math.Min(remaining, readCount);
                if (captureCount > 0)
                {
                    captured.Append(buffer, 0, captureCount);
                }

                if (captureCount < readCount)
                {
                    isTruncated = true;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            isTruncated |= captured.Length >= OutputCaptureLimit;
        }
        catch (Exception exception) when (
            cancellationToken.IsCancellationRequested
            && exception is ObjectDisposedException or IOException)
        {
            isTruncated |= captured.Length >= OutputCaptureLimit;
        }

        return new(captured.ToString(), isTruncated);
    }

    private static async Task<ProcessCleanupResult> CleanupProcessAsync(
        ExternalSourceGitProcessTreeScope scope,
        ProcessExecutionState execution,
        CancellationTokenSource cancellation)
    {
        var failures = new List<Exception>();
        TryCancelOutput(cancellation, failures);
        if (!scope.TryTerminate(failures))
        {
            failures.Add(new InvalidOperationException(
                "Der Git-Prozessbaum konnte nicht beendet werden."));
        }

        using var cleanupTimeout = new CancellationTokenSource(ProcessTerminationTimeout);
        scope.CloseOutputStreams(failures);
        await WaitForProcessExitAsync(scope.Process, cleanupTimeout.Token, failures)
            .ConfigureAwait(false);
        await WaitForReadersAsync(execution, cleanupTimeout.Token, failures)
            .ConfigureAwait(false);
        var output = new ProcessOutput(
            await GetCompletedOutputAsync(execution.StandardOutput).ConfigureAwait(false),
            await GetCompletedOutputAsync(execution.StandardError).ConfigureAwait(false));
        scope.Dispose(failures);
        return new(output, CombineFailures(failures));
    }

    private static void TryCancelOutput(
        CancellationTokenSource cancellation,
        ICollection<Exception> failures)
    {
        try
        {
            cancellation.Cancel();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static async Task WaitForProcessExitAsync(
        Process process,
        CancellationToken cancellationToken,
        ICollection<Exception> failures)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!TryGetHasExited(process, out var hasExited) || !hasExited)
            {
                failures.Add(new TimeoutException(
                    "Der Git-Prozess wurde innerhalb der Cleanup-Grenze nicht beendet."));
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static bool TryGetHasExited(Process process, out bool hasExited)
    {
        try
        {
            hasExited = process.HasExited;
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException)
        {
            hasExited = true;
            return true;
        }
        catch (Win32Exception)
        {
            hasExited = false;
            return false;
        }
    }

    private static async Task WaitForReadersAsync(
        ProcessExecutionState execution,
        CancellationToken cancellationToken,
        ICollection<Exception> failures)
    {
        var readers = Task.WhenAll(execution.StandardOutput, execution.StandardError);
        try
        {
            await readers.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObserveCompletion(readers);
            failures.Add(new TimeoutException(
                "Die Git-Ausgabepipes wurden innerhalb der Cleanup-Grenze nicht geschlossen."));
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static async Task<ProcessOutputCapture> GetCompletedOutputAsync(
        Task<ProcessOutputCapture> output)
    {
        if (!output.IsCompletedSuccessfully)
        {
            return ProcessOutputCapture.Empty;
        }

        return await output.ConfigureAwait(false);
    }

    private static Exception? CombineFailures(ICollection<Exception> failures) =>
        failures.Count switch
        {
            0 => null,
            1 => failures.First(),
            _ => new AggregateException("Die Prozessbereinigung ist fehlgeschlagen.", failures),
        };

    private static void ObserveCompletion<T>(Task<T> task)
    {
        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal static ProcessStartInfo CreateStartInfo(ExternalSourceGitProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
        };
        RemoveInheritedGitEnvironment(startInfo);
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var environmentVariable in request.Environment)
        {
            startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
        }

        return startInfo;
    }

    private static void RemoveInheritedGitEnvironment(ProcessStartInfo startInfo)
    {
        var inheritedGitVariables = new List<string>();
        foreach (var variableName in startInfo.Environment.Keys)
        {
            if (variableName.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase))
            {
                inheritedGitVariables.Add(variableName);
            }
        }

        foreach (var variableName in inheritedGitVariables)
        {
            startInfo.Environment.Remove(variableName);
        }
    }

    private sealed class ProcessExecutionState
    {
        internal Task<ProcessOutputCapture> StandardOutput { get; private set; } =
            Task.FromResult(ProcessOutputCapture.Empty);

        internal Task<ProcessOutputCapture> StandardError { get; private set; } =
            Task.FromResult(ProcessOutputCapture.Empty);

        internal void StartOutputReaders(
            ExternalSourceGitProcessTreeScope scope,
            CancellationToken cancellationToken)
        {
            StandardOutput = ReadOutputAsync(scope.StandardOutput, cancellationToken);
            StandardError = ReadOutputAsync(scope.StandardError, cancellationToken);
        }
    }

    private sealed record ProcessOutputCapture(string Text, bool IsTruncated)
    {
        internal static ProcessOutputCapture Empty { get; } = new(string.Empty, false);
    }

    private sealed record ProcessOutput(
        ProcessOutputCapture StandardOutput,
        ProcessOutputCapture StandardError);

    private sealed record ProcessCleanupResult(ProcessOutput Output, Exception? Failure);
}
