#nullable enable

using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using static AiNetLinter.Mcp.Assemblies.ExternalSource.ProcessExecution.ExternalSourceGitProcessNativeMethods;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.ProcessExecution;

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
        TryCleanup(() => Job?.Close(failures), failures);

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Die nativen Startressourcen konnten nicht vollständig freigegeben werden.",
                failures);
        }
    }

    private void CloseInputHandle()
    {
        var handle = InputHandle;
        InputHandle = IntPtr.Zero;
        if (!ExternalSourceGitProcessCleanupHelpers.IsUsableHandle(handle))
        {
            return;
        }

        if (!CloseHandle(handle))
        {
            throw ExternalSourceGitProcessStartFailureCleanup.CreateNativeFailure(
                Marshal.GetLastWin32Error(),
                "Der Standard-Input-Handle konnte nicht geschlossen werden.");
        }
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

}
