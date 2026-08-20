#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace AiNetLinter.Mcp.Lifetime;

internal static class ParentProcessDetector
{
    private const int ProcessBasicInformationClass = 0;
    private const int ProcStatParentFieldIndex = 1;
    private const int ProcStatFieldsStartOffset = 2;
    private const int InvalidProcessId = 0;
    private const string ProcStatPathFormat = "/proc/{0}/stat";

    internal static int? TryGetParentProcessId(Action<string>? report = null)
    {
        var processId = Environment.ProcessId;
        if (processId <= InvalidProcessId) return null;

        if (OperatingSystem.IsWindows())
        {
            return TryGetWindowsParentProcessId(report);
        }

        if (OperatingSystem.IsLinux())
        {
            return TryGetProcParentProcessId(processId, report);
        }

        return null;
    }

    internal static int? TryParseProcStatParentProcessId(string statContent)
    {
        ArgumentNullException.ThrowIfNull(statContent);

        var closingCommandIndex = statContent.LastIndexOf(')');
        if (closingCommandIndex < 0 || closingCommandIndex + ProcStatFieldsStartOffset > statContent.Length)
        {
            return null;
        }

        var fields = statContent[(closingCommandIndex + ProcStatFieldsStartOffset)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length <= ProcStatParentFieldIndex)
        {
            return null;
        }

        return int.TryParse(
            fields[ProcStatParentFieldIndex],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parentProcessId) && parentProcessId > InvalidProcessId
            ? parentProcessId
            : null;
    }

    private static int? TryGetProcParentProcessId(int processId, Action<string>? report)
    {
        try
        {
            var path = string.Format(CultureInfo.InvariantCulture, ProcStatPathFormat, processId);
            return TryParseProcStatParentProcessId(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            ReportFailure(report, $"Parent-PID konnte nicht aus /proc ermittelt werden: {exception.Message}");
            return null;
        }
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
