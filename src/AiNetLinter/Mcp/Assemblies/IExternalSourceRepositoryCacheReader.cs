#nullable enable

using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal interface IExternalSourceRepositoryCacheReader
{
    bool TryReadCurrent(
        ExternalSourceRepositoryCacheKey key,
        out ExternalSourceRepositoryCacheReadResult? result,
        out ExternalSourceConfigurationDiagnostic? diagnostic);
}
