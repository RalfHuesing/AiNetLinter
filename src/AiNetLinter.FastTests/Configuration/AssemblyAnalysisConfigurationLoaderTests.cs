#nullable enable

using System;
using System.IO;
using AiNetLinter.Configuration;

namespace AiNetLinter.FastTests.Configuration;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisConfigurationLoaderTests
{
    [Fact]
    public void Load_FehlenderAbschnittLiefertKonfigurierteDefaults()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-settings-default-");
        var settingsPath = temp.CreateFile("appsettings.json", "{ \"Logging\": { \"MinimumLevel\": \"Debug\" } }");

        var result = AssemblyAnalysisConfigurationLoader.Load(settingsPath);

        Assert.True(result.Succeeded);
        Assert.Equal(Path.GetFullPath(Path.Combine(temp.DirectoryPath, "cache", "asm")), result.Options.CacheRoot);
        Assert.Equal(TimeSpan.FromSeconds(180), result.Options.DecompilationTimeout);
        Assert.Equal(AssemblyAnalysisConfigurationOptions.DefaultResponseBudgetBytes, result.Options.ResponseBudgetBytes);
    }

    [Fact]
    public void Load_RelativenCacheRootUndTimeoutWerdenGegenSettingsAufgeloest()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-settings-valid-");
        var settingsPath = temp.CreateFile(
            "config/appsettings.json",
            $$"""{ "AssemblyAnalysis": { "CacheRoot": "../../persistent-cache", "DecompilationTimeoutSeconds": 45 } }""");

        var result = AssemblyAnalysisConfigurationLoader.Load(settingsPath);

        Assert.True(result.Succeeded);
        Assert.Equal(Path.GetFullPath(Path.Combine(temp.DirectoryPath, "config", "../../persistent-cache")), result.Options.CacheRoot);
        Assert.Equal(TimeSpan.FromSeconds(45), result.Options.DecompilationTimeout);
    }

    [Fact]
    public void Load_AkzeptiertDasMaximaleCancelAfterTimeout()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-settings-max-timeout-");
        var maxSeconds = AssemblyAnalysisConfigurationOptions.MaxDecompilationTimeoutSeconds;
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            $$"""{ "AssemblyAnalysis": { "DecompilationTimeoutSeconds": {{maxSeconds}} } }""");

        var result = AssemblyAnalysisConfigurationLoader.Load(settingsPath);

        Assert.True(result.Succeeded);
        Assert.Equal(TimeSpan.FromSeconds(maxSeconds), result.Options.DecompilationTimeout);
    }

    [Fact]
    public void Load_LehntTimeoutOberhalbDesCancelAfterBereichsStrukturiertAb()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-settings-overflow-timeout-");
        var timeout = AssemblyAnalysisConfigurationOptions.MaxDecompilationTimeoutSeconds + 1;
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            $$"""{ "AssemblyAnalysis": { "DecompilationTimeoutSeconds": {{timeout}} } }""");

        var result = AssemblyAnalysisConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Location.Contains("DecompilationTimeoutSeconds", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("true")]
    [InlineData("null")]
    public void Load_UngueltigerTimeoutLiefertStrukturierteDiagnose(string timeout)
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-settings-invalid-timeout-");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            $$"""{ "AssemblyAnalysis": { "DecompilationTimeoutSeconds": {{timeout}} } }""");

        var result = AssemblyAnalysisConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("assembly-analysis-configuration-invalid", diagnostic.Code);
        Assert.Contains("DecompilationTimeoutSeconds", diagnostic.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_UnbekanntesFeldSchlaegtFailClosedFehl()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-settings-unknown-");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            "{ \"AssemblyAnalysis\": { \"Unexpected\": true } }");

        var result = AssemblyAnalysisConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Location.Contains("Unexpected", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_LiestResponseBudgetUndBegrenztDenTechnischenMaximalwert()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-settings-budget-");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            "{ \"AssemblyAnalysis\": { \"ResponseBudgetBytes\": 24576 } }");

        var result = AssemblyAnalysisConfigurationLoader.Load(settingsPath);

        Assert.True(result.Succeeded);
        Assert.Equal(24576, result.Options.ResponseBudgetBytes);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("32769")]
    [InlineData("1.5")]
    public void Load_LehntUngueltigesResponseBudgetStrukturiertAb(string budget)
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-settings-budget-invalid-");
        var settingsPath = temp.CreateFile(
            "appsettings.json",
            $$"""{ "AssemblyAnalysis": { "ResponseBudgetBytes": {{budget}} } }""");

        var result = AssemblyAnalysisConfigurationLoader.Load(settingsPath);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Location.Contains("ResponseBudgetBytes", StringComparison.Ordinal));
    }
}
