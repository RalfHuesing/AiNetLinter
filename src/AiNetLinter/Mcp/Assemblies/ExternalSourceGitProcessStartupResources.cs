#nullable enable

using System;
using System.Collections.Generic;
using System.IO.Pipes;
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

        try
        {
            ErrorPipe?.DisposeLocalCopyOfClientHandle();
        }
        finally
        {
            try
            {
                OutputPipe?.DisposeLocalCopyOfClientHandle();
            }
            finally
            {
                CloseHandle(InputHandle);
                ErrorPipe?.Dispose();
                OutputPipe?.Dispose();
                Job.Dispose();
            }
        }
    }
}
