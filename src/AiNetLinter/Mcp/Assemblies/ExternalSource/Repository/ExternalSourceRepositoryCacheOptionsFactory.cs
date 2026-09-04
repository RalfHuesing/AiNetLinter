#nullable enable

using System;
using System.IO;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Daemon;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal sealed record ExternalSourceRepositoryCacheConstruction(
    string CacheRoot,
    string SourceRoot,
    TimeSpan RefreshInterval)
{
    internal LocalExternalSourceRepositoryCacheWriter CreateWriter() =>
        new(SourceRoot);

    internal ExternalSourceRepositoryCacheRefreshPolicy CreateRefreshPolicy(
        TimeProvider? timeProvider = null) =>
        new(timeProvider, RefreshInterval);
}

internal static class ExternalSourceRepositoryCacheOptionsFactory
{
    internal static ExternalSourceRepositoryCacheConstruction Create(
        ExternalSourceCacheOptions options,
        string? daemonProfile = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var normalizedProfile = DaemonInstanceId.Normalize(daemonProfile);
        var configuredCacheRoot = normalizedProfile is null
            ? options.CacheRoot
            : options.CacheRoot + "." + normalizedProfile;
        var cacheRoot = ExternalSourceConfigurationPath.TryCanonicalizeCacheRoot(
            configuredCacheRoot)
            ?? throw new ArgumentException(
                ExternalSourceCacheOptions.InvalidCacheRootMessage,
                nameof(options));
        var sourceRoot = ExternalSourceRepositoryCacheContract.TryCanonicalizeAbsoluteRoot(
            Path.Combine(cacheRoot, ExternalSourceRepositoryCacheContract.SourceDirectoryName))
            ?? throw new ArgumentException(
                "Die externe Source-Cache-Wurzel konnte nicht erzeugt werden.",
                nameof(options));
        return new(cacheRoot, sourceRoot, options.RefreshInterval);
    }
}
