#nullable enable

using System.Linq;
using AiNetLinter.Core.Checkers;
using Xunit;

namespace AiNetLinter.Tests.Core;

public sealed class ViolationDescriptionTests
{
    [Fact]
    public void ReportViolationAtLine_SetsAllFieldsFromDescription()
    {
        var ctx = TestHelper.CreateContext();
        var desc = new ViolationDescription("MyRule", "Details here", "Fix guidance", "warning");

        ctx.ReportViolationAtLine(42, desc);

        var violation = ctx.Violations.Single();
        Assert.Equal("MyRule", violation.RuleName);
        Assert.Equal("Details here", violation.Details);
        Assert.Equal("Fix guidance", violation.Guidance);
        Assert.Equal("warning", violation.EffectiveSeverity);
        Assert.Equal(42, violation.LineNumber);
    }
}
