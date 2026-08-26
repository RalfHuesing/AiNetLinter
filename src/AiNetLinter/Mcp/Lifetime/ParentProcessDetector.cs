#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AiNetLinter.Mcp.Lifetime;

internal static class ParentProcessDetector
{
    private const int ProcessBasicInformationClass = 0;
    private const int InvalidProcessId = 0;

    internal static int? TryGetParentProcessId(Action<string>? report = null)
    {
        var processId = Environment.ProcessId;
        if (processId <= InvalidProcessId) return null;

        if (OperatingSystem.IsWindows())
        {
            return TryGetWindowsParentProcessId(report);
        }

        return null;
    }

    private static int? TryGetWindowsParentProcessId(Action<string>? report)
    {
        try
        {
            // Pseudo-Handle (HANDLE)-1 verweist direkt auf den aktuellen Prozess,
            // ohne Process.GetCurrentProcess()-Allokationen oder Handle-Schliess-Probleme.
            var status = NtQueryInformationProcess(
                new IntPtr(-1),
                ProcessBasicInformationClass,
                out var processInformation,
                Marshal.SizeOf<ProcessBasicInformation>(),
                out _);

            return status < 0
                ? null
                : ToProcessId(processInformation.InheritedFromUniqueProcessId);
        }
        catch (Exception exception)
        {
            ReportFailure(report, $"Windows-API zur Parent-PID-Ermittlung nicht verfuegbar: {exception.Message}");
            return null;
        }
    }

    private static int? ToProcessId(UIntPtr processId)
    {
        var value = processId.ToUInt64();
        return value is > InvalidProcessId and <= int.MaxValue ? (int)value : null;
    }

    private static void ReportFailure(Action<string>? report, string message) => report?.Invoke($"[WARN]: {message}");

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        out ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public UIntPtr AffinityMask;
        public IntPtr BasePriority;
        public UIntPtr UniqueProcessId;
        public UIntPtr InheritedFromUniqueProcessId;
    }
}
