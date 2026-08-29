#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
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
    private const uint ProcessTerminateExitCode = 1;
    private const uint ProcessWaitTimeoutMilliseconds = 5000;
    private const uint JobObjectExtendedLimitInformationClass = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const uint ProcThreadAttributeHandleList = 0x00020002;
    private const uint StartupInfoUseStandardHandles = 0x00000100;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint HandleFlagInherit = 0x00000001;

    internal static ExternalSourceGitProcessLaunch Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ValidateStartInfo(startInfo);
        var resources = CreateResources();
        try
        {
            var launch = LaunchProcess(startInfo, resources);
            resources.OwnershipTransferred = true;
            return launch;
        }
        catch
        {
            resources.Dispose();
            throw;
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
            if (error == 0)
            {
                return true;
            }

            failures.Add(new Win32Exception(error));
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
            var outputHandle = ParseHandle(resources.OutputPipe.GetClientHandleAsString());
            var errorHandle = ParseHandle(resources.ErrorPipe.GetClientHandleAsString());
            EnsureInheritable(outputHandle);
            EnsureInheritable(errorHandle);
            resources.InheritedHandles = [outputHandle, errorHandle];
            resources.InputHandle = CreateStandardInputHandle();
            resources.InheritedHandles.Add(resources.InputHandle);
            return resources;
        }
        catch
        {
            resources.Dispose();
            throw;
        }
    }

    private static ExternalSourceGitProcessLaunch LaunchProcess(
        ProcessStartInfo startInfo,
        ExternalSourceGitProcessStartupResources resources)
    {
        var environmentBlock = CreateEnvironmentBlock(startInfo);
        ProcessInformation processInformation = default;
        try
        {
            var commandLine = CreateCommandLine(startInfo);
            var startupInfo = CreateStartupInfo(resources);
            try
            {
                processInformation = CreateNativeProcess(
                    startInfo,
                    commandLine,
                    environmentBlock,
                    ref startupInfo);
            }
            finally
            {
                DeleteAttributeList(startupInfo.lpAttributeList);
                Marshal.FreeHGlobal(startupInfo.lpAttributeList);
            }

            AssignProcessToJob(resources.Job, processInformation.hProcess);
            ResumeProcess(processInformation.hThread);

            resources.OutputPipe.DisposeLocalCopyOfClientHandle();
            resources.ErrorPipe.DisposeLocalCopyOfClientHandle();
            CloseHandle(resources.InputHandle);
            resources.InputHandle = IntPtr.Zero;
            CloseHandle(processInformation.hThread);
            processInformation.hThread = IntPtr.Zero;
            var process = Process.GetProcessById(checked((int)processInformation.processId));
            CloseHandle(processInformation.hProcess);
            processInformation.hProcess = IntPtr.Zero;
            var stdout = new StreamReader(resources.OutputPipe);
            var stderr = new StreamReader(resources.ErrorPipe);
            return new(process, resources.Job, stdout, stderr);
        }
        catch
        {
            TerminateCreatedProcess(ref processInformation);
            CloseProcessInformation(ref processInformation);
            throw;
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
        IntPtr processHandle)
    {
        if (!AssignProcessToJobObject(job, processHandle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static void ResumeProcess(IntPtr threadHandle)
    {
        if (ResumeThread(threadHandle) == uint.MaxValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static void TerminateCreatedProcess(ref ProcessInformation processInformation)
    {
        if (!IsUsableHandle(processInformation.hProcess))
        {
            return;
        }

        TerminateProcess(processInformation.hProcess, ProcessTerminateExitCode);
        WaitForSingleObject(processInformation.hProcess, ProcessWaitTimeoutMilliseconds);
    }

    private static IntPtr CreateEnvironmentBlock(ProcessStartInfo startInfo)
    {
        var entries = new List<string>();
        foreach (var variable in startInfo.Environment)
        {
            entries.Add(variable.Key + "=" + variable.Value);
        }

        entries.Sort(StringComparer.OrdinalIgnoreCase);
        var block = string.Join('\0', entries) + "\0\0";
        return Marshal.StringToHGlobalUni(block);
    }

    private static StringBuilder CreateCommandLine(ProcessStartInfo startInfo)
    {
        var commandLine = new StringBuilder(QuoteArgument(startInfo.FileName));
        foreach (var argument in startInfo.ArgumentList)
        {
            commandLine.Append(' ');
            commandLine.Append(QuoteArgument(argument));
        }

        return commandLine;
    }

    private static string QuoteArgument(string argument)
    {
        var quoted = new StringBuilder(argument.Length + 2);
        var backslashes = 0;
        quoted.Append('"');
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                quoted.Append('\\', backslashes * 2 + 1);
                quoted.Append('"');
                backslashes = 0;
                continue;
            }

            quoted.Append('\\', backslashes);
            quoted.Append(character);
            backslashes = 0;
        }

        quoted.Append('\\', backslashes * 2);
        quoted.Append('"');
        return quoted.ToString();
    }

    private static IntPtr CreateStandardInputHandle()
    {
        var attributes = new SecurityAttributes
        {
            Length = Marshal.SizeOf<SecurityAttributes>(),
            InheritHandle = true,
        };
        var handle = CreateFile(
            "NUL",
            GenericRead,
            FileShareRead | FileShareWrite,
            ref attributes,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);
        if (!IsUsableHandle(handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            EnsureInheritable(handle);
            return handle;
        }
        catch
        {
            CloseHandle(handle);
            throw;
        }
    }

    private static IntPtr ParseHandle(string handle) =>
        new(long.Parse(handle, CultureInfo.InvariantCulture));

    private static bool IsUsableHandle(IntPtr handle) =>
        handle != IntPtr.Zero && handle != new IntPtr(-1);

    private static void EnsureInheritable(IntPtr handle)
    {
        if (!SetHandleInformation(handle, HandleFlagInherit, HandleFlagInherit))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static void CloseProcessInformation(ref ProcessInformation processInformation)
    {
        CloseHandle(processInformation.hThread);
        CloseHandle(processInformation.hProcess);
        processInformation.hThread = IntPtr.Zero;
        processInformation.hProcess = IntPtr.Zero;
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
