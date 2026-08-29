#nullable enable

using System;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryAcquirerFactory
{
    internal static ExternalSourceRepositoryAcquirer CreateConfigured(
        IGiteaRepositoryTransport transport,
        string stagingRoot,
        ExternalSourceCacheOptions cacheOptions,
        ExternalSourceRepositoryCacheRefreshPolicy? refreshPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(cacheOptions);
        var construction = ExternalSourceRepositoryCacheOptionsFactory.Create(cacheOptions);
        var writer = construction.CreateWriter();
        return new(
            transport,
            stagingRoot,
            cacheWriter: writer,
            cacheReader: writer,
            refreshPolicy: refreshPolicy ?? construction.CreateRefreshPolicy());
    }
}
