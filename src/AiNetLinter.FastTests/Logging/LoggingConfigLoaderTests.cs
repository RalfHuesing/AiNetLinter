#nullable enable

using System;
using System.IO;
using AiNetLinter.Logging;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Logging;

[Trait("Category", "Unit")]
public sealed class LoggingConfigLoaderTests
{
    [Fact]
    public void Load_FehlendeDatei_LiefertDefaults()
    {
        using var tempDir = TestTempDirectory.Create("logging-config-missing-");
        var config = LoggingConfigLoader.Load(tempDir.DirectoryPath);

        Assert.Equal(LoggingConfig.DefaultMinimumLevel, config.MinimumLevel);
        Assert.Equal(LoggingConfig.DefaultDirectoryName, config.Directory);
        Assert.Equal(LoggingConfig.DefaultRetainedFileCount, config.RetainedFileCount);
        Assert.True(config.McpCallLogging);
    }

    [Fact]
    public void Load_GueltigeDatei_ParstWerte()
    {
        using var tempDir = TestTempDirectory.Create("logging-config-valid-");
        var path = Path.Combine(tempDir.DirectoryPath, LoggingConfigLoader.FileName);
        File.WriteAllText(path,
            """
            {
              "Logging": {
                "MinimumLevel": "Warning",
                "Directory": "C:/temp/meine-logs",
              "RetainedFileCount": 7,
              "McpCallLogging": false
              }
            }
            """);

        var config = LoggingConfigLoader.Load(tempDir.DirectoryPath);

        Assert.Equal("Warning", config.MinimumLevel);
        Assert.Equal("C:/temp/meine-logs", config.Directory);
        Assert.Equal(7, config.RetainedFileCount);
        Assert.False(config.McpCallLogging);
    }

    [Fact]
    public void Load_TeilweiseAngaben_ErgaenztDefaults()
    {
        using var tempDir = TestTempDirectory.Create("logging-config-partial-");
        var path = Path.Combine(tempDir.DirectoryPath, LoggingConfigLoader.FileName);
        File.WriteAllText(path, """{ "Logging": { "MinimumLevel": "Information" } }""");

        var config = LoggingConfigLoader.Load(tempDir.DirectoryPath);

        Assert.Equal("Information", config.MinimumLevel);
        Assert.Equal(LoggingConfig.DefaultDirectoryName, config.Directory);
        Assert.Equal(LoggingConfig.DefaultRetainedFileCount, config.RetainedFileCount);
        Assert.True(config.McpCallLogging);
    }

    [Fact]
    public void Load_DefektesJson_WirftHarteFehlermeldung()
    {
        using var tempDir = TestTempDirectory.Create("logging-config-broken-");
        var path = Path.Combine(tempDir.DirectoryPath, LoggingConfigLoader.FileName);
        File.WriteAllText(path, "{ \"Logging\": ");

        var exception = Assert.Throws<InvalidDataException>(
            () => LoggingConfigLoader.Load(tempDir.DirectoryPath));

        Assert.Contains("[CONFIG]", exception.Message);
        Assert.Contains(LoggingConfigLoader.FileName, exception.Message);
    }

    [Fact]
    public void Load_UnbekannterSchluessel_Wirft()
    {
        using var tempDir = TestTempDirectory.Create("logging-config-unknown-key-");
        var path = Path.Combine(tempDir.DirectoryPath, LoggingConfigLoader.FileName);
        File.WriteAllText(path, """{ "Logging": { "PathFormat": "rolling" } }""");

        var exception = Assert.Throws<InvalidDataException>(
            () => LoggingConfigLoader.Load(tempDir.DirectoryPath));

        Assert.Contains("PathFormat", exception.Message);
    }

    [Fact]
    public void Load_UngueltigesLevel_Wirft()
    {
        using var tempDir = TestTempDirectory.Create("logging-config-invalid-level-");
        var path = Path.Combine(tempDir.DirectoryPath, LoggingConfigLoader.FileName);
        File.WriteAllText(path, """{ "Logging": { "MinimumLevel": "Trace" } }""");

        var exception = Assert.Throws<InvalidDataException>(
            () => LoggingConfigLoader.Load(tempDir.DirectoryPath));

        Assert.Contains("MinimumLevel", exception.Message);
        Assert.Contains("Erlaubt", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void Load_RetainedFileCountAusserhalbBereichs_Wirft(int retainedFileCount)
    {
        using var tempDir = TestTempDirectory.Create("logging-config-retained-");
        var path = Path.Combine(tempDir.DirectoryPath, LoggingConfigLoader.FileName);
        File.WriteAllText(path, $$"""{ "Logging": { "RetainedFileCount": {{retainedFileCount}} } }""");

        Assert.Throws<InvalidDataException>(() => LoggingConfigLoader.Load(tempDir.DirectoryPath));
    }

    [Fact]
    public void Load_UngueltigerMcpCallLoggingWert_WirftHarteFehlermeldung()
    {
        using var tempDir = TestTempDirectory.Create("logging-config-mcp-call-logging-");
        var path = Path.Combine(tempDir.DirectoryPath, LoggingConfigLoader.FileName);
        File.WriteAllText(path, """{ "Logging": { "McpCallLogging": "yes" } }""");

        var exception = Assert.Throws<InvalidDataException>(
            () => LoggingConfigLoader.Load(tempDir.DirectoryPath));

        Assert.Contains("McpCallLogging", exception.Message);
    }

    [Fact]
    public void ResolveDirectory_RelativerPfad_BasiertAufExeVerzeichnis()
    {
        var config = new LoggingConfig("Debug", "meine-logs", 3);

        var resolved = config.ResolveDirectory();

        Assert.True(Path.IsPathRooted(resolved));
        Assert.EndsWith("meine-logs", resolved, StringComparison.Ordinal);
    }
}
