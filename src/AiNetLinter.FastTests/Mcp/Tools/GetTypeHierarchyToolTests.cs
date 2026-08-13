#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.FastTests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

[Trait("Category", "Component")]
public sealed class GetTypeHierarchyToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetTypeHierarchyToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "irrelevant", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTypeIdentifier_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "DoesNotExistXyz", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_IdentifierResolvesToMethodNotType_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting.Greet", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithBaseAndDerived_ReturnsInterfaceAndDerivedClass()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("IGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("SpecialGreeting", textContent.Text, StringComparison.Ordinal);
        // Sufficiency-Hinweis: Basisklassen/Interfaces trunkieren nie, aber die
        // abgeleiteten/implementierenden Typen koennten es bei Ueberschreitung von maxResults —
        // hier unter dem Default-Limit, also gilt der Hinweis.
        Assert.Contains("vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InterfaceWithMultipleImplementers_MaxResultsBelowCount_TruncatesAndSuppressesSufficiencyHint()
    {
        // Regression: get_type_hierarchy trunkierte "Abgeleitete Klassen:"/"Implementierende
        // Typen:" frueher nie — bei einem weit implementierten Interface (z. B. IDisposable in
        // einem fremden Projekt) konnte das den Client-Token-Guard sprengen (dieselbe Bug-Klasse
        // wie get_violations/get_hotspots). IGreeting hat in dieser Fixture zwei transitive
        // Implementierer (BaseGreeting direkt, SpecialGreeting via Vererbung von BaseGreeting).
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "IGreeting", 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Typen gesamt, 1 gezeigt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("maxResults erhoehen", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_StableTypeIdentifier_ReturnsInterfaceAndDerivedClass()
    {
        var state = _fixture.CreateServer();
        var (resolved, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "BaseGreeting", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(resolved!);

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, stableId!, GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("IGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("SpecialGreeting", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InterfaceType_ReturnsImplementingClasses()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "IGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("BaseGreeting", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LeafClassWithoutDerivedTypes_ReturnsNoDerivedTypesMessage()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "SpecialGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("BaseGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Keine abgeleiteten Typen.", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithImplicitObjectBase_ReturnsExternalBaseTypeInsteadOfEmptyMessage()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("object", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Basisklasse.", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithExternalInterface_ReturnsExternalInterfaceInsteadOfEmptyMessage()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "DisposableGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("IDisposable", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Interfaces.", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithDiRegistration_IncludesDiRegistrationSection()
    {
        using var context = new McpInMemoryTestContext(DiRegistrationMiniSolutionSpec.Create());
        using var state = context.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "ConsoleReporter", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("DI-Registrierungen (heuristisch", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("AddScoped", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithoutDiRegistration_OmitsDiRegistrationSection()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("DI-Registrierungen", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "ValidClassA", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
    }
}
