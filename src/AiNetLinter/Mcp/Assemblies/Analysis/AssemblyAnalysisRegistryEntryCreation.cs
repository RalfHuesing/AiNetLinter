#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed record AssemblyAnalysisRegistryEntryCreation(
    CancellationTokenSource CancellationSource,
    Task<AssemblyAnalysisEntry> Task)
{
    private int cancellationSourceDisposed;

    internal void DisposeCancellationSource()
    {
        if (Interlocked.Exchange(ref cancellationSourceDisposed, 1) == 0)
        {
            CancellationSource.Dispose();
        }
    }
}
