#nullable enable

using System;
using System.Threading;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;

internal interface IAssemblySourceSelectionSnapshotRegistry : IDisposable
{
    int ResidentCount { get; }

    ExternalResourceOperationLease BeginOperation(CancellationToken cancellationToken);

    SourceSnapshotLease Acquire(ExternalSourceSnapshot snapshot);
}
