#nullable enable

using System;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal sealed class ExternalSourceRepositoryCacheRefreshPolicy
{
    internal static readonly TimeSpan DefaultRefreshInterval =
        ExternalSourceCacheOptions.DefaultRefreshInterval;

    private readonly TimeProvider timeProvider;

    internal ExternalSourceRepositoryCacheRefreshPolicy(
        TimeProvider? timeProvider = null,
        TimeSpan? refreshInterval = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
        RefreshInterval = refreshInterval ?? DefaultRefreshInterval;
        if (RefreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshInterval));
        }
    }

    internal TimeSpan RefreshInterval { get; }

    internal bool IsStale(ExternalSourceRepositoryCacheManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.CreatedUtc.Kind is not DateTimeKind.Utc)
        {
            return true;
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (manifest.CreatedUtc > nowUtc)
        {
            return true;
        }

        try
        {
            return nowUtc >= manifest.CreatedUtc.Add(RefreshInterval);
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }
}
