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

namespace AiNetLinter.FastTests.Mcp.Tools;

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
}

