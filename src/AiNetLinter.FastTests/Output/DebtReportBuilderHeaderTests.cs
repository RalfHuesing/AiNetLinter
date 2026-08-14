#nullable enable

using System.Threading.Tasks;
using Xunit;
using AiNetLinter.Output;

namespace AiNetLinter.FastTests.Output;

[Trait("Category", "Unit")]
public sealed class DebtReportBuilderHeaderTests
{
    [Fact]
    public async Task DebtReportBuilder_WithoutIgnoreSuppressions_HeaderStandard()
    {
        var report = await DebtReportBuilder.BuildAsync(".");

        Assert.StartsWith("# AiNetLinter - debt report\n", report);
        Assert.DoesNotContain("[Ignore-Suppressions:", report);
    }

    [Fact]
    public async Task DebtReportBuilder_WithIgnoreSuppressions_IncludesIgnoreNoticeInHeader()
    {
        var report = await DebtReportBuilder.BuildAsync(".", ignoreSuppressions: new[] { "cs", "razor" });

        Assert.StartsWith("# AiNetLinter - debt report [Ignore-Suppressions: cs, razor]\n", report);
    }

    [Fact]
    public async Task DebtReportBuilder_WithIgnoreSuppressionsAll_IncludesAllNoticeInHeader()
    {
        var report = await DebtReportBuilder.BuildAsync(".", ignoreSuppressions: new[] { "all" });

        Assert.StartsWith("# AiNetLinter - debt report [Ignore-Suppressions: all]\n", report);
    }
}
