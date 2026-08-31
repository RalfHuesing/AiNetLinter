#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.FastTests.Configuration;

[Trait("Category", "Component")]
public sealed class ExternalSourceConfigurationLoaderTests
{
    [Fact]
    public void Load_FehlendeAppSettings_LiefertLeereKonfiguration()
    {
        using var tempDir = TestTempDirectory.Create("external-source-settings-missing-");

        var result = ExternalSourceConfigurationLoader.Load(tempDir.GetPath("appsettings.json"));

        Assert.True(result.Succeeded);
        Assert.True(result.Configuration!.IsEmpty);
        Assert.Empty(result.Diagnostics);
        ExternalSourceConfigurationAssertions.AssertDefaultCacheOptions(result.Configuration);
    }

    [Fact]
    public void Load_FehlenderExternalSourcesAbschnitt_LiefertLeereKonfiguration()
    {
        using var tempDir = TestTempDirectory.Create("external-source-section-missing-");
        var settingsPath = tempDir.CreateFile("appsettings.json", "{ \"Logging\": { \"MinimumLevel\": \"Debug\" } }");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.True(result.Succeeded);
        Assert.True(result.Configuration!.IsEmpty);
        Assert.Empty(result.Diagnostics);
        ExternalSourceConfigurationAssertions.AssertDefaultCacheOptions(result.Configuration);
    }

    [Fact]
    public void Load_DefektesAppSettingsJson_LiefertSettingsJsonDiagnose()
    {
        using var tempDir = TestTempDirectory.Create("external-source-settings-json-");
        var settingsPath = tempDir.CreateFile("appsettings.json", "{ \"ExternalSources\": ");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ExternalSourceConfigurationDiagnosticCodes.SettingsJsonInvalid, diagnostic.Code);
        Assert.Equal("error", diagnostic.Severity);
        Assert.Contains(settingsPath, diagnostic.Location, StringComparison.Ordinal);
        Assert.Contains("($)", diagnostic.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RelativerMappingsPath_WirdGegenSettingsVerzeichnisAufgeloest()
    {
        using var tempDir = TestTempDirectory.Create("external-source-relative-path-");
        tempDir.CreateFile("config/external-sources.json", ValidMappings("Foo.dll"));
        var settingsPath = tempDir.CreateFile(
            "config/appsettings.json",
            "{ \"ExternalSources\": { \"MappingsPath\": \"external-sources.json\" } }");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        var mapping = AssertSingleMapping(result);
        Assert.Equal("Foo", mapping.Assemblies.Single());
    }

    [Fact]
    public void Load_AbsoluterMappingsPath_BleibtAbsolut()
    {
        using var tempDir = TestTempDirectory.Create("external-source-absolute-path-");
        var mappingsPath = tempDir.CreateFile("mappings.json", ValidMappings("Foo"));
        var settingsPath = tempDir.CreateFile(
            "appsettings.json",
            $$"""{ "ExternalSources": { "MappingsPath": {{JsonSerializer.Serialize(mappingsPath)}} } }""");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        AssertSingleMapping(result);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Load_RelativerCacheRoot_WirdGegenSettingsVerzeichnisAufgeloest()
    {
        using var tempDir = TestTempDirectory.Create("external-source-cache-root-relative-");
        var mappingsPath = tempDir.CreateFile("config/mappings.json", ValidMappings("Foo"));
        var settingsPath = WriteSettings(
            tempDir,
            mappingsPath,
            JsonSerializer.Serialize("cache-root"),
            "15",
            "config/appsettings.json");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        AssertSingleMapping(result);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(settingsPath)!, "cache-root")),
            result.Configuration!.CacheOptions.CacheRoot);
        Assert.Equal(TimeSpan.FromMinutes(15), result.Configuration.CacheOptions.RefreshInterval);
    }

    [Fact]
    public void Load_AbsoluterCacheRoot_BleibtKanonischAbsolut()
    {
        using var tempDir = TestTempDirectory.Create("external-source-cache-root-absolute-");
        var mappingsPath = tempDir.CreateFile("mappings.json", ValidMappings("Foo"));
        var cacheRoot = tempDir.GetPath("absolute-cache-root");
        var settingsPath = WriteSettings(
            tempDir,
            mappingsPath,
            JsonSerializer.Serialize(cacheRoot),
            "60");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        AssertSingleMapping(result);
        Assert.Equal(Path.GetFullPath(cacheRoot), result.Configuration!.CacheOptions.CacheRoot);
        Assert.Equal(ExternalSourceCacheOptions.DefaultRefreshInterval, result.Configuration.CacheOptions.RefreshInterval);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \\t\"")]
    [InlineData("\"../outside\"")]
    [InlineData("\"./cache\"")]
    [InlineData("\"https://user:secret@example.invalid/cache\"")]
    public void Load_UngueltigerCacheRoot_LiefertFailClosedDiagnose(string cacheRootJson)
    {
        using var tempDir = TestTempDirectory.Create("external-source-cache-root-invalid-");
        var mappingsPath = tempDir.CreateFile("mappings.json", ValidMappings("Foo"));
        var settingsPath = WriteSettings(tempDir, mappingsPath, cacheRootJson);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        ExternalSourceConfigurationAssertions.AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.CacheRootInvalid, "CacheRoot");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_DoppelteOderUnbekannteCacheFelder_LiefertStrukturierteDiagnose()
    {
        using var tempDir = TestTempDirectory.Create("external-source-cache-fields-invalid-");
        var mappingsPath = tempDir.CreateFile("mappings.json", ValidMappings("Foo"));
        var settingsPath = tempDir.CreateFile(
            "appsettings.json",
            $$"""{ "ExternalSources": { "MappingsPath": {{JsonSerializer.Serialize(mappingsPath)}}, "CacheRoot": "cache", "CacheRoot": "other", "Unexpected": true } }""");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.DuplicateField);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.UnknownField);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("60")]
    public void Load_GueltigesRefreshInterval_WirdAlsTimeSpanGespeichert(string refreshIntervalJson)
    {
        using var tempDir = TestTempDirectory.Create("external-source-refresh-interval-valid-");
        var mappingsPath = tempDir.CreateFile("mappings.json", ValidMappings("Foo"));
        var settingsPath = WriteSettings(tempDir, mappingsPath, null, refreshIntervalJson);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        AssertSingleMapping(result);
        Assert.Equal(
            TimeSpan.FromMinutes(long.Parse(refreshIntervalJson)),
            result.Configuration!.CacheOptions.RefreshInterval);
    }

    [Fact]
    public void Load_MaximalesGanzesRefreshInterval_WirdAkzeptiert()
    {
        using var tempDir = TestTempDirectory.Create("external-source-refresh-interval-max-");
        var mappingsPath = tempDir.CreateFile("mappings.json", ValidMappings("Foo"));
        var maxMinutes = ExternalSourceCacheOptions.MaxRefreshIntervalMinutes;
        var settingsPath = WriteSettings(tempDir, mappingsPath, null, maxMinutes.ToString());

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        AssertSingleMapping(result);
        Assert.Equal(
            TimeSpan.FromTicks(maxMinutes * TimeSpan.TicksPerMinute),
            result.Configuration!.CacheOptions.RefreshInterval);
    }

    [Theory]
    [InlineData("null", "external-source-invalid-field-type")]
    [InlineData("true", "external-source-invalid-field-type")]
    [InlineData("\"60\"", "external-source-invalid-field-type")]
    [InlineData("60.5", "external-source-invalid-field-type")]
    [InlineData("0", "external-source-refresh-interval-invalid")]
    [InlineData("-1", "external-source-refresh-interval-invalid")]
    [InlineData("9223372036854775807", "external-source-refresh-interval-invalid")]
    public void Load_UngueltigesRefreshInterval_LiefertFailClosedDiagnose(
        string refreshIntervalJson,
        string code)
    {
        using var tempDir = TestTempDirectory.Create("external-source-refresh-interval-invalid-");
        var mappingsPath = tempDir.CreateFile("mappings.json", ValidMappings("Foo"));
        var settingsPath = WriteSettings(tempDir, mappingsPath, null, refreshIntervalJson);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        ExternalSourceConfigurationAssertions.AssertDiagnosis(result, code, "RefreshIntervalMinutes");
    }

    [Fact]
    public void Load_FehlendeMappingsDatei_LiefertSichtbareDiagnose()
    {
        using var tempDir = TestTempDirectory.Create("external-source-mappings-missing-");
        var settingsPath = tempDir.CreateFile(
            "appsettings.json",
            "{ \"ExternalSources\": { \"MappingsPath\": \"missing.json\" } }");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ExternalSourceConfigurationDiagnosticCodes.MappingsPathInvalid, diagnostic.Code);
        Assert.Contains("missing.json", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_DefektesMappingsJson_LiefertMappingJsonDiagnose()
    {
        using var tempDir = TestTempDirectory.Create("external-source-mappings-json-");
        var mappingsPath = tempDir.CreateFile("mappings.json", "{ \"repositories\": [");
        var settingsPath = WriteSettings(tempDir, mappingsPath);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        ExternalSourceConfigurationAssertions.AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.MappingsJsonInvalid, mappingsPath);
    }

    [Fact]
    public void Load_DoppelteRepositoriesProperty_LiefertGenauEineDuplicateDiagnose()
    {
        using var tempDir = TestTempDirectory.Create("external-source-duplicate-property-");
        var mappingsPath = tempDir.CreateFile(
            "mappings.json",
            "{ \"repositories\": [], \"repositories\": [] }");
        var settingsPath = WriteSettings(tempDir, mappingsPath);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.DuplicateField
            && diagnostic.Location.Contains("$.repositories", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RequiredFieldMissing);
    }

    [Fact]
    public void Load_FehlendesRepositoriesProperty_LiefertRequiredDiagnose()
    {
        using var tempDir = TestTempDirectory.Create("external-source-required-property-");
        var mappingsPath = tempDir.CreateFile("mappings.json", "{}");
        var settingsPath = WriteSettings(tempDir, mappingsPath);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        ExternalSourceConfigurationAssertions.AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.RequiredFieldMissing, "($)");
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.DuplicateField);
    }

    [Fact]
    public void Load_UnbekannteFelder_MarkierenMappingAlsNichtVerwendbar()
    {
        using var tempDir = TestTempDirectory.Create("external-source-unknown-field-");
        var mappingsPath = tempDir.CreateFile(
            "mappings.json",
            "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", \"solutionPath\": \"src/Shared.slnx\", \"assemblies\": [\"Foo.dll\"], \"branch\": \"main\" }] }");
        var settingsPath = WriteSettings(tempDir, mappingsPath);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        ExternalSourceConfigurationAssertions.AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.UnknownField, "branch");
    }

    [Fact]
    public void Load_GueltigeMapping_NormalisiertSolutionPathUndAssemblySuffix()
    {
        using var tempDir = TestTempDirectory.Create("external-source-normalized-");
        var mappingsPath = tempDir.CreateFile(
            "mappings.json",
            "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", \"solutionPath\": \"./src/../src/Shared.SLNX\", \"assemblies\": [\" Foo.DLL \", \"Bar.exe\"] }] }");
        var settingsPath = WriteSettings(tempDir, mappingsPath);

        var mapping = AssertSingleMapping(ExternalSourceConfigurationLoader.Load(settingsPath));

        Assert.Equal("https://gitea.example/shared.git", mapping.Url);
        Assert.Equal("src/Shared.SLNX", mapping.SolutionPath);
        Assert.Equal(["Foo", "Bar"], mapping.Assemblies);
    }

    [Theory]
    [InlineData("file:///repo/source.sln", "external-source-url-invalid")]
    [InlineData("https://", "external-source-url-invalid")]
    [InlineData("https://build-user:secret@example.invalid/repository", "external-source-url-invalid")]
    [InlineData("https://gitea.example/shared.git?branch=main", "external-source-url-invalid")]
    [InlineData("https://gitea.example/shared.git#main", "external-source-url-invalid")]
    [InlineData("https://gitea.example/shared.git", "external-source-solution-path-invalid")]
    public void Load_UngueltigeUrlOderSolutionPath_LiefertStabileDiagnose(string url, string code)
    {
        using var tempDir = TestTempDirectory.Create("external-source-invalid-path-");
        var solutionPath = code == "external-source-solution-path-invalid" ? "../Shared.slnx" : "src/Shared.slnx";
        var mappingsPath = tempDir.CreateFile(
            "mappings.json",
            $$"""{ "repositories": [{ "url": {{JsonSerializer.Serialize(url)}}, "solutionPath": {{JsonSerializer.Serialize(solutionPath)}}, "assemblies": ["Foo.dll"] }] }""");
        var settingsPath = WriteSettings(tempDir, mappingsPath);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        ExternalSourceConfigurationAssertions.AssertDiagnosis(result, code, "repositories[0]");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_LeereOderNichtEindeutigeAssemblies_LiefertDiagnosen()
    {
        using var tempDir = TestTempDirectory.Create("external-source-assemblies-invalid-");
        var mappingsPath = tempDir.CreateFile(
            "mappings.json",
            "{ \"repositories\": ["
            + "{ \"url\": \"https://gitea.example/one.git\", \"solutionPath\": \"one.sln\", \"assemblies\": [\"Foo.dll\"] },"
            + "{ \"url\": \"https://gitea.example/two.git\", \"solutionPath\": \"two.slnx\", \"assemblies\": [\"Foo\", \"foo\"] },"
            + "{ \"url\": \"https://gitea.example/three.git\", \"solutionPath\": \"three.sln\", \"assemblies\": [\"Foo\"] },"
            + "{ \"url\": \"https://gitea.example/four.git\", \"solutionPath\": \"four.sln\", \"assemblies\": [] }"
            + "] }");
        var settingsPath = WriteSettings(tempDir, mappingsPath);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.DuplicateAssembly);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.AmbiguousAssembly);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.AssemblyListInvalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Load_LeererOderWhitespaceAssemblyName_LiefertAssemblyNameDiagnose(string assemblyName)
    {
        using var tempDir = TestTempDirectory.Create("external-source-assembly-name-invalid-");
        var mappingsPath = tempDir.CreateFile(
            "mappings.json",
            $$"""{ "repositories": [{ "url": "https://gitea.example/shared.git", "solutionPath": "src/Shared.slnx", "assemblies": [{{JsonSerializer.Serialize(assemblyName)}}] }] }""");
        var settingsPath = WriteSettings(tempDir, mappingsPath);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        ExternalSourceConfigurationAssertions.AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.AssemblyNameInvalid, "repositories[0].assemblies[0]");
    }

    [Fact]
    public void Load_ExternalSourcesOhneMappingsPath_LiefertFehler()
    {
        using var tempDir = TestTempDirectory.Create("external-source-path-missing-");
        var settingsPath = tempDir.CreateFile("appsettings.json", "{ \"ExternalSources\": {} }");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        ExternalSourceConfigurationAssertions.AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.MappingsPathMissing, "ExternalSources");
    }

    [Fact]
    public void Load_ExterneRessourcenlimits_WerdenValidiertUndGespeichert()
    {
        using var tempDir = TestTempDirectory.Create("external-source-resource-limits-valid-");
        var mappingsPath = tempDir.CreateFile("mappings.json", ValidMappings("Foo"));
        var settingsPath = WriteSettings(
            tempDir,
            mappingsPath,
            resourceFields: "\"MaxDiskBytes\": 100, \"MaxMemoryBytes\": 200, \"MaxParallelOperations\": 3, \"MaxResidentResources\": 5, \"IdleTtlMinutes\": 0.5");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        AssertSingleMapping(result);
        var limits = result.Configuration!.CacheOptions.ResourceOptions;
        Assert.Equal(100, limits.MaxDiskBytes);
        Assert.Equal(200, limits.MaxMemoryBytes);
        Assert.Equal(3, limits.MaxParallelOperations);
        Assert.Equal(5, limits.MaxResidentResources);
        Assert.Equal(TimeSpan.FromSeconds(30), limits.IdleTtl);
    }

    [Theory]
    [InlineData("MaxDiskBytes", "0")]
    [InlineData("MaxMemoryBytes", "-1")]
    [InlineData("MaxParallelOperations", "0")]
    [InlineData("MaxResidentResources", "0")]
    [InlineData("IdleTtlMinutes", "0")]
    [InlineData("MaxDiskBytes", "9223372036854775807")]
    public void Load_UngueltigesExternesRessourcenlimit_LiefertFailClosedDiagnose(
        string propertyName,
        string value)
    {
        using var tempDir = TestTempDirectory.Create("external-source-resource-limits-invalid-");
        var mappingsPath = tempDir.CreateFile("mappings.json", ValidMappings("Foo"));
        var settingsPath = WriteSettings(
            tempDir,
            mappingsPath,
            resourceFields: $"\"{propertyName}\": {value}");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Null(result.Configuration);
        ExternalSourceConfigurationAssertions.AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.ResourceLimitInvalid, propertyName);
    }

    [Fact]
    public void Load_DiagnosenSindStrukturiertUndEnthaltenFundstelle()
    {
        using var tempDir = TestTempDirectory.Create("external-source-diagnostic-");
        var mappingsPath = tempDir.CreateFile("mappings.json", "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", \"solutionPath\": \"../Shared.slnx\", \"assemblies\": [\"Foo.dll\"] }] }");
        var settingsPath = WriteSettings(tempDir, mappingsPath);

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(ExternalSourceConfigurationDiagnosticCodes.SolutionPathInvalid, diagnostic.Code);
        Assert.Equal("error", diagnostic.Severity);
        Assert.NotEmpty(diagnostic.Message);
        Assert.Contains(mappingsPath, diagnostic.Location);
        Assert.Contains("solutionPath", diagnostic.Location);
    }

    private static ExternalSourceMapping AssertSingleMapping(ExternalSourceConfigurationLoadResult result)
    {
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.Single(result.Configuration!.Mappings);
    }

    private static string WriteSettings(
        TestTempDirectory tempDir,
        string mappingsPath,
        string? cacheRootJson = null,
        string? refreshIntervalJson = null,
        string relativePath = "appsettings.json",
        string? resourceFields = null)
    {
        var fields = $"\"MappingsPath\": {JsonSerializer.Serialize(mappingsPath)}";
        if (cacheRootJson is not null)
        {
            fields += $", \"CacheRoot\": {cacheRootJson}";
        }

        if (refreshIntervalJson is not null)
        {
            fields += $", \"RefreshIntervalMinutes\": {refreshIntervalJson}";
        }

        if (resourceFields is not null)
        {
            fields += ", " + resourceFields;
        }

        return tempDir.CreateFile(
            relativePath,
            $$"""{ "ExternalSources": { {{fields}} } }""");
    }

    private static string ValidMappings(string assembly) => $$"""{ "repositories": [{ "url": "https://gitea.example/shared.git", "solutionPath": "src/Shared.slnx", "assemblies": [{{JsonSerializer.Serialize(assembly)}}] }] }""";
}