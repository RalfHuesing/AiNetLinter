using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Maps.Skeleton;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Maps.Skeleton;

[Trait("Category", "Unit")]
[Collection("SymbolGraphCatalog")]
public sealed class SkeletonStableIdTests
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public SkeletonStableIdTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExtractFromDocument_Greeter_HasGreeterTypeIdAndGreetMethodId()
    {
        var solutionDir = System.IO.Path.GetDirectoryName(_fixture.Catalog.Solution.FilePath) ?? "";
        var document = _fixture.Catalog.Solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => (d.FilePath ?? "").EndsWith("Greeter.cs", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(document);

        var args = new LinterArgs { TargetPath = "", Verbose = false };
        var types = await SkeletonMapBuilder.ExtractFromDocumentAsync(document!, solutionDir, args, CancellationToken.None);

        var greeter = types.FirstOrDefault(t => t.Name == "Greeter");
        Assert.NotNull(greeter);
        Assert.NotNull(greeter!.Id);
        Assert.StartsWith("T:", greeter.Id, System.StringComparison.Ordinal);

        var greetMethod = greeter.Members.FirstOrDefault(m => m.Signature.Contains("Greet", System.StringComparison.Ordinal));
        Assert.NotNull(greetMethod);
        Assert.NotNull(greetMethod!.Id);
        Assert.StartsWith("M:", greetMethod.Id, System.StringComparison.Ordinal);
    }
}
