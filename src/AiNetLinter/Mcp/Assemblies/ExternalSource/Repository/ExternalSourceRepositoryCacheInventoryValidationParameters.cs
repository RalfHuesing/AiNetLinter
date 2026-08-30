#nullable enable

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal sealed record ExternalSourceRepositoryCacheInventoryValidationParameters
{
    internal ExternalSourceRepositoryCacheReadRequest Request { get; init; } = null!;
    internal string GenerationName { get; init; } = string.Empty;
    internal string GenerationDirectory { get; init; } = string.Empty;
    internal ExternalSourceRepositoryCacheManifest Manifest { get; init; } = null!;
    internal ExternalSourceRepositoryCacheInventory Inventory { get; init; } = null!;
}
