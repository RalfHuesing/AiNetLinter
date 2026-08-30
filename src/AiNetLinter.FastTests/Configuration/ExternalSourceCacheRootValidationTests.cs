#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.FastTests.Configuration;

[Trait("Category", "Component")]
public sealed class ExternalSourceCacheRootValidationTests
{
    [Theory]
    [InlineData("https:/user:secret@example.invalid/cache")]
    [InlineData("file:/C:/secret")]
    [InlineData("C:/temp/a:secret")]
    [InlineData("C:/temp/a?b")]
    [InlineData("C:/temp/a#b")]
    [InlineData("C:/temp/./cache")]
    [InlineData("C:/temp/../cache")]
    [InlineData(@"\\.\C:\secret")]
    [InlineData(@"\\?\C:\secret")]
    [InlineData(@"\Device\HarddiskVolume1\secret")]
    [InlineData(@"\??\C:\secret")]
    [InlineData("C:/temp/CON.txt")]
    [InlineData("C:/temp/PRN.log")]
    [InlineData("C:/temp/COM1.txt")]
    [InlineData("CON")]
    [InlineData("cache|root")]
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
    [InlineData("https:/user:secret@example.invalid/cache")]
    [InlineData("file:/C:/secret")]
    [InlineData("C:/temp/a:secret")]
    [InlineData("C:/temp/a?b")]
    [InlineData("C:/temp/a#b")]
    [InlineData("C:/temp/./cache")]
    [InlineData(@"\\.\C:\secret")]
    [InlineData(@"\\?\C:\secret")]
    [InlineData(@"\Device\HarddiskVolume1\secret")]
    [InlineData(@"\??\C:\secret")]
    [InlineData("C:/temp/CON.txt")]
    [InlineData("C:/temp/PRN.log")]
    [InlineData("C:/temp/COM1.txt")]
    public void Constructor_AdversarialCacheRoot_ThrowsGenericArgumentException(string cacheRoot)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ExternalSourceCacheOptions(cacheRoot, TimeSpan.FromMinutes(5)));

        Assert.DoesNotContain(cacheRoot, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ValidDriveAndUncRootsRemainUsable()
    {
        using var temp = TestTempDirectory.Create("external-source-cache-root-valid-");
        var driveRoot = temp.GetPath("cache");
        var driveOptions = new ExternalSourceCacheOptions(
            driveRoot,
            TimeSpan.FromMinutes(5));
        var uncRoot = @"\\server\share\cache";
        var uncOptions = new ExternalSourceCacheOptions(
            uncRoot,
            TimeSpan.FromMinutes(5));

        Assert.Equal(Path.GetFullPath(driveRoot), driveOptions.CacheRoot);
        Assert.Equal(Path.GetFullPath(uncRoot), uncOptions.CacheRoot);
    }

    [Fact]
    public void Load_ValidUncCacheRootRemainsCanonical()
    {
        using var temp = TestTempDirectory.Create("external-source-cache-root-unc-");
        temp.CreateFile("mappings.json", ValidMappings());
        var uncRoot = @"\\server\share\cache";
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
