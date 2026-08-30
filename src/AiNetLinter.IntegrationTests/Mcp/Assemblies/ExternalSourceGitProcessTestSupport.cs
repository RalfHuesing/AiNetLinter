#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies;

internal static class ExternalSourceGitProcessTestSupport
{
    internal static readonly TimeSpan ProcessObservationTimeout = TimeSpan.FromSeconds(10);

    internal static async Task<(
        Task<ExternalSourceGitProcessResult> Operation,
        int[] ProcessIds)> StartAndReadProcessIdsAsync(
        ExternalSourceGitProcessRequest request,
        string markerPath,
        CancellationToken cancellationToken = default,
        ExternalSourceGitProcessNativeOperations? operations = null)
    {
        var executor = new ExternalSourceGitProcessExecutor();
        var operation = operations is null
            ? executor.ExecuteAsync(request, cancellationToken)
            : executor.ExecuteWithNativeOperationsAsync(request, operations, cancellationToken);
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

        if (processIds is null)
        {
            throw new InvalidOperationException("Die Prozess-IDs wurden nicht gelesen.");
        }

        return (operation, processIds);
    }

    internal static async Task<int[]> ReadProcessIdsAfterFailureAsync(string markerPath)
    {
        int[]? processIds = null;
        await TestWaiter.WaitForConditionAsync(
            () => TryReadProcessIds(markerPath, out processIds),
            ProcessObservationTimeout);
        if (processIds is null)
        {
            throw new InvalidOperationException("Die Prozess-IDs wurden nicht gelesen.");
        }

        return processIds;
    }

    internal static async Task WaitForProcessesToExitAsync(int[] processIds) =>
        await TestWaiter.WaitForConditionAsync(
            () => AllProcessesHaveExited(processIds),
            ProcessObservationTimeout);

    internal static bool TryReadProcessIds(string markerPath, out int[]? processIds)
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

    internal static bool IsProcessRunning(int processId)
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

    internal static async Task TerminateProcessesAsync(IEnumerable<int> processIds)
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

    internal static string CreateProcessTreeScripts(TestTempDirectory temp)
    {
        temp.CreateFile("grandchild.ps1", ExternalSourceGitProcessTestScripts.GrandchildScript);
        return temp.CreateFile("tree.ps1", ExternalSourceGitProcessTestScripts.ProcessTreeScript);
    }

    internal static ExternalSourceGitProcessRequest CreateProcessTreeRequest(
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

    internal static bool ReadBooleanMarker(string output, string markerName)
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
            // Der Testprozess kann zwischen der Abfrage und dem Kill beendet worden sein.
        }
        catch (InvalidOperationException)
        {
            // Der Testprozess kann zwischen der Abfrage und dem Kill beendet worden sein.
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }
}

internal static class ExternalSourceGitProcessTestScripts
{
    internal const string ProbeScript = """
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

    internal const string ProcessTreeScript = """
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

    internal const string GrandchildScript = """
        while ($true) {
            [Console]::Out.WriteLine("grandchild-output")
            [Console]::Error.WriteLine("grandchild-error")
            Start-Sleep -Milliseconds 10
        }
        """;

    internal const string StartMarkerScript = """
        param([string]$MarkerPath)
        Set-Content -LiteralPath $MarkerPath -Value "started"
        """;
}
