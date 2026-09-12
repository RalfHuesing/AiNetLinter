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
            if (entry.Recommendation is not null)
            {
                Assert.Contains(entry.Recommendation, result.Text, StringComparison.Ordinal);
            }
        });
    }

    [Fact]
    public async Task ScanAsync_CategoryFilter_RendersOnlyTheRequestedCategory()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M()
    {
        var endpoint = ""https://api.example.test/v1"";
        var format = ""yyyy-MM-dd"";
    }
}";

        var result = await FindMagicValuesTestHelpers.RunAsync(
            ("Foo.cs", source), category: MagicValueCategory.ConfigCandidates);

        Assert.Contains("- config_candidates:", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("- constant_candidates:", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_MixedCategories_RendersHigherPrioritySecurityFindingsFirst()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M()
    {
        var apiKey = ""sk-example-secret"";
        var endpoint = ""https://api.example.test/v1"";
    }
}";

        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        var securityPosition = result.Text.IndexOf("- security_candidates:", StringComparison.Ordinal);
        var configPosition = result.Text.IndexOf("- config_candidates:", StringComparison.Ordinal);
        Assert.True(securityPosition >= 0);
        Assert.True(configPosition >= 0);
        Assert.True(securityPosition < configPosition);
    }

    [Fact]
    public async Task ScanAsync_TruncatedResults_RenderOnlyCategoriesWithVisibleFindings()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M()
    {
        var apiKey = ""sk-example-secret"";
        var endpoint = ""https://api.example.test/v1"";
    }
}";

        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), maxResults: 1);

        Assert.Contains("- security_candidates:", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("- config_candidates:", result.Text, StringComparison.Ordinal);
    }
}
