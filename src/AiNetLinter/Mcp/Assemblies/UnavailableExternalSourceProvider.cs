#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class UnavailableExternalSourceProvider : IExternalSourceProvider
{
    public ValueTask<ExternalSourceProviderResult> ResolveAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostic = new ExternalSourceConfigurationDiagnostic(
            ExternalSourceConfigurationDiagnosticCodes.ProviderUnavailable,
            "Der externe Source-Provider ist nicht verfügbar.",
            "warning",
            mapping.Url);
        return ValueTask.FromResult(new ExternalSourceProviderResult(
            isAvailable: false,
            diagnostics: [diagnostic],
            failureKind: ExternalSourceProviderFailureKind.ProviderUnavailable));
    }
}
