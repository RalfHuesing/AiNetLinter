#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal interface IExternalSourceRepositoryAcquirer
{
    Task<ExternalSourceRepositoryAcquisitionResult> AcquireAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken = default);
}
