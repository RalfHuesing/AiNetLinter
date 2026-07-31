using System.Threading.Tasks;
using Xunit;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

// @covers DebtReportBuilder
public sealed class DebtReportBuilderHeaderTests
{
    [Fact]
    public async Task DebtReportBuilder_WithoutIgnoreSuppressions_HeaderStandard()
    {
        // Act
        var report = await DebtReportBuilder.BuildAsync(".");

        // Assert
        Assert.StartsWith("# AiNetLinter - debt report\n", report);
        Assert.DoesNotContain("[Ignore-Suppressions:", report);
    }

    [Fact]
    public async Task DebtReportBuilder_WithIgnoreSuppressions_IncludesIgnoreNoticeInHeader()
    {
        // Act
        var report = await DebtReportBuilder.BuildAsync(".", ignoreSuppressions: new[] { "cs", "razor" });

        // Assert
        Assert.StartsWith("# AiNetLinter - debt report [Ignore-Suppressions: cs, razor]\n", report);
    }

    [Fact]
    public async Task DebtReportBuilder_WithIgnoreSuppressionsAll_IncludesAllNoticeInHeader()
    {
        // Act
        var report = await DebtReportBuilder.BuildAsync(".", ignoreSuppressions: new[] { "all" });

        // Assert
        Assert.StartsWith("# AiNetLinter - debt report [Ignore-Suppressions: all]\n", report);
    }
}
