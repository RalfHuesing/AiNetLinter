#nullable enable

using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.IntegrationTests.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Baseline;

[Trait("Category", "Integration")]
public sealed class SourceFileCatalogRegistrationTests
{
    [Fact]
    public async Task LoadAsync_SecondSequentialCall_DoesNotRepatchBuildHost()
    {
        // Idempotenz: ein zweiter sequentieller LoadAsync darf RegisterMSBuild nicht
        // erneut durchlaufen (Fast-Pfad).
        var solutionRoot = SolutionRootLocator.Find();
        var fixturePath = Path.Combine(solutionRoot, "tests", "Fixtures", "BaselineMini");

        using var first = await SourceFileCatalog.LoadAsync(fixturePath);
        Assert.NotNull(first);

        using var second = await SourceFileCatalog.LoadAsync(fixturePath);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }
}
