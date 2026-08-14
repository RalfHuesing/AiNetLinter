#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Maps.Skeleton;
using Xunit;

namespace AiNetLinter.FastTests.Maps.Skeleton;

[Trait("Category", "Component")]
public sealed class SkeletonStableIdTests : IDisposable
{
    private readonly McpInMemoryTestContext _fixture;

    public SkeletonStableIdTests()
    {
        _fixture = new McpInMemoryTestContext();
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ExtractFromDocument_Greeter_HasGreeterTypeIdAndGreetMethodId()
    {
        var solutionDir = Path.GetDirectoryName(_fixture.Solution.FilePath) ?? "";
        var document = _fixture.Solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => (d.FilePath ?? "").EndsWith("Greeter.cs", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(document);

        var args = new LinterArgs { TargetPath = "", Verbose = false };
        var types = await SkeletonMapBuilder.ExtractFromDocumentAsync(document!, solutionDir, args, CancellationToken.None);

        var greeter = types.FirstOrDefault(t => t.Name == "Greeter");
        Assert.NotNull(greeter);
        Assert.NotNull(greeter!.Id);
        Assert.StartsWith("T:", greeter.Id, StringComparison.Ordinal);

        var greetMethod = greeter.Members.FirstOrDefault(m => m.Signature.Contains("Greet", StringComparison.Ordinal));
        Assert.NotNull(greetMethod);
        Assert.NotNull(greetMethod!.Id);
        Assert.StartsWith("M:", greetMethod.Id, StringComparison.Ordinal);
    }
}
