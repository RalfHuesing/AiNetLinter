#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Providers;

internal interface IExternalSourceSnapshotMaterializer
{
    ValueTask<ExternalSourceSnapshot> MaterializeAsync(
        ExternalSourceMapping mapping,
        ExternalSourceCheckoutHandle checkout,
        CancellationToken cancellationToken = default);
}
