#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools.FileStructure;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Component")]
public sealed class StructureToolResponseBudgetContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(511)]
    [InlineData(65_537)]
    public async Task NamespaceTree_RejectsOutOfContractPublicBudget(int maxResponseBytes)
    {
        using var context = new McpInMemoryTestContext();
        var result = await GetNamespaceTreeTool.ExecuteAsync(
            context.CreateServer(), new GetNamespaceTreeInput(MaxResponseBytes: maxResponseBytes), CancellationToken.None);

        Assert.Contains("INVALID_ARGUMENT", TextOf(result));
        Assert.Contains("$.maxResponseBytes", TextOf(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(511)]
    [InlineData(65_537)]
    public async Task ClassStructure_RejectsOutOfContractPublicBudget(int maxResponseBytes)
    {
        using var context = new McpInMemoryTestContext();
        var result = await GetClassStructureTool.ExecuteAsync(
            context.CreateServer(), new GetClassStructureArgs("Greeter", MaxResponseBytes: maxResponseBytes), CancellationToken.None);

        Assert.Contains("INVALID_ARGUMENT", TextOf(result));
        Assert.Contains("$.maxResponseBytes", TextOf(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(511)]
    [InlineData(65_537)]
    public async Task FileSkeleton_RejectsOutOfContractPublicBudget(int maxResponseBytes)
    {
        using var context = new McpInMemoryTestContext();
        var result = await GetFileSkeletonTool.ExecuteAsync(
            context.CreateServer(), ["src/SymbolGraphMini/Greeter.cs"], maxResponseBytes, CancellationToken.None);

        Assert.Contains("INVALID_ARGUMENT", TextOf(result));
        Assert.Contains("$.maxResponseBytes", TextOf(result));
    }

    [Fact]
    public async Task PublicDefaultsAndMaximumProduceVisibleResponses()
    {
        using var context = new McpInMemoryTestContext();
        var server = context.CreateServer();

        var defaultNamespace = await GetNamespaceTreeTool.ExecuteAsync(server, new GetNamespaceTreeInput(), CancellationToken.None);
        var maximumNamespace = await GetNamespaceTreeTool.ExecuteAsync(server, new GetNamespaceTreeInput(MaxResponseBytes: 65_536), CancellationToken.None);
        var defaultClass = await GetClassStructureTool.ExecuteAsync(server, new GetClassStructureArgs("Greeter"), CancellationToken.None);
        var maximumClass = await GetClassStructureTool.ExecuteAsync(server, new GetClassStructureArgs("Greeter", MaxResponseBytes: 65_536), CancellationToken.None);
        var defaultSkeleton = await GetFileSkeletonTool.ExecuteAsync(server, ["src/SymbolGraphMini/Greeter.cs"], CancellationToken.None);
        var maximumSkeleton = await GetFileSkeletonTool.ExecuteAsync(server, ["src/SymbolGraphMini/Greeter.cs"], 65_536, CancellationToken.None);

        Assert.All(
            new[] { defaultNamespace, maximumNamespace, defaultClass, maximumClass, defaultSkeleton, maximumSkeleton },
            result => Assert.NotEmpty(TextOf(result)));
    }

    [Fact]
    public async Task MinimumBudget_IsAcceptedByEveryPublicStructureTool()
    {
        using var context = new McpInMemoryTestContext();
        var server = context.CreateServer();

        var namespaceResult = await GetNamespaceTreeTool.ExecuteAsync(
            server, new GetNamespaceTreeInput(MaxResponseBytes: 512), CancellationToken.None);
        var classResult = await GetClassStructureTool.ExecuteAsync(
            server, new GetClassStructureArgs("Greeter", MaxResponseBytes: 512), CancellationToken.None);
        var skeletonResult = await GetFileSkeletonTool.ExecuteAsync(
            server, ["src/SymbolGraphMini/Greeter.cs"], 512, CancellationToken.None);

        Assert.All(
            new[] { namespaceResult, classResult, skeletonResult },
            result => Assert.DoesNotContain("muss zwischen", TextOf(result)));
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
