#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;
using static AiNetLinter.IntegrationTests.Mcp.Assemblies.ExternalSourceGitProcessTestSupport;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies;

[Trait("Category", "Integration")]
// @covers ExternalSourceGitProcessExecutor
// @covers ExternalSourceGitProcessNativeJob
public sealed class ExternalSourceGitProcessExecutorTests
{
    private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);
    private const int AccessDeniedErrorCode = 5;
    private const uint WaitFailedStatus = uint.MaxValue;

    [Fact]
    public async Task ExecuteAsync_UsesRealProcessStartInfoAndIsolatesEnvironment()
    {
        using var temp = TestTempDirectory.Create("git-process-real-probe-");
        var workingDirectory = temp.CreateSubdirectory("working directory");
        var scriptPath = temp.CreateFile(
            "probe.ps1",
            ExternalSourceGitProcessTestScripts.ProbeScript);
        var argument = "argument with spaces & $() 'quotes'";
        var inheritedName = "GIT_AINETLINTER_INHERITED";
        var explicitName = "GIT_AINETLINTER_EXPLICIT";
        var previousInherited = Environment.GetEnvironmentVariable(inheritedName);

        await EnvironmentLock.WaitAsync();
        try
        {
            Environment.SetEnvironmentVariable(inheritedName, "ambient-marker");
            var request = new ExternalSourceGitProcessRequest(
                ShellExecutable,
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", scriptPath, "probe", argument],
                workingDirectory,
                TimeSpan.FromSeconds(15),
                new Dictionary<string, string>
                {
                    [explicitName] = "explicit-marker",
                    ["AINETLINTER_TEST_MARKER"] = "request-marker",
                });
            var startInfo = ExternalSourceGitProcessExecutor.CreateStartInfo(request);

            var result = await new ExternalSourceGitProcessExecutor().ExecuteAsync(request);

            Assert.False(startInfo.UseShellExecute);
            Assert.True(startInfo.RedirectStandardOutput);
            Assert.True(startInfo.RedirectStandardError);
            Assert.False(startInfo.RedirectStandardInput);
            Assert.Equal(request.WorkingDirectory, startInfo.WorkingDirectory);
            Assert.Equal(request.Arguments, startInfo.ArgumentList);
            Assert.DoesNotContain(inheritedName, startInfo.Environment.Keys);
            Assert.Equal("explicit-marker", startInfo.Environment[explicitName]);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"ARG={argument}", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("ambient-marker", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"WORKING={workingDirectory}", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("GIT_INHERITED=", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("GIT_EXPLICIT=explicit-marker", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("REQUEST_MARKER=request-marker", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("STDERR=stderr-marker", result.StandardError, StringComparison.Ordinal);
            Assert.Equal(Console.IsInputRedirected, ReadBooleanMarker(result.StandardOutput, "STDIN_REDIRECTED"));
            Assert.False(result.StandardOutputTruncated);
            Assert.False(result.StandardErrorTruncated);
        }
        finally
        {
            Environment.SetEnvironmentVariable(inheritedName, previousInherited);
            EnvironmentLock.Release();
        }
    }

    [Fact]
    public async Task ExecuteAsync_BoundsCapturedOutputAndMarksTruncation()
    {
        using var temp = TestTempDirectory.Create("git-process-output-limit-");
        var scriptPath = temp.CreateFile(
            "probe.ps1",
            ExternalSourceGitProcessTestScripts.ProbeScript);
        var request = new ExternalSourceGitProcessRequest(
            ShellExecutable,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", scriptPath, "large", "unused"],
            temp.DirectoryPath,
            TimeSpan.FromSeconds(15),
            new Dictionary<string, string>());

        var result = await new ExternalSourceGitProcessExecutor().ExecuteAsync(request);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.StandardOutputTruncated);
        Assert.True(result.StandardErrorTruncated);
        Assert.True(result.StandardOutput.Length <= ExternalSourceGitProcessExecutor.OutputCaptureLimit);
        Assert.True(result.StandardError.Length <= ExternalSourceGitProcessExecutor.OutputCaptureLimit);
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutKillsLocalChildAndGrandchild()
    {
        using var temp = TestTempDirectory.Create("git-process-timeout-tree-");
        var scriptPath = CreateProcessTreeScripts(temp);
        var markerPath = temp.GetPath("timeout-pids.txt");
        var request = CreateProcessTreeRequest(scriptPath, temp.DirectoryPath, markerPath, 2_000);
        var started = await StartAndReadProcessIdsAsync(request, markerPath);

        try
        {
            var result = await started.Operation.WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(result.WasTimedOut);
            await WaitForProcessesToExitAsync(started.ProcessIds);
        }
        finally
        {
            await TerminateProcessesAsync(started.ProcessIds);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CancellationKillsLocalChildAndGrandchildAndPreservesToken()
    {
        using var temp = TestTempDirectory.Create("git-process-cancel-tree-");
        var scriptPath = CreateProcessTreeScripts(temp);
        var markerPath = temp.GetPath("cancel-pids.txt");
        using var cancellation = new CancellationTokenSource();
        var request = CreateProcessTreeRequest(scriptPath, temp.DirectoryPath, markerPath, 15_000);
        var started = await StartAndReadProcessIdsAsync(request, markerPath, cancellation.Token);

        try
        {
            cancellation.Cancel();

            var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                started.Operation.WaitAsync(TimeSpan.FromSeconds(15)));

            Assert.Equal(cancellation.Token, exception.CancellationToken);
            await WaitForProcessesToExitAsync(started.ProcessIds);
        }
        finally
        {
            await TerminateProcessesAsync(started.ProcessIds);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PostCreateOwnershipFailureUsesBoundedFallback()
    {
        using var temp = TestTempDirectory.Create("git-process-start-failure-");
        var markerPath = temp.GetPath("start-marker.txt");
        var scriptPath = temp.CreateFile(
            "start-marker.ps1",
            ExternalSourceGitProcessTestScripts.StartMarkerScript);
        var request = new ExternalSourceGitProcessRequest(
            ShellExecutable,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", scriptPath, markerPath],
            temp.DirectoryPath,
            TimeSpan.FromSeconds(15),
            new Dictionary<string, string>());
        var assignCalls = 0;
        var terminateCalls = 0;
        var waitCalls = 0;
        var forcedWaitFailures = 0;
        var observedProcessId = 0;
        var runtimeOperations = ExternalSourceGitProcessNativeOperations.Runtime;
        var operations = new ExternalSourceGitProcessNativeOperations(
            (_, _) =>
            {
                Interlocked.Increment(ref assignCalls);
                return new(false, AccessDeniedErrorCode);
            },
            (_, _) =>
            {
                Interlocked.Increment(ref terminateCalls);
                return new(false, AccessDeniedErrorCode);
            },
            (handle, milliseconds) =>
            {
                Interlocked.Increment(ref waitCalls);
                if (Interlocked.CompareExchange(ref forcedWaitFailures, 1, 0) == 0)
                {
                    return new(WaitFailedStatus, AccessDeniedErrorCode);
                }

                return runtimeOperations.WaitForSingleObject(handle, milliseconds);
            },
            processInformation =>
                Volatile.Write(
                    ref observedProcessId,
                    checked((int)processInformation.processId)));

        try
        {
            var exception = await Assert.ThrowsAsync<Win32Exception>(() =>
                new ExternalSourceGitProcessExecutor().ExecuteWithNativeOperationsAsync(
                    request,
                    operations));

            Assert.Contains("AssignProcessToJobObject", exception.Message, StringComparison.Ordinal);
            Assert.True(observedProcessId > 0);
            Assert.True(assignCalls > 0);
            Assert.True(terminateCalls > 0);
            Assert.True(waitCalls >= 2);
            Assert.True(exception.Data.Values.Cast<object>().Any(value => value is Exception));
            await WaitForProcessesToExitAsync([observedProcessId]);
            Assert.False(IsProcessRunning(observedProcessId));
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            if (observedProcessId > 0)
            {
                await TerminateProcessesAsync([observedProcessId]);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ResumedParentExitRequiresJobProofAndReportsCloseFailure()
    {
        using var temp = TestTempDirectory.Create("git-process-resumed-tree-");
        var scriptPath = CreateProcessTreeScripts(temp);
        var markerPath = temp.GetPath("resumed-pids.txt");
        var request = CreateProcessTreeRequest(scriptPath, temp.DirectoryPath, markerPath, 15_000);
        var runtimeOperations = ExternalSourceGitProcessNativeOperations.Runtime;
        var observedProcessId = 0;
        var closeCalls = 0;
        var operations = new ExternalSourceGitProcessNativeOperations(
            (job, handle) => runtimeOperations.AssignProcessToJobObject(job, handle),
            (handle, exitCode) => runtimeOperations.TerminateProcess(handle, exitCode),
            (_, _) => new(WaitFailedStatus, AccessDeniedErrorCode),
            processInformation =>
                Volatile.Write(
                    ref observedProcessId,
                    checked((int)processInformation.processId))
        )
        {
            ProcessResumed = processInformation =>
            {
                var parentWait = runtimeOperations.WaitForSingleObject(
                    processInformation.hProcess,
                    5_000);
                Assert.Equal(0u, parentWait.Status);
                throw new InvalidOperationException("Erzwungener Post-Create-Fehler.");
            },
            TerminateJobObject = (_, _) => new(false, AccessDeniedErrorCode),
            CloseHandle = handle =>
            {
                Interlocked.Increment(ref closeCalls);
                runtimeOperations.CloseHandle(handle);
                return new(false, AccessDeniedErrorCode);
            },
        };

        var processIds = Array.Empty<int>();
        try
        {
            var exception = await Assert.ThrowsAsync<AggregateException>(() =>
                new ExternalSourceGitProcessExecutor().ExecuteWithNativeOperationsAsync(
                    request,
                    operations));

            Assert.True(observedProcessId > 0);
            processIds = await ReadProcessIdsAfterFailureAsync(markerPath);
            Assert.Equal(2, processIds.Length);
            Assert.Contains(observedProcessId, processIds);
            Assert.Contains(
                exception.Flatten().InnerExceptions,
                failure => failure is InvalidOperationException
                    && failure.Message.Contains("Post-Create-Fehler", StringComparison.Ordinal));
            Assert.Contains(
                exception.Flatten().InnerExceptions,
                failure => failure is Win32Exception
                    && failure.Message.Contains("Job-Handle", StringComparison.Ordinal));
            Assert.Equal(1, closeCalls);
            await WaitForProcessesToExitAsync(processIds);
            Assert.All(processIds, processId => Assert.False(IsProcessRunning(processId)));
        }
        finally
        {
            await TerminateProcessesAsync(
                processIds.Length > 0 ? processIds : [observedProcessId]);
        }
    }

    [Fact]
    public async Task ExecuteAsync_TreeScopeCloseFailureIsVisibleAfterRealTreeCleanup()
    {
        using var temp = TestTempDirectory.Create("git-process-tree-close-failure-");
        var scriptPath = CreateProcessTreeScripts(temp);
        var markerPath = temp.GetPath("close-failure-pids.txt");
        var request = CreateProcessTreeRequest(scriptPath, temp.DirectoryPath, markerPath, 2_000);
        var runtimeOperations = ExternalSourceGitProcessNativeOperations.Runtime;
        var closeCalls = 0;
        var operations = new ExternalSourceGitProcessNativeOperations(
            (job, handle) => runtimeOperations.AssignProcessToJobObject(job, handle),
            (handle, exitCode) => runtimeOperations.TerminateProcess(handle, exitCode),
            (handle, milliseconds) => runtimeOperations.WaitForSingleObject(handle, milliseconds))
        {
            CloseHandle = handle =>
            {
                Interlocked.Increment(ref closeCalls);
                runtimeOperations.CloseHandle(handle);
                return new(false, AccessDeniedErrorCode);
            },
        };
        var started = await StartAndReadProcessIdsAsync(request, markerPath, operations: operations);

        try
        {
            var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
                started.Operation.WaitAsync(TimeSpan.FromSeconds(15)));

            Assert.True(
                exception.ToString().Contains("Job-Handle", StringComparison.Ordinal),
                exception.ToString());
            Assert.Equal(1, closeCalls);
            await WaitForProcessesToExitAsync(started.ProcessIds);
            Assert.All(started.ProcessIds, processId => Assert.False(IsProcessRunning(processId)));
        }
        finally
        {
            await TerminateProcessesAsync(started.ProcessIds);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnrepresentableTimeoutBeforeProcessStart()
    {
        using var temp = TestTempDirectory.Create("git-process-timeout-preflight-");
        var markerPath = temp.GetPath("start-marker.txt");
        var scriptPath = temp.CreateFile(
            "start-marker.ps1",
            ExternalSourceGitProcessTestScripts.StartMarkerScript);
        var request = new ExternalSourceGitProcessRequest(
            ShellExecutable,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", scriptPath, markerPath],
            temp.DirectoryPath,
            TimeSpan.FromMilliseconds((double)uint.MaxValue + 1),
            new Dictionary<string, string>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ExternalSourceGitProcessExecutor().ExecuteAsync(request));

        Assert.False(File.Exists(markerPath));
    }

}
