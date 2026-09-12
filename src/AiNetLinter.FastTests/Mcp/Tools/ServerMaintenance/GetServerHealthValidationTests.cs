#nullable enable

using AiNetLinter.Mcp.Tools.ServerMaintenance;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.ServerMaintenance;

[Trait("Category", "Unit")]
public sealed class GetServerHealthValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateOptions_NonPositiveDiagnosticLimit_ReturnsFieldAwareInvalidArgument(int maxDiagnostics)
    {
        var result = GetServerHealthTool.ValidateOptions(new GetServerHealthOptions(MaxDiagnostics: maxDiagnostics));

        Assert.NotNull(result);
        Assert.False(result!.IsError);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.maxDiagnostics", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }
}
