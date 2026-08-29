#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using static AiNetLinter.Mcp.Assemblies.ExternalSourceGitProcessNativeMethods;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceGitProcessLauncherNativeHelpers
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint HandleFlagInherit = 0x00000001;

    internal static IntPtr CreateEnvironmentBlock(ProcessStartInfo startInfo)
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

    internal static StringBuilder CreateCommandLine(ProcessStartInfo startInfo)
    {
        var commandLine = new StringBuilder(QuoteArgument(startInfo.FileName));
        foreach (var argument in startInfo.ArgumentList)
        {
            commandLine.Append(' ');
            commandLine.Append(QuoteArgument(argument));
        }

        return commandLine;
    }

    internal static IntPtr CreateStandardInputHandle()
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
        catch (Exception primaryException)
        {
            try
            {
                CloseHandleOrThrow(handle, "Standard-Input-Handle");
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(primaryException, cleanupException);
            }

            ExceptionDispatchInfo.Capture(primaryException).Throw();
            throw new InvalidOperationException(
                "Die primäre Standard-Input-Exception konnte nicht erneut ausgelöst werden.");
        }
    }

    internal static IntPtr ParseHandle(string handle) =>
        new(long.Parse(handle, CultureInfo.InvariantCulture));

    internal static bool IsUsableHandle(IntPtr handle) =>
        handle != IntPtr.Zero && handle != new IntPtr(-1);

    internal static void EnsureInheritable(IntPtr handle)
    {
        if (!SetHandleInformation(handle, HandleFlagInherit, HandleFlagInherit))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    internal static void CloseHandleOrThrow(IntPtr handle, string handleName)
    {
        if (!IsUsableHandle(handle))
        {
            return;
        }

        if (!CloseHandle(handle))
        {
            throw ExternalSourceGitProcessStartFailureCleanup.CreateNativeFailure(
                Marshal.GetLastWin32Error(),
                $"Das {handleName} konnte nicht geschlossen werden.");
        }
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
}
