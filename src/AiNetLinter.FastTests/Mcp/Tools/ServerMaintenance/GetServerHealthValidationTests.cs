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
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains("fieldPath: $.maxDiagnostics", text, StringComparison.Ordinal);
    }
}
