#nullable enable

using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using static AiNetLinter.Mcp.Assemblies.ExternalSourceGitProcessNativeMethods;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class ExternalSourceGitProcessStartupResources : IDisposable
{
    internal ExternalSourceGitProcessNativeJob Job { get; set; } = null!;

    internal AnonymousPipeServerStream OutputPipe { get; set; } = null!;

    internal AnonymousPipeServerStream ErrorPipe { get; set; } = null!;

    internal List<IntPtr> InheritedHandles { get; set; } = [];

    internal IntPtr InputHandle { get; set; }

    internal bool OwnershipTransferred { get; set; }

    public void Dispose()
    {
        if (OwnershipTransferred)
        {
            return;
        }

        var failures = new List<Exception>();
        TryCleanup(
            () => ErrorPipe?.DisposeLocalCopyOfClientHandle(),
            failures);
        TryCleanup(
            () => OutputPipe?.DisposeLocalCopyOfClientHandle(),
            failures);
        TryCleanup(CloseInputHandle, failures);
        TryCleanup(() => ErrorPipe?.Dispose(), failures);
        TryCleanup(() => OutputPipe?.Dispose(), failures);
        TryCleanup(() => Job?.Dispose(), failures);

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Die nativen Startressourcen konnten nicht vollständig freigegeben werden.",
                failures);
        }
    }

    private void CloseInputHandle()
    {
        if (!IsUsableHandle(InputHandle))
        {
            return;
        }

        if (!CloseHandle(InputHandle))
        {
            throw ExternalSourceGitProcessStartFailureCleanup.CreateNativeFailure(
                Marshal.GetLastWin32Error(),
                "Der Standard-Input-Handle konnte nicht geschlossen werden.");
        }

        InputHandle = IntPtr.Zero;
    }

    private static void TryCleanup(Action cleanup, ICollection<Exception> failures)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static bool IsUsableHandle(IntPtr handle) =>
        handle != IntPtr.Zero && handle != new IntPtr(-1);
}
