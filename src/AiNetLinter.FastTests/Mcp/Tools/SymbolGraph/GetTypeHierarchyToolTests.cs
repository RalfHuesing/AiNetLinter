#nullable enable

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using static AiNetLinter.TestKit.McpTestResultText;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

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
    public async Task ExecuteAsync_ClassWithBaseAndDerived_ReturnsStructuredSuccessPayload()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(
            state, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = JsonSerializer.Deserialize<TypeHierarchyPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal("SymbolGraphMini.BaseGreeting", payload!.TypeName);
        Assert.Contains(payload.Interfaces, value => value.Contains("IGreeting", StringComparison.Ordinal));
        var subtype = Assert.Single(payload.Subtypes);
        Assert.Contains("SpecialGreeting", subtype, StringComparison.Ordinal);
        Assert.Equal(1, payload.ShownSubtypeCount);
        Assert.Equal(1, payload.TotalSubtypeCount);
        Assert.False(payload.SubtypesTruncated);
        Assert.Empty(payload.SubtypesTruncatedBy);
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
    public async Task ExecuteAsync_CompileErrorFixture_ReturnsResultsWithoutCompileErrorHint()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "ValidClassA", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteRouted_AssemblyAndProjectRoutes_ValidateAssemblySymbolIdentityAndAllowProjectSymbols()
    {
        using var temp = TestTempDirectory.Create("get-type-hierarchy-route-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "HierarchyProbe",
            "namespace Probe; public interface IService { } public class Service : IService { }");

        await using var assemblyRegistry = new AssemblyAnalysisRegistry();
        await using var projectRegistry = ProjectWiringFixtures.CreateLoadedRegistry();
        var targetRoute = AnalysisToolCall.CreateTargetRoute(
            ProjectAnalysisDispatcher.CreateRoute(projectRegistry),
            AssemblyAnalysisDispatcher.CreateRoute(assemblyRegistry));

        // 1. Aktuelle verpackte Assembly-ID auf Assembly-Ziel ist erfolgreich
        var firstLeaseResult = await assemblyRegistry.LeaseAsync(assemblyPath);
        Assert.NotNull(firstLeaseResult.Lease);
        using var firstLease = firstLeaseResult.Lease!;
        var serviceSymbol = firstLease.Context.Compilation.GetTypeByMetadataName("Probe.Service")!;
        var currentAssemblySymbolId = CallGraphTraversal.GetStableSymbolId(serviceSymbol, firstLease.Server.AssemblySymbolIdentity);
        Assert.StartsWith("assembly:", currentAssemblySymbolId, StringComparison.Ordinal);

        var assemblyCallResult = await AnalysisToolCall.ExecuteRouted(
            targetRoute,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest("assembly", assemblyPath),
                new AnalysisToolDispatch(
                    ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, currentAssemblySymbolId, GetTypeHierarchyTool.DefaultMaxResults, default),
                    AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, currentAssemblySymbolId, GetTypeHierarchyTool.DefaultMaxResults, default))));

        Assert.NotEqual(true, assemblyCallResult.IsError);
        var assemblyText = TextOf(assemblyCallResult);
        Assert.Contains("IService", assemblyText, StringComparison.Ordinal);
        Assert.Contains("Probe.IService", assemblyText, StringComparison.Ordinal);

        // 2. Generationenwechsel ueber A -> B -> A
        AssemblyTestHelper.EmitAssembly(
            temp,
            "HierarchyProbe",
            "namespace Probe; public interface IOther { } public class Other : IOther { }");
        var secondLeaseResult = await assemblyRegistry.LeaseAsync(assemblyPath);
        secondLeaseResult.Lease!.Dispose();

        AssemblyTestHelper.EmitAssembly(
            temp,
            "HierarchyProbe",
            "namespace Probe; public interface IService { } public class Service : IService { }");
        var thirdLeaseResult = await assemblyRegistry.LeaseAsync(assemblyPath);
        thirdLeaseResult.Lease!.Dispose();

        // 3. Alte Assembly-ID nach Generationwechsel wird als stale abgelehnt
        var staleCallResult = await AnalysisToolCall.ExecuteRouted(
            targetRoute,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest("assembly", assemblyPath),
                new AnalysisToolDispatch(
                    ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, currentAssemblySymbolId, GetTypeHierarchyTool.DefaultMaxResults, default),
                    AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, currentAssemblySymbolId, GetTypeHierarchyTool.DefaultMaxResults, default))));

        Assert.NotEqual(true, staleCallResult.IsError);
        var staleText = TextOf(staleCallResult);
        Assert.Contains("INVALID_ARGUMENT", staleText, StringComparison.Ordinal);
        Assert.Contains("aktuellen Assembly-Generation", staleText, StringComparison.Ordinal);

        // 4. Eine bare DocumentationCommentId wird auf die aktuelle Assembly-Generation bezogen
        var unwrappedCallResult = await AnalysisToolCall.ExecuteRouted(
            targetRoute,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest("assembly", assemblyPath),
                new AnalysisToolDispatch(
                    ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, "T:Probe.Service", GetTypeHierarchyTool.DefaultMaxResults, default),
                    AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, "T:Probe.Service", GetTypeHierarchyTool.DefaultMaxResults, default))));

        Assert.NotEqual(true, unwrappedCallResult.IsError);
        var unwrappedText = TextOf(unwrappedCallResult);
        Assert.Contains("IService", unwrappedText, StringComparison.Ordinal);

        // 5. Projekt-ID auf Projekt-Ziel bleibt weiterhin erfolgreich
        var projectRoot = ProjectRegistryFixture.CreateProjectRoot(temp, "probe-proj");
        var projectCallResult = await AnalysisToolCall.ExecuteRouted(
            targetRoute,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest("project", projectRoot),
                new AnalysisToolDispatch(
                    ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, default),
                    AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, default))));

        Assert.NotEqual(true, projectCallResult.IsError);
        var projectText = TextOf(projectCallResult);
        Assert.Contains("IGreeting", projectText, StringComparison.Ordinal);
        Assert.Contains("SpecialGreeting", projectText, StringComparison.Ordinal);
    }
}
