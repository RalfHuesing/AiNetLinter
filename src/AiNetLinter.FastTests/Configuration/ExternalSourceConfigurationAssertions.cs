#nullable enable

using System;
using System.IO;
using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.FastTests.Configuration;

internal static class ExternalSourceConfigurationAssertions
{
    public static void AssertDiagnosis(
        ExternalSourceConfigurationLoadResult result,
        string code,
        string locationPart)
    {
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == code
            && diagnostic.Severity == "error"
            && diagnostic.Location.Contains(locationPart, StringComparison.Ordinal));
    }

    public static void AssertDefaultCacheOptions(ExternalSourceConfiguration configuration)
    {
        Assert.Equal(
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                ExternalSourceCacheOptions.DefaultCacheDirectoryName)),
            configuration.CacheOptions.CacheRoot);
        Assert.Equal(
            ExternalSourceCacheOptions.DefaultRefreshInterval,
            configuration.CacheOptions.RefreshInterval);
        Assert.Equal(
            ExternalSourceResourceOptions.DefaultMaxDiskBytes,
            configuration.CacheOptions.ResourceOptions.MaxDiskBytes);
        Assert.Equal(
            ExternalSourceResourceOptions.DefaultMaxMemoryBytes,
            configuration.CacheOptions.ResourceOptions.MaxMemoryBytes);
        Assert.Equal(
            ExternalSourceResourceOptions.DefaultMaxParallelOperations,
            configuration.CacheOptions.ResourceOptions.MaxParallelOperations);
        Assert.Equal(
            ExternalSourceResourceOptions.DefaultMaxResidentResources,
            configuration.CacheOptions.ResourceOptions.MaxResidentResources);
        Assert.Equal(
            ExternalSourceResourceOptions.DefaultIdleTtl,
            configuration.CacheOptions.ResourceOptions.IdleTtl);
    }
}
