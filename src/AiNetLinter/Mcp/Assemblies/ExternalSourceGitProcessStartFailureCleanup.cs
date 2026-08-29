#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using static AiNetLinter.Mcp.Assemblies.ExternalSourceGitProcessNativeMethods;

namespace AiNetLinter.Mcp.Assemblies;

internal readonly record struct ExternalSourceGitProcessNativeBooleanResult(
    bool Succeeded,
    int LastError);

internal readonly record struct ExternalSourceGitProcessNativeWaitResult(
    uint Status,
    int LastError);

internal sealed class ExternalSourceGitProcessNativeOperations
{
    internal ExternalSourceGitProcessNativeOperations(
        Func<ExternalSourceGitProcessNativeJob, IntPtr, ExternalSourceGitProcessNativeBooleanResult>
            assignProcessToJobObject,
        Func<IntPtr, uint, ExternalSourceGitProcessNativeBooleanResult> terminateProcess,
        Func<IntPtr, uint, ExternalSourceGitProcessNativeWaitResult> waitForSingleObject,
        Action<ProcessInformation>? processCreated = null)
    {
        ArgumentNullException.ThrowIfNull(assignProcessToJobObject);
        ArgumentNullException.ThrowIfNull(terminateProcess);
        ArgumentNullException.ThrowIfNull(waitForSingleObject);
        AssignProcessToJobObject = assignProcessToJobObject;
        TerminateProcess = terminateProcess;
        WaitForSingleObject = waitForSingleObject;
        ProcessCreated = processCreated;
    }

    internal Func<ExternalSourceGitProcessNativeJob, IntPtr, ExternalSourceGitProcessNativeBooleanResult>
        AssignProcessToJobObject { get; }

    internal Func<IntPtr, uint, ExternalSourceGitProcessNativeBooleanResult> TerminateProcess
    {
        get;
    }

    internal Func<IntPtr, uint, ExternalSourceGitProcessNativeWaitResult> WaitForSingleObject
    {
        get;
    }

    internal Action<ProcessInformation>? ProcessCreated { get; }

    internal static ExternalSourceGitProcessNativeOperations Runtime { get; } =
        new(
            (job, processHandle) => InvokeBoolean(
                () => ExternalSourceGitProcessNativeMethods.AssignProcessToJobObject(
                    job,
                    processHandle)),
            (processHandle, exitCode) => InvokeBoolean(
                () => ExternalSourceGitProcessNativeMethods.TerminateProcess(
                    processHandle,
                    exitCode)),
            (handle, milliseconds) => InvokeWait(
                () => ExternalSourceGitProcessNativeMethods.WaitForSingleObject(
                    handle,
                    milliseconds)));

    private static ExternalSourceGitProcessNativeBooleanResult InvokeBoolean(
        Func<bool> operation)
    {
        var succeeded = operation();
        return new(
            succeeded,
            succeeded ? 0 : Marshal.GetLastWin32Error());
    }

    private static ExternalSourceGitProcessNativeWaitResult InvokeWait(
        Func<uint> operation)
    {
        var status = operation();
        return new(status, Marshal.GetLastWin32Error());
    }
}

internal static class ExternalSourceGitProcessStartFailureCleanup
{
    private const uint ProcessTerminateExitCode = 1;
    private const uint ProcessWaitTimeoutMilliseconds = 5000;
    private const uint WaitObject0 = 0;
    private const uint WaitAbandoned0 = 0x00000080;
    private const uint WaitTimeout = 0x00000102;
    private const uint WaitFailed = uint.MaxValue;
    private static readonly object CleanupFailureDataKey = new();

    internal static void Cleanup(
        ref ProcessInformation processInformation,
        ExternalSourceGitProcessNativeOperations operations,
        ICollection<Exception> failures)
    {
        if (!IsUsableHandle(processInformation.hProcess))
        {
            CloseProcessInformation(ref processInformation, failures);
            return;
        }

        var nativeEndProven = TryTerminateAndWait(
            processInformation.hProcess,
            operations,
            failures);
        var fallbackEndProven = false;
        if (!nativeEndProven)
        {
            fallbackEndProven = TryManagedFallback(
                processInformation.processId,
                failures);
        }

        var finalNativeEndProven = TryWaitForProcessEnd(
            processInformation.hProcess,
            operations,
            failures,
            "abschließende");
        if (!finalNativeEndProven && !fallbackEndProven)
        {
            fallbackEndProven = TryManagedFallback(
                processInformation.processId,
                failures);
        }

        if (!finalNativeEndProven && !fallbackEndProven)
        {
            failures.Add(new InvalidOperationException(
                "Das Ende des erzeugten Git-Prozesses konnte nicht nachgewiesen werden."));
        }

        CloseProcessInformation(ref processInformation, failures);
    }

    internal static void RethrowWithCleanup(
        Exception primaryException,
        ICollection<Exception> failures)
    {
        var cleanupFailure = CombineFailures(failures);
        if (cleanupFailure is not null)
        {
            AttachCleanupFailure(primaryException, cleanupFailure);
        }

        ExceptionDispatchInfo.Capture(primaryException).Throw();
        throw new InvalidOperationException(
            "Die primäre Prozessstart-Exception konnte nicht erneut ausgelöst werden.");
    }

    internal static void AttachCleanupFailure(
        Exception primary,
        Exception cleanupFailure)
    {
        try
        {
            primary.Data[CleanupFailureDataKey] = cleanupFailure;
        }
        catch (Exception attachFailure)
        {
            throw new AggregateException(primary, cleanupFailure, attachFailure);
        }
    }

    private static bool TryTerminateAndWait(
        IntPtr processHandle,
        ExternalSourceGitProcessNativeOperations operations,
        ICollection<Exception> failures)
    {
        try
        {
            var termination = operations.TerminateProcess(
                processHandle,
                ProcessTerminateExitCode);
            if (!termination.Succeeded)
            {
                failures.Add(CreateNativeFailure(
                    termination.LastError,
                    "TerminateProcess konnte den erzeugten Git-Prozess nicht terminieren."));
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        return TryWaitForProcessEnd(
            processHandle,
            operations,
            failures,
            "erster");
    }

    private static bool TryWaitForProcessEnd(
        IntPtr processHandle,
        ExternalSourceGitProcessNativeOperations operations,
        ICollection<Exception> failures,
        string waitDescription)
    {
        try
        {
            var wait = operations.WaitForSingleObject(
                processHandle,
                ProcessWaitTimeoutMilliseconds);
            return HandleWaitResult(wait, waitDescription, failures);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            return false;
        }
    }

    private static bool HandleWaitResult(
        ExternalSourceGitProcessNativeWaitResult wait,
        string waitDescription,
        ICollection<Exception> failures)
    {
        if (wait.Status == WaitObject0)
        {
            return true;
        }

        if (wait.Status == WaitTimeout)
        {
            failures.Add(new TimeoutException(
                $"Der {waitDescription} Wait auf den erzeugten Git-Prozess ist abgelaufen."));
            return false;
        }

        if (wait.Status == WaitFailed)
        {
            failures.Add(CreateNativeFailure(
                wait.LastError,
                $"Der {waitDescription} Wait auf den erzeugten Git-Prozess ist fehlgeschlagen."));
            return false;
        }

        if (wait.Status == WaitAbandoned0)
        {
            failures.Add(new InvalidOperationException(
                $"Der {waitDescription} Wait auf den erzeugten Git-Prozess meldete WAIT_ABANDONED."));
            return false;
        }

        failures.Add(new InvalidOperationException(
            $"Der {waitDescription} Wait auf den erzeugten Git-Prozess meldete den unerwarteten Status 0x{wait.Status:X8}."));
        return false;
    }

    private static bool TryManagedFallback(
        uint processId,
        ICollection<Exception> failures)
    {
        if (processId == 0)
        {
            failures.Add(new InvalidOperationException(
                "ProcessInformation enthielt keine gültige Prozess-ID für den Fallback."));
            return false;
        }

        Process? process = null;
        try
        {
            var managedProcessId = checked((int)processId);
            process = Process.GetProcessById(managedProcessId);
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException exception)
                {
                    // Der Prozess kann zwischen HasExited und Kill beendet worden sein.
                    Debug.WriteLine(
                        $"Der bekannte Prozess {processId} war vor dem Fallback-Kill bereits beendet: {exception.Message}");
                }
            }

            if (!process.WaitForExit((int)ProcessWaitTimeoutMilliseconds))
            {
                failures.Add(new TimeoutException(
                    "Der bounded Fallback-Wait auf den erzeugten Git-Prozess ist abgelaufen."));
                return false;
            }

            if (!process.HasExited)
            {
                failures.Add(new InvalidOperationException(
                    "Der bounded Fallback konnte den Prozessstatus nicht als beendet verifizieren."));
                return false;
            }

            return true;
        }
        catch (ArgumentException exception)
        {
            Debug.WriteLine(
                $"Der bekannte Prozess {processId} war beim Fallback bereits beendet: {exception.Message}");
            return false;
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            return false;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static void CloseProcessInformation(
        ref ProcessInformation processInformation,
        ICollection<Exception> failures)
    {
        CloseNativeHandle(
            processInformation.hThread,
            "Thread-Handle",
            failures);
        CloseNativeHandle(
            processInformation.hProcess,
            "Prozess-Handle",
            failures);
        processInformation.hThread = IntPtr.Zero;
        processInformation.hProcess = IntPtr.Zero;
    }

    private static void CloseNativeHandle(
        IntPtr handle,
        string handleName,
        ICollection<Exception> failures)
    {
        if (!IsUsableHandle(handle))
        {
            return;
        }

        try
        {
            if (!ExternalSourceGitProcessNativeMethods.CloseHandle(handle))
            {
                failures.Add(CreateNativeFailure(
                    Marshal.GetLastWin32Error(),
                    $"Das {handleName} konnte nicht geschlossen werden."));
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    internal static Win32Exception CreateNativeFailure(int errorCode, string message) =>
        new Win32Exception(
            errorCode == 0 ? ErrorGenFailure : errorCode,
            message);

    private static Exception? CombineFailures(ICollection<Exception> failures) =>
        failures.Count switch
        {
            0 => null,
            1 => failures.First(),
            _ => new AggregateException("Die Prozessstartbereinigung ist fehlgeschlagen.", failures),
        };

    private static bool IsUsableHandle(IntPtr handle) =>
        handle != IntPtr.Zero && handle != new IntPtr(-1);
}
