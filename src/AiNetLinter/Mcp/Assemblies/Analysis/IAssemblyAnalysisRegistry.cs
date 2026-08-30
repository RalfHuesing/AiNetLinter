#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal interface IAssemblyAnalysisRegistry : IAsyncDisposable
{
    int ResidentCount { get; }

    Task<AssemblyAnalysisLeaseResult> LeaseAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}
