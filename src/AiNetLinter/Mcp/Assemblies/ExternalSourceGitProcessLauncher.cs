#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using static AiNetLinter.Mcp.Assemblies.ExternalSourceGitProcessNativeMethods;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceGitProcessLauncher
{
    private const uint CreateNoWindow = 0x08000000;
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint JobObjectExtendedLimitInformationClass = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const uint ProcThreadAttributeHandleList = 0x00020002;
    private const uint StartupInfoUseStandardHandles = 0x00000100;

    internal static ExternalSourceGitProcessLaunch Start(
        ProcessStartInfo startInfo,
        ExternalSourceGitProcessNativeOperations operations)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(operations);
        ValidateStartInfo(startInfo);
        var resources = CreateResources();
        try
        {
            var launch = LaunchProcess(startInfo, resources, operations);
            resources.OwnershipTransferred = true;
            return launch;
        }
        catch (Exception primaryException)
        {
            try
            {
                resources.Dispose();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(primaryException, cleanupException);
            }

            ExceptionDispatchInfo.Capture(primaryException).Throw();
            throw new InvalidOperationException(
                "Die primäre Prozessstart-Exception konnte nicht erneut ausgelöst werden.");
        }
    }

    internal static bool TryTerminate(
        ExternalSourceGitProcessNativeJob job,
        ICollection<Exception> failures)
    {
        try
        {
            if (job.IsInvalid || job.IsClosed)
            {
                return false;
            }

            if (TerminateJobObject(job, 1))
            {
                return true;
            }

            var error = Marshal.GetLastWin32Error();
            failures.Add(new Win32Exception(
                error == 0 ? ErrorGenFailure : error,
                "TerminateJobObject konnte den Git-Prozessbaum nicht beenden."));
            return false;
        }
        catch (Exception exception) when (IsExpectedProcessException(exception))
        {
            failures.Add(exception);
            return false;
        }
    }

    internal static bool CloseNativeHandle(IntPtr handle) => CloseHandle(handle);

    private static void ValidateStartInfo(ProcessStartInfo startInfo)
    {
        if (startInfo.UseShellExecute || !startInfo.RedirectStandardOutput
            || !startInfo.RedirectStandardError)
        {
            throw new InvalidOperationException(
                "Der Prozessbesitz-Helper benötigt deaktivierte Shell-Ausführung und Redirects.");
        }
    }

    private static ExternalSourceGitProcessStartupResources CreateResources()
    {
        var resources = new ExternalSourceGitProcessStartupResources
        {
            Job = CreateJob(),
        };
        try
        {
            resources.OutputPipe = new AnonymousPipeServerStream(
                PipeDirection.In,
                HandleInheritability.Inheritable);
            resources.ErrorPipe = new AnonymousPipeServerStream(
                PipeDirection.In,
                HandleInheritability.Inheritable);
            var outputHandle = ExternalSourceGitProcessLauncherNativeHelpers.ParseHandle(
                resources.OutputPipe.GetClientHandleAsString());
            var errorHandle = ExternalSourceGitProcessLauncherNativeHelpers.ParseHandle(
                resources.ErrorPipe.GetClientHandleAsString());
            ExternalSourceGitProcessLauncherNativeHelpers.EnsureInheritable(outputHandle);
            ExternalSourceGitProcessLauncherNativeHelpers.EnsureInheritable(errorHandle);
            resources.InheritedHandles = [outputHandle, errorHandle];
            resources.InputHandle =
                ExternalSourceGitProcessLauncherNativeHelpers.CreateStandardInputHandle();
            resources.InheritedHandles.Add(resources.InputHandle);
            return resources;
        }
        catch (Exception primaryException)
        {
            try
            {
                resources.Dispose();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(primaryException, cleanupException);
            }

            ExceptionDispatchInfo.Capture(primaryException).Throw();
            throw new InvalidOperationException(
                "Die primäre Startressourcen-Exception konnte nicht erneut ausgelöst werden.");
        }
    }

    private static ExternalSourceGitProcessLaunch LaunchProcess(
        ProcessStartInfo startInfo,
        ExternalSourceGitProcessStartupResources resources,
        ExternalSourceGitProcessNativeOperations operations)
    {
        var environmentBlock = ExternalSourceGitProcessLauncherNativeHelpers
            .CreateEnvironmentBlock(startInfo);
        ProcessInformation processInformation = default;
        try
        {
            var commandLine = ExternalSourceGitProcessLauncherNativeHelpers
                .CreateCommandLine(startInfo);
            var startupInfo = CreateStartupInfo(resources);
            try
            {
                processInformation = CreateNativeProcess(
                    startInfo,
                    commandLine,
                    environmentBlock,
                    ref startupInfo);
                operations.ProcessCreated?.Invoke(processInformation);
            }
            finally
            {
                DeleteAttributeList(startupInfo.lpAttributeList);
                Marshal.FreeHGlobal(startupInfo.lpAttributeList);
            }

            AssignProcessToJob(resources.Job, processInformation.hProcess, operations);
            ResumeProcess(processInformation.hThread);

            resources.OutputPipe.DisposeLocalCopyOfClientHandle();
            resources.ErrorPipe.DisposeLocalCopyOfClientHandle();
            ExternalSourceGitProcessLauncherNativeHelpers.CloseHandleOrThrow(
                resources.InputHandle,
                "Standard-Input-Handle");
            resources.InputHandle = IntPtr.Zero;
            ExternalSourceGitProcessLauncherNativeHelpers.CloseHandleOrThrow(
                processInformation.hThread,
                "Thread-Handle");
            processInformation.hThread = IntPtr.Zero;
            var process = Process.GetProcessById(checked((int)processInformation.processId));
            ExternalSourceGitProcessLauncherNativeHelpers.CloseHandleOrThrow(
                processInformation.hProcess,
                "Prozess-Handle");
            processInformation.hProcess = IntPtr.Zero;
            var stdout = new StreamReader(resources.OutputPipe);
            var stderr = new StreamReader(resources.ErrorPipe);
            return new(process, resources.Job, stdout, stderr);
        }
        catch (Exception primaryException)
        {
            var failures = new List<Exception>();
            ExternalSourceGitProcessStartFailureCleanup.Cleanup(
                ref processInformation,
                operations,
                failures);
            ExternalSourceGitProcessStartFailureCleanup.RethrowWithCleanup(
                primaryException,
                failures);
            throw new InvalidOperationException(
                "Die primäre Prozessstart-Exception konnte nicht erneut ausgelöst werden.");
        }
        finally
        {
            Marshal.FreeHGlobal(environmentBlock);
        }
    }

    private static StartupInfoEx CreateStartupInfo(
        ExternalSourceGitProcessStartupResources resources) =>
        new()
        {
            StartupInfo = new StartupInfo
            {
                cb = Marshal.SizeOf<StartupInfoEx>(),
                dwFlags = StartupInfoUseStandardHandles,
                hStdInput = resources.InputHandle,
                hStdOutput = resources.OutputPipe.ClientSafePipeHandle.DangerousGetHandle(),
                hStdError = resources.ErrorPipe.ClientSafePipeHandle.DangerousGetHandle(),
            },
            lpAttributeList = CreateAttributeList(resources),
        };

    private static IntPtr CreateAttributeList(
        ExternalSourceGitProcessStartupResources resources)
    {
        IntPtr size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        if (size == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var attributeList = Marshal.AllocHGlobal(size);
        try
        {
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var handlesValue = Marshal.AllocHGlobal(IntPtr.Size * resources.InheritedHandles.Count);
            try
            {
                for (var index = 0; index < resources.InheritedHandles.Count; index++)
                {
                    Marshal.WriteIntPtr(
                        handlesValue,
                        index * IntPtr.Size,
                        resources.InheritedHandles[index]);
                }

                if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ProcThreadAttributeHandleList,
                    handlesValue,
                    (UIntPtr)(IntPtr.Size * resources.InheritedHandles.Count),
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return attributeList;
            }
            finally
            {
                Marshal.FreeHGlobal(handlesValue);
            }
        }
        catch
        {
            Marshal.FreeHGlobal(attributeList);
            throw;
        }
    }

    private static ExternalSourceGitProcessNativeJob CreateJob()
    {
        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var job = new ExternalSourceGitProcessNativeJob(handle);
        var limits = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = JobObjectLimitKillOnJobClose,
            },
        };
        if (!SetInformationJobObject(
                job,
                JobObjectExtendedLimitInformationClass,
                ref limits,
                Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
        {
            job.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return job;
    }

    private static ProcessInformation CreateNativeProcess(
        ProcessStartInfo startInfo,
        StringBuilder commandLine,
        IntPtr environmentBlock,
        ref StartupInfoEx startupInfo)
    {
        if (!CreateProcess(
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                true,
                ExtendedStartupInfoPresent | CreateSuspended | CreateUnicodeEnvironment | CreateNoWindow,
                environmentBlock,
                string.IsNullOrEmpty(startInfo.WorkingDirectory)
                    ? null
                    : startInfo.WorkingDirectory,
                ref startupInfo,
                out var processInformation))
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, "CreateProcessW konnte nicht gestartet werden.");
        }

        return processInformation;
    }

    private static void AssignProcessToJob(
        ExternalSourceGitProcessNativeJob job,
        IntPtr processHandle,
        ExternalSourceGitProcessNativeOperations operations)
    {
        var result = operations.AssignProcessToJobObject(job, processHandle);
        if (!result.Succeeded)
        {
            throw ExternalSourceGitProcessStartFailureCleanup.CreateNativeFailure(
                result.LastError,
                "AssignProcessToJobObject konnte den Git-Prozess nicht übernehmen.");
        }
    }

    private static void ResumeProcess(IntPtr threadHandle)
    {
        if (ResumeThread(threadHandle) == uint.MaxValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static void DeleteAttributeList(IntPtr attributeList)
    {
        if (attributeList != IntPtr.Zero)
        {
            DeleteProcThreadAttributeList(attributeList);
        }
    }

    private static bool IsExpectedProcessException(Exception exception) =>
        exception is InvalidOperationException
            or ObjectDisposedException
            or IOException
            or Win32Exception;

}

internal sealed record ExternalSourceGitProcessLaunch(
    Process Process,
    ExternalSourceGitProcessNativeJob Job,
    StreamReader StandardOutput,
    StreamReader StandardError);

internal sealed class ExternalSourceGitProcessNativeJob : SafeHandleZeroOrMinusOneIsInvalid
{
    internal ExternalSourceGitProcessNativeJob(IntPtr handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle() =>
        ExternalSourceGitProcessLauncher.CloseNativeHandle(handle);
}
