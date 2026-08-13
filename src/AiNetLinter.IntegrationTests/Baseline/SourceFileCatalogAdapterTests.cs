#nullable enable

using AiNetLinter.IntegrationTests.Platform;

namespace AiNetLinter.IntegrationTests.Baseline;

[Trait("Category", "Integration")]
public sealed class SourceFileCatalogAdapterTests
{
    [Fact]
    public async Task LoadAsync_MiniFixture_ReturnsSourceFiles()
    {
        await using var fixture = await LoadedFixture.CreateAsync("BaselineMini");
        var files = fixture.Catalog.GetSourceFiles(fixture.RootPath);
        Assert.Contains(files, file => file.RelativePath.EndsWith("ViolatingClass.cs", StringComparison.OrdinalIgnoreCase));
    }
}
