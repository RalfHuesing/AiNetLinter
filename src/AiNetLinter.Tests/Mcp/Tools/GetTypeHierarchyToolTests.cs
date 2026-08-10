using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Trait("Category", "Unit")]
[Collection("SymbolGraphCatalog")]
public sealed class GetTypeHierarchyToolTests
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public GetTypeHierarchyToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "irrelevant", CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTypeIdentifier_ReturnsRecoverableSymbolNotFound()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "DoesNotExistXyz", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_IdentifierResolvesToMethodNotType_ReturnsRecoverableInvalidArgument()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting.Greet", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithBaseAndDerived_ReturnsInterfaceAndDerivedClass()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("IGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("SpecialGreeting", textContent.Text, StringComparison.Ordinal);
        // Q5 Sufficiency-Hinweis: get_type_hierarchy trunkiert nie, Hinweis gilt immer.
        Assert.Contains("vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_StableTypeIdentifier_ReturnsInterfaceAndDerivedClass()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));
        var (resolved, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "BaseGreeting", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(resolved!);

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, stableId!, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("IGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("SpecialGreeting", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InterfaceType_ReturnsImplementingClasses()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "IGreeting", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("BaseGreeting", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LeafClassWithoutDerivedTypes_ReturnsNoDerivedTypesMessage()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "SpecialGreeting", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("BaseGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Keine abgeleiteten Typen.", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithImplicitObjectBase_ReturnsExternalBaseTypeInsteadOfEmptyMessage()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("object", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Basisklasse.", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithExternalInterface_ReturnsExternalInterfaceInsteadOfEmptyMessage()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "DisposableGreeting", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("IDisposable", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Interfaces.", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithDiRegistration_IncludesDiRegistrationSection()
    {
        using var fixture = new DiRegistrationMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "ConsoleReporter", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("DI-Registrierungen (heuristisch", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("AddScoped", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithoutDiRegistration_OmitsDiRegistrationSection()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("DI-Registrierungen", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "ValidClassA", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
    }
}
