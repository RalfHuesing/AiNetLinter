#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using Xunit;

namespace AiNetLinter.FastTests.Configuration;

[Trait("Category", "Component")]
public sealed class ExternalSourceCacheRootValidationTests
{
    public static IEnumerable<object[]> InvalidCacheRoots =>
    [
        ["https://user:secret@example.invalid/cache"],
        ["https:/user:secret@example.invalid/cache"],
        ["file:/C:/secret"],
        ["//user:secret@host/share/cache"],
        ["?cache"],
        ["#cache"],
        ["C:/temp/a:secret"],
        ["C:/temp/a?b"],
        ["C:/temp/a#b"],
        ["C:/temp/./cache"],
        ["C:/temp/../cache"],
        ["./cache"],
        ["../cache"],
        [@"\\.\C:\secret"],
        [@"\\?\C:\secret"],
        [@"\Device\HarddiskVolume1\secret"],
        [@"\??\C:\secret"],
        [@"\globalroot\Device\HarddiskVolume1\secret"],
        [@"\\server"],
        ["//server"],
        [@"\\server\"],
        ["//server/"],
        ["C:/temp/CON.txt"],
        ["C:/temp/PRN.log"],
        ["C:/temp/AUX.data"],
        ["C:/temp/NUL.bin"],
        ["C:/temp/COM1.txt"],
        ["C:/temp/LPT9.log"],
        ["CON"],
        ["cache|root"]
    ];

    [Theory]
    [MemberData(nameof(InvalidCacheRoots))]
    public void Load_AdversarialCacheRoot_IsRejectedWithoutRawValueOrSecret(string cacheRoot)
    {
        using var temp = TestTempDirectory.Create("external-source-cache-root-adversarial-");
        var mappingsPath = temp.CreateFile("mappings.json", ValidMappings());
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            $$"""{ "ExternalSources": { "MappingsPath": "mappings.json", "CacheRoot": {{JsonSerializer.Serialize(cacheRoot)}} } }""");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.CacheRootInvalid);
        var diagnostics = string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(diagnostic => diagnostic.Message + diagnostic.Location));
        Assert.DoesNotContain(cacheRoot, diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(InvalidCacheRoots))]
    public void Constructor_AdversarialCacheRoot_ThrowsGenericArgumentException(string cacheRoot)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ExternalSourceCacheOptions(cacheRoot, TimeSpan.FromMinutes(5)));

        Assert.DoesNotContain(cacheRoot, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConstructorAndFactory_ValidDriveAndUncRootsRemainUsable()
    {
        using var temp = TestTempDirectory.Create("external-source-cache-root-valid-");
        var roots = new[]
        {
            Path.GetPathRoot(temp.DirectoryPath)!,
            temp.GetPath("cache"),
            @"\\server\share",
            @"\\server\share\cache",
            "//server/share/cache"
        };

        foreach (var root in roots)
        {
            var options = new ExternalSourceCacheOptions(root, TimeSpan.FromMinutes(5));
            var construction = ExternalSourceRepositoryCacheOptionsFactory.Create(options);

            Assert.Equal(Path.GetFullPath(root), options.CacheRoot);
            Assert.Equal(options.CacheRoot, construction.CacheRoot);
            Assert.Equal(
                Path.Combine(options.CacheRoot, ExternalSourceRepositoryCacheContract.SourceDirectoryName),
                construction.SourceRoot);
            Assert.Equal(TimeSpan.FromMinutes(5), construction.RefreshInterval);
        }
    }

    [Fact]
    public void Load_ValidRelativeCacheRootResolvesAgainstSettingsDirectory()
    {
        using var temp = TestTempDirectory.Create("external-source-cache-root-relative-");
        temp.CreateFile("mappings.json", ValidMappings());
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            "{ \"ExternalSources\": { \"MappingsPath\": \"mappings.json\", \"CacheRoot\": \"relative-cache\" } }");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(
            Path.GetFullPath(temp.GetPath("relative-cache")),
            result.Configuration!.CacheOptions.CacheRoot);
    }

    [Theory]
    [InlineData(@"\\server\share")]
    [InlineData(@"\\server\share\cache")]
    [InlineData("//server/share/cache")]
    public void Load_ValidUncCacheRootRemainsCanonical(string uncRoot)
    {
        using var temp = TestTempDirectory.Create("external-source-cache-root-unc-");
        temp.CreateFile("mappings.json", ValidMappings());
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            $$"""{ "ExternalSources": { "MappingsPath": "mappings.json", "CacheRoot": {{JsonSerializer.Serialize(uncRoot)}} } }""");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(Path.GetFullPath(uncRoot), result.Configuration!.CacheOptions.CacheRoot);
    }

    private static string ValidMappings() =>
        "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", "
        + "\"solutionPath\": \"src/Shared.slnx\", \"assemblies\": [\"TargetAssembly\"] }] }";
}
