#nullable enable

using System;
using System.IO;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

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
        ExternalSourceCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var cacheRoot = ExternalSourceConfigurationPath.TryCanonicalizeCacheRoot(
            options.CacheRoot)
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
