#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

internal sealed class ExternalSourceRecordingTransport : IGiteaRepositoryTransport
{
    private readonly Func<ExternalSourceMapping, string, CancellationToken, ExternalSourceRepositoryTransportResult> operation;

    internal ExternalSourceRecordingTransport(
        Func<ExternalSourceMapping, string, CancellationToken, ExternalSourceRepositoryTransportResult> operation)
    {
        this.operation = operation;
    }

    internal int CallCount { get; private set; }

    internal ExternalSourceMapping? Mapping { get; private set; }

    internal string? DestinationPath { get; private set; }

    internal bool DestinationHadNoWorkingTreeEntriesAtCall { get; private set; }

    internal CancellationToken CancellationToken { get; private set; }

    public ValueTask<ExternalSourceRepositoryTransportResult> CloneDefaultBranchAsync(
        ExternalSourceMapping mapping,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        Mapping = mapping;
        DestinationPath = destinationPath;
        DestinationHadNoWorkingTreeEntriesAtCall = Directory.Exists(destinationPath)
            && Directory.EnumerateFileSystemEntries(destinationPath).All(path =>
                string.Equals(
                    Path.GetFileName(path),
                    ExternalSourceCheckoutOwnership.OwnershipMarkerFileName,
                    StringComparison.Ordinal));
        CancellationToken = cancellationToken;
        return ValueTask.FromResult(operation(mapping, destinationPath, cancellationToken));
    }
}
