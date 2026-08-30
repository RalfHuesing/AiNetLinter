#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal interface IAssemblyAnalysisRegistry : IAsyncDisposable
{
    int ResidentCount { get; }

    Task<IReadOnlyList<AssemblyAnalysisHealthSnapshot>> SnapshotsAsync();

    Task<AssemblyAnalysisLeaseResult> LeaseAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}
