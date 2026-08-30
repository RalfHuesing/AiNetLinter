#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Providers;

internal interface IExternalSourceRepositoryAcquirer
{
    Task<ExternalSourceRepositoryAcquisitionResult> AcquireAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken = default);
}
