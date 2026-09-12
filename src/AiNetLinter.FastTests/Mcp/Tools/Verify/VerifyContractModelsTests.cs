#nullable enable

using System;
using AiNetLinter.Mcp.Tools.Verify;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Verify;

[Trait("Category", "Unit")]
public sealed class VerifyContractModelsTests
{
    [Theory]
    [InlineData(null, "Changes")]
    [InlineData("changes", "Changes")]
    [InlineData("solution", "Solution")]
    public void TryParseScope_ContractValuesResolveToPublishedScope(string? value, string expected)
    {
        var parsed = VerifyContract.TryParseScope(value, out var actual);

        Assert.True(parsed);
        Assert.Equal(expected, actual.ToString());
    }

    [Theory]
    [InlineData("all")]
    [InlineData("diff")]
    [InlineData("Changes")]
    public void TryParseScope_UnsupportedValueIsRejectedBeforeAnalysis(string value)
    {
        var parsed = VerifyContract.TryParseScope(value, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData(@"C:\\repo\\App.sln")]
    [InlineData(@"C:\\repo\\App.slnx")]
    [InlineData(@"C:\\repo\\APP.SLNX")]
    public void IsSourceSolutionTarget_SourceSolutionIsAccepted(string targetPath) =>
        Assert.True(VerifyContract.IsSourceSolutionTarget(targetPath));

    [Theory]
    [InlineData(@"C:\\repo\\App.dll")]
    [InlineData(@"C:\\repo\\App.exe")]
    [InlineData(@"C:\\repo\\App.csproj")]
    public void IsSourceSolutionTarget_NonSolutionTargetIsRejectedBeforeAnalysis(string targetPath) =>
        Assert.False(VerifyContract.IsSourceSolutionTarget(targetPath));

    [Theory]
    [InlineData(@"C:\\repo\\App.DLL")]
    [InlineData(@"C:\\repo\\App.EXE")]
    public void TryValidateSourceSolutionTarget_UppercaseAssemblyTargetUsesAssemblyError(string targetPath)
    {
        var valid = VerifyContract.TryValidateSourceSolutionTarget(targetPath, out var error);

        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Equal("ASSEMBLY_TARGET_UNSUPPORTED", error.Code);
    }

    [Fact]
    public void GateSummary_ExposesFixedAcceptanceThresholds()
    {
        Assert.Equal(10.0, VerifyGateSummary.RequiredScore);
        Assert.Equal(0, VerifyGateSummary.RequiredViolationCount);
    }
}
