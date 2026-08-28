#nullable enable

using System;
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
        AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.MappingsJsonInvalid, mappingsPath);
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
        AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.RequiredFieldMissing, "($)");
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
        AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.UnknownField, "branch");
    }

    [Fact]
    public void Load_GueltigeMapping_NormalisiertSolutionPathUndAssemblySuffix()
    {
        using var tempDir = TestTempDirectory.Create("external-source-normalized-");
        var mappingsPath = tempDir.CreateFile(
            "mappings.json",
            "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", \"solutionPath\": \"./src/../src/Shared.SLNX\", \"assemblies\": [\" Foo.DLL \", \"Bar\"] }] }");
        var settingsPath = WriteSettings(tempDir, mappingsPath);

        var mapping = AssertSingleMapping(ExternalSourceConfigurationLoader.Load(settingsPath));

        Assert.Equal("https://gitea.example/shared.git", mapping.Url);
        Assert.Equal("src/Shared.SLNX", mapping.SolutionPath);
        Assert.Equal(["Foo", "Bar"], mapping.Assemblies);
    }

    [Theory]
    [InlineData("file:///repo/source.sln", "external-source-url-invalid")]
    [InlineData("https://", "external-source-url-invalid")]
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
        AssertDiagnosis(result, code, "repositories[0]");
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
        AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.AssemblyNameInvalid, "repositories[0].assemblies[0]");
    }

    [Fact]
    public void Load_ExternalSourcesOhneMappingsPath_LiefertFehler()
    {
        using var tempDir = TestTempDirectory.Create("external-source-path-missing-");
        var settingsPath = tempDir.CreateFile("appsettings.json", "{ \"ExternalSources\": {} }");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        AssertDiagnosis(result, ExternalSourceConfigurationDiagnosticCodes.MappingsPathMissing, "ExternalSources");
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

    private static string WriteSettings(TestTempDirectory tempDir, string mappingsPath) =>
        tempDir.CreateFile(
            "appsettings.json",
            $$"""{ "ExternalSources": { "MappingsPath": {{JsonSerializer.Serialize(mappingsPath)}} } }""");

    private static string ValidMappings(string assembly) =>
        $$"""{ "repositories": [{ "url": "https://gitea.example/shared.git", "solutionPath": "src/Shared.slnx", "assemblies": [{{JsonSerializer.Serialize(assembly)}}] }] }""";

    private static void AssertDiagnosis(
        ExternalSourceConfigurationLoadResult result,
        string code,
        string locationPart)
    {
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == code
            && diagnostic.Severity == "error"
            && diagnostic.Location.Contains(locationPart, StringComparison.Ordinal));
    }
}
