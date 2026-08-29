#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceGitProcessNativeMethods
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetInformationJobObject(
        ExternalSourceGitProcessNativeJob job,
        uint jobObjectInfoClass,
        ref JobObjectExtendedLimitInformation jobObjectInfo,
        int jobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TerminateJobObject(
        ExternalSourceGitProcessNativeJob job,
        uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AssignProcessToJobObject(
        ExternalSourceGitProcessNativeJob job,
        IntPtr processHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TerminateProcess(
        IntPtr processHandle,
        uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint ResumeThread(IntPtr threadHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint WaitForSingleObject(
        IntPtr handle,
        uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);

    // ainetlinter-disable MaxMethodParameterCount — Win32-ABI-Signatur muss unverändert bleiben.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        ref SecurityAttributes securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetHandleInformation(
        IntPtr handle,
        uint mask,
        uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        int attributeCount,
        int flags,
        ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

    // ainetlinter-disable MaxMethodParameterCount — Win32-ABI-Signatur muss unverändert bleiben.
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        uint flags,
        uint attribute,
        IntPtr value,
        UIntPtr size,
        IntPtr previousValue,
        IntPtr returnSize);

    // ainetlinter-disable MaxMethodParameterCount — Win32-ABI-Signatur muss unverändert bleiben.
    // ainetlinter-disable AllowOutParameters — CreateProcessW schreibt die native Prozessstruktur.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateProcess(
        string? applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

}

[StructLayout(LayoutKind.Sequential)]
internal struct JobObjectBasicLimitInformation
{
    internal long PerProcessUserTimeLimit;
    internal long PerJobUserTimeLimit;
    internal uint LimitFlags;
    internal UIntPtr MinimumWorkingSetSize;
    internal UIntPtr MaximumWorkingSetSize;
    internal uint ActiveProcessLimit;
    internal UIntPtr Affinity;
    internal uint PriorityClass;
    internal uint SchedulingClass;
}

[StructLayout(LayoutKind.Sequential)]
internal struct IoCounters
{
    internal ulong ReadOperationCount;
    internal ulong WriteOperationCount;
    internal ulong OtherOperationCount;
    internal ulong ReadTransferCount;
    internal ulong WriteTransferCount;
    internal ulong OtherTransferCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct JobObjectExtendedLimitInformation
{
    internal JobObjectBasicLimitInformation BasicLimitInformation;
    internal IoCounters IoInfo;
    internal UIntPtr ProcessMemoryLimit;
    internal UIntPtr PeakProcessMemoryUsed;
    internal UIntPtr JobMemoryLimit;
    internal UIntPtr PeakJobMemoryUsed;
}

[StructLayout(LayoutKind.Sequential)]
internal struct StartupInfo
{
    internal int cb;
    internal IntPtr lpReserved;
    internal IntPtr lpDesktop;
    internal IntPtr lpTitle;
    internal int dwX;
    internal int dwY;
    internal int dwXSize;
    internal int dwYSize;
    internal int dwXCountChars;
    internal int dwYCountChars;
    internal int dwFillAttribute;
    internal uint dwFlags;
    internal short wShowWindow;
    internal short cbReserved2;
    internal IntPtr lpReserved2;
    internal IntPtr hStdInput;
    internal IntPtr hStdOutput;
    internal IntPtr hStdError;
}

[StructLayout(LayoutKind.Sequential)]
internal struct StartupInfoEx
{
    internal StartupInfo StartupInfo;
    internal IntPtr lpAttributeList;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ProcessInformation
{
    internal IntPtr hProcess;
    internal IntPtr hThread;
    internal uint processId;
    internal uint threadId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SecurityAttributes
{
    internal int Length;
    internal IntPtr SecurityDescriptor;
    [MarshalAs(UnmanagedType.Bool)]
    internal bool InheritHandle;
}
