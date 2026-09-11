#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Component")]
public sealed class GetNamespaceTreeToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetNamespaceTreeToolTests()
    {
        _fixture = new McpInMemoryTestContext();
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownKind_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(Project: "SymbolGraphMini", Kind: "unknown_kind"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("unknown_kind", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownProject_ReturnsRecoverableInvalidArgumentWithAvailableList()
    {
        var state = _fixture.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(Project: "DoesNotExistProject"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("DoesNotExistProject", textContent.Text);
        Assert.Contains("SymbolGraphMini", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_AmbiguousProject_ReturnsRecoverableAmbiguousSymbol()
    {
        using var multiProjSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\Multi.slnx",
            new ProjectSpec("App.Core", [("C1.cs", "namespace App.Core; public class C1 {}")]),
            new ProjectSpec("App.Core.Tests", [("T1.cs", "namespace App.Core.Tests; public class T1 {}")]));

        using var multiContext = new McpInMemoryTestContext(multiProjSolution);
        var state = multiContext.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(Project: "Core"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("AMBIGUOUS_SYMBOL", textContent.Text);
        Assert.Contains("App.Core", textContent.Text);
        Assert.Contains("App.Core.Tests", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NoParameters_ReturnsSolutionOverview()
    {
        var state = _fixture.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Solution Overview", textContent.Text);
        Assert.Contains("SymbolGraphMini", textContent.Text);

        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value.Deserialize<NamespaceTreePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Projects);
        Assert.NotEmpty(payload.Projects!);
        Assert.Equal(1, payload.RequestedDepth);
        Assert.Equal(1, payload.EffectiveDepth);
        Assert.False(payload.DepthWasClamped);
    }

    [Fact]
    public async Task ExecuteAsync_SpecificProject_ReturnsNamespaceTree()
    {
        var state = _fixture.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(Project: "SymbolGraphMini", Depth: 2, IncludeTypes: false), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Namespaces in Projekt 'SymbolGraphMini'", textContent.Text);
        Assert.Contains("SymbolGraphMini", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_DepthAboveCap_ReportsRequestedAndEffectiveDepth()
    {
        var state = _fixture.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(Project: "SymbolGraphMini", Depth: 99, IncludeTypes: false), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<NamespaceTreePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal(99, payload!.RequestedDepth);
        Assert.Equal(GetNamespaceTreeTool.MaxDepthCap, payload.EffectiveDepth);
        Assert.True(payload.DepthWasClamped);
    }

    [Fact]
    public async Task ExecuteAsync_SpecificNamespaceAndKind_ReturnsTypes()
    {
        var state = _fixture.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(Project: "SymbolGraphMini", NamespacePrefix: "SymbolGraphMini", Depth: 1, IncludeTypes: true, Kind: "interface"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Typen in Namespace 'SymbolGraphMini' (Projekt: SymbolGraphMini):", textContent.Text);
        Assert.Contains("IGreeting (interface)", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_CaseInsensitiveProjectName_ResolvesCorrectly()
    {
        var state = _fixture.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(Project: "symbolgraphmini"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Namespaces in Projekt 'SymbolGraphMini'", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeDepth_DefaultsTo1()
    {
        var state = _fixture.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(Project: "SymbolGraphMini", Depth: -5), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Namespaces in Projekt 'SymbolGraphMini'", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NamespacePrefixWithoutProject_ResolvesUniqueProjectAutomatically()
    {
        var state = _fixture.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(NamespacePrefix: "SymbolGraphMini"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Typen in Namespace 'SymbolGraphMini' (Projekt: SymbolGraphMini):", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NamespacePrefixWithoutProject_AmbiguousNamespace_ReturnsAmbiguousSymbol()
    {
        using var multiProjSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\Multi.slnx",
            new ProjectSpec("App.Core", [("C1.cs", "namespace Shared.Common; public class C1 {}")]),
            new ProjectSpec("App.Utils", [("U1.cs", "namespace Shared.Common; public class U1 {}")]));

        using var multiContext = new McpInMemoryTestContext(multiProjSolution);
        var state = multiContext.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(NamespacePrefix: "Shared.Common"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("AMBIGUOUS_SYMBOL", textContent.Text);
        Assert.Contains("App.Core", textContent.Text);
        Assert.Contains("App.Utils", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NamespacePrefixWithoutProject_NotFound_ReturnsInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            state, new GetNamespaceTreeInput(NamespacePrefix: "NonExistent.Namespace"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("NonExistent.Namespace", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResponseBytes_ProjectsSameVisibleTypesIntoTextAndStructuredContent()
    {
        var source = "namespace BudgetNs {\n" + string.Join("\n", System.Linq.Enumerable.Range(1, 40)
            .Select(i => $"public class Type{i} {{ }}")) + "\n}";
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\NamespaceBudget.slnx",
            new ProjectSpec("BudgetProject", [("Budget.cs", source)])));
        var result = await GetNamespaceTreeTool.ExecuteAsync(
            context.CreateServer(),
            new GetNamespaceTreeInput(Project: "BudgetProject", NamespacePrefix: "BudgetNs", MaxResponseBytes: 1024),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<NamespaceTreePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.True(payload!.Truncated, result.StructuredContent!.Value.GetRawText());
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(result.StructuredContent!.Value.GetRawText()) <= 1024);
        Assert.Contains("maxResponseBytes", payload.TruncatedBy!);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Equal(payload.ShownCount, payload.Types!.Count);
        Assert.All(payload.Types, type => Assert.Contains(type.Name, text, StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyFinalResponseBudget_WithSufficientPositiveBudget_PreservesCompleteResponseWithoutTruncationMetadata()
    {
        var payload = new NamespaceTreePayload(
            SolutionName: "BudgetSolution",
            Project: "BudgetProject",
            NamespacePrefix: "BudgetNs",
            KindFilter: "all",
            Depth: 1,
            IncludeTypes: true,
            TotalCount: 1,
            ShownCount: 1,
            Truncated: false,
            Types: [new TypeNodeEntry("Type1", "class", "Budget.cs", 1, "public")]);
        var result = new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = "# Typen in Namespace 'BudgetNs' (Projekt: BudgetProject):\n\n- Type1 (class) — Budget.cs:1\n\n## Navigation\n\n- get_class_structure"
                }
            ],
            StructuredContent = JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default),
        };

        var projected = GetNamespaceTreeTool.ApplyFinalResponseBudget(result, 16_384);

        Assert.Same(result, projected);
        var projectedPayload = projected.StructuredContent!.Value.Deserialize<NamespaceTreePayload>(McpJsonOptions.Default);
        Assert.NotNull(projectedPayload);
        Assert.False(projectedPayload!.Truncated);
        Assert.Null(projectedPayload.Next);
        Assert.DoesNotContain("maxResponseBytes", projectedPayload.TruncatedBy ?? [], StringComparer.Ordinal);
        Assert.DoesNotContain("maxResponseBytes", Assert.IsType<TextContentBlock>(Assert.Single(projected.Content)).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(16 * 1024)]
    [InlineData(64 * 1024)]
    public async Task ExecuteAsync_ExactLeafNamespace_ProjectsRootAndDirectTypeAtPublishedBudgets(int maxResponseBytes)
    {
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\LeafNamespace.slnx",
            new ProjectSpec("LeafProject", [("Leaf.cs", "namespace Contract.Leaf; public class DirectLeafType {}")])));

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            context.CreateServer(),
            new GetNamespaceTreeInput("LeafProject", "Contract.Leaf", MaxResponseBytes: maxResponseBytes),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<NamespaceTreePayload>(McpJsonOptions.Default)!;
        Assert.Equal(1, payload.TotalCount);
        Assert.Equal(1, payload.ShownCount);
        Assert.False(payload.Truncated);
        Assert.Equal("Contract.Leaf", Assert.Single(payload.Namespaces!).Namespace);
        Assert.Equal("DirectLeafType", Assert.Single(payload.Types!).Name);
        Assert.Null(payload.Next);
    }

    [Fact]
    public async Task ExecuteAsync_NonexistentNamespace_IsEmptyAndNeverTruncated()
    {
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MissingNamespace.slnx",
            new ProjectSpec("NamespaceProject", [("Existing.cs", "namespace Contract.Existing; public class ExistingType {}")])));

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            context.CreateServer(),
            new GetNamespaceTreeInput("NamespaceProject", "Contract.Missing"),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<NamespaceTreePayload>(McpJsonOptions.Default)!;
        Assert.Equal(0, payload.TotalCount);
        Assert.Equal(0, payload.ShownCount);
        Assert.False(payload.Truncated);
        Assert.Null(payload.Next);
    }

    [Fact]
    public async Task ExecuteAsync_ExactNamespaceWithChildren_PreservesRootTypesAndCompleteChildCount()
    {
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\NamespaceChildren.slnx",
            new ProjectSpec(
                "NamespaceProject",
                [
                    ("Root.cs", "namespace Contract.Root; public class DirectRootType {}"),
                    ("Child.cs", "namespace Contract.Root.Child; public class ChildType {}"),
                ])));

        var result = await GetNamespaceTreeTool.ExecuteAsync(
            context.CreateServer(),
            new GetNamespaceTreeInput("NamespaceProject", "Contract.Root", Depth: 2, IncludeTypes: true),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<NamespaceTreePayload>(McpJsonOptions.Default)!;
        var root = Assert.Single(payload.Namespaces!);
        Assert.Equal("DirectRootType", Assert.Single(root.Types!).Name);
        Assert.Equal("Contract.Root.Child", Assert.Single(root.SubNamespaces!).Namespace);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(2, payload.ShownCount);
        Assert.False(payload.Truncated);
    }

    [Fact]
    public async Task ExecuteAsync_BudgetBelowLeafMinimum_ReturnsExactRetryableBudgetError()
    {
        const string namespaceName = "Contract.NamespaceWithAnIntentionallyLongNameForTheMinimumProjection";
        const string typeName = "DirectTypeWithAnIntentionallyLongNameForTheMinimumProjection";
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MinimumNamespaceBudget.slnx",
            new ProjectSpec(
                "ProjectWithAnIntentionallyLongNameForTheMinimumProjection",
                [("FileWithAnIntentionallyLongNameForTheMinimumProjection.cs", $"namespace {namespaceName}; public class {typeName} {{ }}")])));
        var server = context.CreateServer();
        var input = new GetNamespaceTreeInput(
            "ProjectWithAnIntentionallyLongNameForTheMinimumProjection",
            namespaceName,
            MaxResponseBytes: 512);

        var constrained = await GetNamespaceTreeTool.ExecuteAsync(server, input, CancellationToken.None);

        Assert.True(constrained.IsError);
        Assert.Equal("RESPONSE_BUDGET_TOO_SMALL", constrained.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.maxResponseBytes", constrained.StructuredContent!.Value.GetProperty("fieldPath").GetString());
        Assert.Equal(512, constrained.StructuredContent!.Value.GetProperty("requestedBytes").GetInt32());
        var minimumResponseBytes = constrained.StructuredContent!.Value.GetProperty("minimumResponseBytes").GetInt32();
        Assert.True(minimumResponseBytes > 512);

        var retry = await GetNamespaceTreeTool.ExecuteAsync(
            server,
            input with { MaxResponseBytes = minimumResponseBytes },
            CancellationToken.None);

        Assert.NotEqual(true, retry.IsError);
        var retryPayload = retry.StructuredContent!.Value.Deserialize<NamespaceTreePayload>(McpJsonOptions.Default)!;
        Assert.Equal(1, retryPayload.TotalCount);
        Assert.Equal(1, retryPayload.ShownCount);
        Assert.Equal(typeName, Assert.Single(retryPayload.Types!).Name);
    }
}
