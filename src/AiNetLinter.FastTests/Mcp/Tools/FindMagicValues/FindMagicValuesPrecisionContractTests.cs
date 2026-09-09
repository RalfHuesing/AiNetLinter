#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.MagicValues;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FindMagicValues;

[Trait("Category", "Component")]
public sealed class FindMagicValuesPrecisionContractTests
{
    [Fact]
    public async Task ScanAsync_CategoryEvidenceScopeAndRecommendationStayDisjoint()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M()
    {
        var apiKey = ""sk-example-secret"";
        var endpoint = ""https://api.example.test/v1"";
        throw new InvalidOperationException(""A user-facing localized failure message."");
    }
}";

        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        Assert.False(result.IsMalfunction);
        Assert.NotNull(result.Payload);
        var payload = result.Payload!;
        Assert.Equal("candidate", payload.ResultType);
        Assert.Equal("checked", payload.Summary.Status);
        Assert.Equal(7, payload.Categories.Count);
        Assert.Equal(7, payload.Categories.Select(category => category.Category).Distinct().Count());

        var config = Assert.Single(payload.MagicValues, entry => entry.Category == "config_candidates");
        var localization = Assert.Single(payload.MagicValues, entry => entry.Category == "localization_candidates");
        var security = Assert.Single(payload.MagicValues, entry => entry.Category == "security_candidates");
        Assert.Contains("appsettings", config.Recommendation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("KeyVault", config.Recommendation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IStringLocalizer", localization.Recommendation, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyVault", localization.Recommendation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secret-Store", security.Recommendation, StringComparison.Ordinal);
        Assert.DoesNotContain("appsettings", security.Recommendation, StringComparison.OrdinalIgnoreCase);

        Assert.All(payload.MagicValues, entry =>
        {
            Assert.Equal("candidate", entry.ResultType);
            Assert.NotEmpty(entry.EvidenceBoundary);
            Assert.NotEmpty(entry.Scope);
            Assert.Contains(entry.EvidenceBoundary, result.Text, StringComparison.Ordinal);
            Assert.Contains(entry.Scope, result.Text, StringComparison.Ordinal);
            Assert.Contains(entry.Recommendation, result.Text, StringComparison.Ordinal);
        });
    }
}
