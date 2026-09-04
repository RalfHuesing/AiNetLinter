#nullable enable

using System;
using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.FastTests.Configuration;

[Trait("Category", "Component")]
public sealed class ExternalSourceSourceModeTests
{
    [Theory]
    [InlineData("source_required", (int)ExternalSourceSourceMode.SourceRequired)]
    [InlineData("source_preferred", (int)ExternalSourceSourceMode.SourcePreferred)]
    [InlineData("decompilation_allowed", (int)ExternalSourceSourceMode.DecompilationAllowed)]
    public void TryParse_RecognizesSupportedWireValues(string value, int expected)
    {
        Assert.True(ExternalSourceSourceModeExtensions.TryParse(value, out var mode));
        Assert.Equal((ExternalSourceSourceMode)expected, mode);
        Assert.Equal(value, mode.ToWireValue());
    }

    [Fact]
    public void Load_UnbekannterSourceMode_LiefertKontrollierteDiagnose()
    {
        using var tempDir = TestTempDirectory.Create("external-source-mode-invalid-");
        tempDir.CreateFile("mappings.json", "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", \"solutionPath\": \"src/Shared.slnx\", \"assemblies\": [\"Foo\"] }] }");
        var settingsPath = tempDir.CreateFile(
            "appsettings.json",
            "{ \"ExternalSources\": { \"MappingsPath\": \"mappings.json\", \"SourceMode\": \"unknown\" } }");

        var result = ExternalSourceConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.InvalidFieldType
                && diagnostic.Location.Contains("SourceMode", StringComparison.Ordinal));
    }
}
