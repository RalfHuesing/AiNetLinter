#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies;

[Trait("Category", "Integration")]
// @covers ExternalSourceGitProcessExecutor
public sealed class ExternalSourceGitProcessExecutorTests
{
    private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);
    private static readonly TimeSpan ProcessObservationTimeout = TimeSpan.FromSeconds(10);
    private const int AccessDeniedErrorCode = 5;
    private const uint WaitFailedStatus = uint.MaxValue;

    [Fact]
    public async Task ExecuteAsync_UsesRealProcessStartInfoAndIsolatesEnvironment()
    {
        using var temp = TestTempDirectory.Create("git-process-real-probe-");
        var workingDirectory = temp.CreateSubdirectory("working directory");
        var scriptPath = temp.CreateFile("probe.ps1", ProbeScript);
        var argument = "argument with spaces & $() 'quotes'";
        var inheritedName = "GIT_AINETLINTER_INHERITED";
        var explicitName = "GIT_AINETLINTER_EXPLICIT";
        var previousInherited = Environment.GetEnvironmentVariable(inheritedName);

        await EnvironmentLock.WaitAsync();
        try
        {
            Environment.SetEnvironmentVariable(inheritedName, "ambient-marker");
            var request = new ExternalSourceGitProcessRequest(
                "pwsh",
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
        var scriptPath = temp.CreateFile("probe.ps1", ProbeScript);
        var request = new ExternalSourceGitProcessRequest(
            "pwsh",
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
        var scriptPath = temp.CreateFile("start-marker.ps1", StartMarkerScript);
        var request = new ExternalSourceGitProcessRequest(
            "pwsh",
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
    public async Task ExecuteAsync_RejectsUnrepresentableTimeoutBeforeProcessStart()
    {
        using var temp = TestTempDirectory.Create("git-process-timeout-preflight-");
        var markerPath = temp.GetPath("start-marker.txt");
        var scriptPath = temp.CreateFile("start-marker.ps1", StartMarkerScript);
        var request = new ExternalSourceGitProcessRequest(
            "pwsh",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", scriptPath, markerPath],
            temp.DirectoryPath,
            TimeSpan.FromMilliseconds((double)uint.MaxValue + 1),
            new Dictionary<string, string>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ExternalSourceGitProcessExecutor().ExecuteAsync(request));

        Assert.False(File.Exists(markerPath));
    }

    private static ExternalSourceGitProcessRequest CreateProcessTreeRequest(
        string scriptPath,
        string workingDirectory,
        string markerPath,
        int timeoutMilliseconds) =>
        new(
            "pwsh",
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", scriptPath, "tree", markerPath],
            workingDirectory,
            TimeSpan.FromMilliseconds(timeoutMilliseconds),
            new Dictionary<string, string>());

    private static async Task<(Task<ExternalSourceGitProcessResult> Operation, int[] ProcessIds)> StartAndReadProcessIdsAsync(
        ExternalSourceGitProcessRequest request,
        string markerPath,
        CancellationToken cancellationToken = default)
    {
        var operation = new ExternalSourceGitProcessExecutor().ExecuteAsync(request, cancellationToken);
        int[]? processIds = null;
        try
        {
            await TestWaiter.WaitForConditionAsync(
                () => TryReadProcessIds(markerPath, out processIds),
                ProcessObservationTimeout);
        }
        catch
        {
            if (operation.IsFaulted)
            {
                await operation;
            }

            throw;
        }
        Assert.NotNull(processIds);
        return (operation, processIds!);
    }

    private static async Task WaitForProcessesToExitAsync(int[] processIds) =>
        await TestWaiter.WaitForConditionAsync(
            () => AllProcessesHaveExited(processIds),
            ProcessObservationTimeout);

    private static bool TryReadProcessIds(string markerPath, out int[]? processIds)
    {
        processIds = null;
        try
        {
            if (!File.Exists(markerPath))
            {
                return false;
            }

            var lines = File.ReadAllLines(markerPath);
            if (lines.Length < 2
                || !int.TryParse(lines[0], out var parentId)
                || !int.TryParse(lines[1], out var childId))
            {
                return false;
            }

            processIds = [parentId, childId];
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task TerminateProcessesAsync(IEnumerable<int> processIds)
    {
        var knownProcessIds = processIds.Distinct().ToArray();
        var failures = new List<Exception>();
        foreach (var processId in knownProcessIds)
        {
            TryTerminateProcess(processId, failures);
        }

        try
        {
            await WaitForProcessesToExitAsync(knownProcessIds);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        foreach (var processId in knownProcessIds)
        {
            try
            {
                if (IsProcessRunning(processId))
                {
                    failures.Add(new InvalidOperationException(
                        $"Der Testprozess {processId} ist nach dem Cleanup noch aktiv."));
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Die Testprozess-Bereinigung konnte das Ende nicht nachweisen.",
                failures);
        }
    }

    private static bool AllProcessesHaveExited(IEnumerable<int> processIds)
    {
        foreach (var processId in processIds)
        {
            if (IsProcessRunning(processId))
            {
                return false;
            }
        }

        return true;
    }

    private static void TryTerminateProcess(int processId, ICollection<Exception> failures)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            Debug.WriteLine($"Testprozess {processId} war beim Cleanup bereits beendet.");
        }
        catch (InvalidOperationException)
        {
            Debug.WriteLine($"Testprozess {processId} war beim Cleanup bereits beendet.");
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static bool ReadBooleanMarker(string output, string markerName)
    {
        var prefix = markerName + "=";
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return bool.Parse(line[prefix.Length..].Trim());
            }
        }

        throw new InvalidOperationException($"Marker fehlt: {markerName}");
    }

    private static string CreateProcessTreeScripts(TestTempDirectory temp)
    {
        temp.CreateFile("grandchild.ps1", GrandchildScript);
        return temp.CreateFile("tree.ps1", ProcessTreeScript);
    }

    private const string ProbeScript = """
        param([string]$Mode)
        Write-Output "ARG=$($args[0])"
        Write-Output "WORKING=$((Get-Location).Path)"
        Write-Output "GIT_INHERITED=$($env:GIT_AINETLINTER_INHERITED)"
        Write-Output "GIT_EXPLICIT=$($env:GIT_AINETLINTER_EXPLICIT)"
        Write-Output "REQUEST_MARKER=$($env:AINETLINTER_TEST_MARKER)"
        Write-Output "STDIN_REDIRECTED=$([Console]::IsInputRedirected)"
        [Console]::Error.WriteLine("STDERR=stderr-marker")
        if ($Mode -eq "large") {
            [Console]::WriteLine(("o" * 100000))
            [Console]::Error.WriteLine(("e" * 100000))
        }
        """;

    private const string ProcessTreeScript = """
        param([string]$Mode, [string]$MarkerPath)
        if ($Mode -ne "tree") { exit 2 }
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = "pwsh"
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardInput = $false
        $startInfo.RedirectStandardOutput = $false
        $startInfo.RedirectStandardError = $false
        $startInfo.ArgumentList.Add("-NoLogo")
        $startInfo.ArgumentList.Add("-NoProfile")
        $startInfo.ArgumentList.Add("-NonInteractive")
        $startInfo.ArgumentList.Add("-File")
        $startInfo.ArgumentList.Add((Join-Path $PSScriptRoot "grandchild.ps1"))
        $grandchild = [System.Diagnostics.Process]::Start($startInfo)
        "$PID`n$($grandchild.Id)" | Set-Content -LiteralPath $MarkerPath
        exit 0
        """;

    private const string GrandchildScript = """
        while ($true) {
            [Console]::Out.WriteLine("grandchild-output")
            [Console]::Error.WriteLine("grandchild-error")
            Start-Sleep -Milliseconds 10
        }
        """;

    private const string StartMarkerScript = """
        param([string]$MarkerPath)
        Set-Content -LiteralPath $MarkerPath -Value "started"
        """;
}
