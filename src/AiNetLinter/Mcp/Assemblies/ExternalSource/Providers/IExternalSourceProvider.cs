#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Providers;

internal interface IExternalSourceProvider
{
    ValueTask<ExternalSourceProviderResult> ResolveAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken = default);
}
